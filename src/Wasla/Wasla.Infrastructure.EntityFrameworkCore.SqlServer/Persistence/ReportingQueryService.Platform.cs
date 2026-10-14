using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Operations;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<IReadOnlyList<ReportMetric>> AdditionalPlatformMetricsAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct)
    {
        var bounds = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
        var size = w.ToDate.DayNumber - w.FromDate.DayNumber + 1;
        var preceding = ReportingTimeScope.Bounds(DateOnly.FromDayNumber(Math.Max(1, w.FromDate.DayNumber - size)), w.FromDate.AddDays(-1), w.ReportTimeZoneId);
        var bookings = await db.Reservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) &&
            r.CreatedOnUtc >= preceding.FromUtc && r.CreatedOnUtc < bounds.BeforeUtc && r.CreatedOnUtc <= w.AsOfUtc)
            .Select(r => new { r.DoctorId, r.PatientId, At = r.CreatedOnUtc }).ToArrayAsync(ct);
        var checkIns = await db.Tickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) &&
            t.CheckInTimeUtc >= preceding.FromUtc && t.CheckInTimeUtc < bounds.BeforeUtc && t.CheckInTimeUtc <= w.AsOfUtc)
            .Select(t => new { t.DoctorId, t.PatientId, At = t.CheckInTimeUtc }).ToArrayAsync(ct);
        var activity = bookings.Concat(checkIns).ToArray();
        var currentDoctors = activity.Where(a => a.At >= bounds.FromUtc).Select(a => a.DoctorId).Distinct().ToArray();
        var previousDoctors = activity.Where(a => a.At < bounds.FromUtc).Select(a => a.DoctorId).Distinct().ToArray();
        var currentPatients = activity.Where(a => a.At >= bounds.FromUtc).Select(a => a.PatientId).Distinct().ToArray();
        var previousPatients = activity.Where(a => a.At < bounds.FromUtc).Select(a => a.PatientId).Distinct().ToArray();
        List<ReportMetric> result = [calculator.Ratio("DoctorRetention", currentDoctors.Intersect(previousDoctors).Count(), previousDoctors.Length),
            previousPatients.Length is > 0 and < 5 || currentPatients.Length is > 0 and < 5 || currentPatients.Intersect(previousPatients).Count() is > 0 and < 5
                ? calculator.Missing("PatientRetention", ReportDataStatus.NotAvailable, "Report.SmallGroupSuppressed")
                : calculator.Ratio("PatientRetention", currentPatients.Intersect(previousPatients).Count(), previousPatients.Length),
            calculator.Count("FeatureBookingDoctors", bookings.Where(a => a.At >= bounds.FromUtc).Select(a => a.DoctorId).Distinct().Count()),
            calculator.Count("FeatureQueueDoctors", checkIns.Where(a => a.At >= bounds.FromUtc).Select(a => a.DoctorId).Distinct().Count()),
            calculator.Missing("PageViewAdoption", ReportDataStatus.NotInstrumented, "Report.PageViewInstrumentationNotRecorded"),
            calculator.Missing("SearchAdoption", ReportDataStatus.NotInstrumented, "Report.SearchInstrumentationNotRecorded")];
        var statuses = await db.DoctorStatusHistories.AsNoTracking().Where(h => (scope.DoctorId == null || h.DoctorId == scope.DoctorId) &&
            h.OccurredOnUtc >= bounds.FromUtc && h.OccurredOnUtc < bounds.BeforeUtc && h.OccurredOnUtc <= w.AsOfUtc)
            .Select(h => new { h.FromStatus, h.ToStatus }).ToArrayAsync(ct);
        result.Add(calculator.Count("DoctorSuspensions", statuses.Count(s => s.ToStatus == DoctorApprovalStatus.Suspended)));
        result.Add(calculator.Count("DoctorReactivations", statuses.Count(s => s.FromStatus == DoctorApprovalStatus.Suspended && s.ToStatus == DoctorApprovalStatus.Approved)));
        var specEvents = await (from h in db.DoctorSpecializationRequestHistories.AsNoTracking()
            join r in db.DoctorSpecializationRequests.AsNoTracking() on h.DoctorSpecializationRequestId equals r.Id
            where (scope.DoctorId == null || r.DoctorId == scope.DoctorId) && h.PerformedOnUtc >= bounds.FromUtc &&
                h.PerformedOnUtc < bounds.BeforeUtc && h.PerformedOnUtc <= w.AsOfUtc
            select h.Action).ToArrayAsync(ct);
        result.Add(calculator.Count("SpecializationApprovals", specEvents.Count(a => a == DoctorSpecializationRequestAction.Approved)));
        var reviews = await db.DoctorSpecializationRequests.AsNoTracking().Where(r => (scope.DoctorId == null || r.DoctorId == scope.DoctorId) &&
            r.ReviewedOnUtc >= bounds.FromUtc && r.ReviewedOnUtc < bounds.BeforeUtc && r.ReviewedOnUtc <= w.AsOfUtc)
            .Select(r => new { r.SubmittedOnUtc, r.ReviewedOnUtc }).ToArrayAsync(ct);
        var durations = reviews.Where(r => r.ReviewedOnUtc >= r.SubmittedOnUtc).Select(r => (decimal)(r.ReviewedOnUtc!.Value - r.SubmittedOnUtc).TotalMinutes).ToArray();
        result.Add(calculator.Ratio("SpecializationReviewMinutes", durations.Sum(), durations.Length, reviews.Length - durations.Length));
        var investigationEvents = await db.Set<InvestigationHistory>().AsNoTracking().Where(h => h.RecordedAtUtc <= w.AsOfUtc)
            .Select(h => new { h.CaseId, h.SequenceNumber, h.Status, h.Operation, h.RecordedAtUtc }).ToArrayAsync(ct);
        if (scope.Permissions.Contains(Phase18PermissionNames.InvestigationsView))
        {
            result.Add(calculator.Count("OpenInvestigations", investigationEvents.GroupBy(h => h.CaseId).Count(g => g.MaxBy(h => h.SequenceNumber)!.Status != InvestigationStatus.Closed)));
            result.Add(calculator.Count("InvestigationResolutions", investigationEvents.Count(h => h.Operation == "Resolved" && h.RecordedAtUtc >= bounds.FromUtc && h.RecordedAtUtc < bounds.BeforeUtc)));
        }
        if (w.AsOfUtc != w.GeneratedAtUtc)
        {
            foreach (var metric in new[] { "CurrentBookableDoctors", "PublicEligibleDoctors", "VerificationCompleteDoctors", "PendingApprovalAgeMinutes" })
                result.Add(calculator.Missing(metric, ReportDataStatus.NotMeasured, "Report.HistoricalGovernanceMetadataNotCaptured"));
        }
        else
        {
            var pending = await db.Doctors.AsNoTracking().Where(d => (scope.DoctorId == null || d.Id == scope.DoctorId) &&
                d.ApprovalStatus == DoctorApprovalStatus.Pending).Select(d => d.CreatedOnUtc).ToArrayAsync(ct);
            var valid = pending.Where(d => d != default && d <= w.AsOfUtc).ToArray();
            result.Add(calculator.Ratio("PendingApprovalAgeMinutes", valid.Sum(d => (decimal)(w.AsOfUtc - d).TotalMinutes), valid.Length, pending.Length - valid.Length));
            result.Add(calculator.Count("VerificationCompleteDoctors", await db.Doctors.CountAsync(d => (scope.DoctorId == null || d.Id == scope.DoctorId) &&
                d.PersonalIdFrontMediaKey != "" && d.PersonalIdBackMediaKey != "" && d.SyndicateCardFrontMediaKey != "", ct)));
            var eligible = from d in db.Doctors.AsNoTracking() join u in db.ApplicationUsers.AsNoTracking() on d.ApplicationUserId equals u.Id
                where (scope.DoctorId == null || d.Id == scope.DoctorId) && d.ApprovalStatus == DoctorApprovalStatus.Approved && u.IsActive &&
                    db.DoctorPractices.Any(p => p.DoctorId == d.Id && p.IsActive && scope.PracticeIds.Contains(p.Id))
                select d.Id;
            result.Add(calculator.Count("PublicEligibleDoctors", await eligible.CountAsync(ct)));
            var bookable = await eligible.CountAsync(d => db.PublicPracticeAvailabilitySlots.Any(s => s.DoctorId == d &&
                scope.PracticeIds.Contains(s.DoctorPracticeId) && s.IsAvailable && s.VisibleFromUtc <= w.AsOfUtc && s.SlotStartUtc > w.AsOfUtc &&
                db.DoctorPractices.Any(p => p.Id == s.DoctorPracticeId && p.IsActive) &&
                db.DoctorPracticeConfigurations.Any(c => c.DoctorPracticeId == s.DoctorPracticeId && c.AllowOnlineBooking) &&
                db.DoctorPracticeSegmentVisitTypePrices.Any(price => price.DoctorPracticeId == s.DoctorPracticeId &&
                    db.DoctorPracticeSegments.Any(seg => seg.Id == price.SegmentId && seg.IsDefault && seg.IsActive) &&
                    db.DoctorPracticeVisitTypes.Any(t => t.Id == price.VisitTypeId && t.Type == DoctorPracticeVisitTypeCode.NewConsultation && t.IsActive))), ct);
            result.Add(calculator.Count("CurrentBookableDoctors", bookable) with { DataStatus = ReportDataStatus.Partial, WarningCode = "Report.PublicAvailabilityProjectionObservedAtGeneration" });
        }
        if (code == "security-metadata")
        {
            if (w.AsOfUtc == w.GeneratedAtUtc)
            {
                result.Add(calculator.Count("ActiveUserAccounts", await db.ApplicationUsers.LongCountAsync(u => u.IsActive, ct)));
                result.Add(calculator.Count("InactiveUserAccounts", await db.ApplicationUsers.LongCountAsync(u => !u.IsActive, ct)));
                result.Add(calculator.Count("FirstLoginAccounts", await db.ApplicationUsers.LongCountAsync(u => u.IsFirstLogin, ct)));
                result.Add(calculator.Count("RoleAssignments", await db.UserRoles.LongCountAsync(ct)));
                result.Add(calculator.Count("PermissionGrants", await db.RolePermissions.LongCountAsync(ct)));
                result.Add(calculator.Count("AssignedPracticePermissionGrants", await db.ReceptionPracticeAssignmentPermissions.LongCountAsync(p =>
                    db.ReceptionPracticeAssignments.Any(a => a.Id == p.AssignmentId && a.IsActive), ct)));
                result.Add(calculator.Count("SuperAdminAccounts", await db.SuperAdmins.LongCountAsync(a => !a.IsDeleted, ct)));
            }
            else foreach (var metric in new[] { "ActiveUserAccounts", "InactiveUserAccounts", "FirstLoginAccounts", "RoleAssignments", "PermissionGrants", "AssignedPracticePermissionGrants", "SuperAdminAccounts" })
                result.Add(calculator.Missing(metric, ReportDataStatus.NotMeasured, "Report.HistoricalAccountGovernanceNotCaptured"));
            result.Add(calculator.Count("PasswordResetChallenges", await db.PasswordResetChallenges.LongCountAsync(c =>
                c.CreatedOnUtc >= bounds.FromUtc && c.CreatedOnUtc < bounds.BeforeUtc && c.CreatedOnUtc <= w.AsOfUtc, ct)));
            result.Add(calculator.Count("PasswordResetConsumptions", await db.PasswordResetChallenges.LongCountAsync(c =>
                c.ConsumedOnUtc >= bounds.FromUtc && c.ConsumedOnUtc < bounds.BeforeUtc && c.ConsumedOnUtc <= w.AsOfUtc, ct)));
            result.Add(calculator.Missing("SecurityLoginEvents", ReportDataStatus.NotInstrumented, "Report.LoginSecurityEventsNotPersisted"));
        }
        return result;
    }
    private async Task<IReadOnlyList<ReportSeriesPoint>> PlatformBreakdownsAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct)
    {
        List<ReportSeriesPoint> result = [];
        if (code == "adoption") result.AddRange(await PlatformCohortsAsync(scope, w, ct));
        if (w.AsOfUtc != w.GeneratedAtUtc) return result;
        if (code == "geography")
        {
            var geography = await (from p in db.DoctorPractices.AsNoTracking() join g in db.Governorates.AsNoTracking() on p.GovernorateId equals g.Id
                join city in db.Cities.AsNoTracking() on p.CityId equals city.Id
                where scope.PracticeIds.Contains(p.Id) select new { g.Id, g.NameAr, g.NameEn, CityId = city.Id, CityAr = city.NameAr, CityEn = city.NameEn, p.DoctorId }).ToArrayAsync(ct);
            foreach (var g in geography.GroupBy(g => (g.Id, g.NameAr, g.NameEn)))
                result.Add(new("Governorate:" + g.Key.Id, g.Key.NameAr, g.Key.NameEn, [calculator.Count("PracticeCoverage", g.Count())]));
            foreach (var g in geography.GroupBy(g => (g.CityId, g.CityAr, g.CityEn)))
                result.Add(new("City:" + g.Key.CityId, g.Key.CityAr, g.Key.CityEn, [calculator.Count("PracticeCoverage", g.Count())]));
            var specs = await (from s in db.DoctorSpecializations.AsNoTracking() join d in db.Doctors.AsNoTracking() on s.DoctorId equals d.Id
                join m in db.MedicalSpecializations.AsNoTracking() on s.MedicalSpecializationId equals m.Id
                where (scope.DoctorId == null || d.Id == scope.DoctorId) && d.ApprovalStatus == DoctorApprovalStatus.Approved
                select new { m.Id, m.NameAr, m.NameEn, d.ApplicationUserId }).ToArrayAsync(ct);
            foreach (var s in specs.GroupBy(s => (s.Id, s.NameAr, s.NameEn)))
                result.Add(new("Specialization:" + s.Key.Id, s.Key.NameAr, s.Key.NameEn, [calculator.Count("SpecializationCoverage", s.Select(s => s.ApplicationUserId).Distinct().Count())]));
        }
        if (code == "doctor-lifecycle")
        {
            var states = await db.Doctors.AsNoTracking().Where(d => scope.DoctorId == null || d.Id == scope.DoctorId).Select(d => d.ApprovalStatus).ToArrayAsync(ct);
            foreach (var s in states.GroupBy(s => s)) result.Add(new("Approval:" + s.Key, ApprovalAr(s.Key), s.Key.ToString(), [calculator.Count("RegisteredDoctors", s.Count())]));
            // Reasons stay behind the existing governance detail route; only status-transition counts enter this report.
        }
        if (code == "security-metadata")
        {
            var roles = await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                select r.Name).ToArrayAsync(ct);
            foreach (var role in roles.GroupBy(r => r)) result.Add(new("Role:" + role.Key, RoleAr(role.Key), role.Key, [calculator.Count("RoleAssignments", role.Count())]));
        }
        return result;
    }
    private static string ApprovalAr(DoctorApprovalStatus status) => status switch
    { DoctorApprovalStatus.Pending => "قيد المراجعة", DoctorApprovalStatus.Approved => "معتمد", DoctorApprovalStatus.Rejected => "مرفوض", _ => "موقوف" };
    private static string RoleAr(string role) => role switch
    { SystemRoleNames.Doctor => "طبيب", SystemRoleNames.Reception => "استقبال", SystemRoleNames.Patient => "مريض", _ => "إدارة" };
}
