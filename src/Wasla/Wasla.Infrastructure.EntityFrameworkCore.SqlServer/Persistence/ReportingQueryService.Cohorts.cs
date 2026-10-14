using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<IReadOnlyList<ReportMetric>> PatientMetricsAsync(ReportScope scope, IReadOnlyList<TicketReportingFact> tickets,
        ReportingWindow w, bool clinicalAllowed, CancellationToken ct)
    {
        var current = tickets.Where(t => OperationalKpiCalculator.InWindow(t.BusinessDate, w)).Select(t => t.PatientId).Distinct().ToArray();
        var history = await db.Tickets.AsNoTracking().Where(t => t.DoctorId == scope.DoctorId && current.Contains(t.PatientId) && t.CheckInTimeUtc <= w.AsOfUtc)
            .GroupBy(t => t.PatientId).Select(g => g.OrderBy(t => t.CheckInTimeUtc).ThenBy(t => t.Id)
                .Select(t => new { t.PatientId, t.BusinessDate }).First()).ToArrayAsync(ct);
        var priorFrom = DateOnly.FromDayNumber(Math.Max(0, w.FromDate.DayNumber - 90));
        var prior = await db.Tickets.AsNoTracking().Where(t => t.DoctorId == scope.DoctorId && scope.PracticeIds.Contains(t.DoctorPracticeId) && t.BusinessDate >= priorFrom &&
            t.BusinessDate < w.FromDate && t.CheckInTimeUtc <= w.AsOfUtc).Select(t => t.PatientId).Distinct().ToArrayAsync(ct);
        var clinical = clinicalAllowed ? await ClinicalCompletionMetricAsync(scope, w, ct)
            : calculator.Missing("ClinicalWorkflowCompleted", ReportDataStatus.NotAvailable, "Report.ClinicalPermissionRequired");
        return [calculator.Count("UniqueDoctorPatients", current.Length),
            calculator.Count("NewToDoctorPatients", history.Count(t => OperationalKpiCalculator.InWindow(t.BusinessDate, w))),
            calculator.Count("ReturningDoctorPatients", history.Count(t => t.BusinessDate < w.FromDate)),
            calculator.Ratio("PatientRetention90d", current.Intersect(prior).Count(), prior.Length),
            calculator.Count("FollowUpActivity", tickets.Count(t => t.VisitTypeCode == "FollowUp" && OperationalKpiCalculator.InWindow(t.BusinessDate, w))), clinical];
    }
    private async Task<ReportMetric> ClinicalCompletionMetricAsync(ReportScope scope, ReportingWindow w, CancellationToken ct)
    {
        var fromUtc = DateOnly.FromDayNumber(Math.Max(0, w.FromDate.DayNumber - 2)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var before = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, w.ToDate.DayNumber + 2)).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var rows = await (from e in db.MedicalEncounters.AsNoTracking()
            join t in db.Tickets.AsNoTracking() on e.TicketId equals t.Id
            where scope.PracticeIds.Contains(e.DoctorPracticeId) && e.CompletedAtUtc != null && e.CompletedAtUtc <= w.AsOfUtc &&
                e.CompletedAtUtc >= fromUtc && e.CompletedAtUtc <= before
            select new { e.PatientId, e.CompletedAtUtc, t.PracticeTimeZoneIdSnapshot }).ToArrayAsync(ct);
        var eligible = rows.Where(e => OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.CompletedAtUtc!.Value, e.PracticeTimeZoneIdSnapshot), w)).ToArray();
        return scope.Actor == ReportActor.SuperAdmin && eligible.Select(e => e.PatientId).Distinct().Count() is > 0 and < 5
            ? calculator.Missing("ClinicalWorkflowCompleted", ReportDataStatus.NotAvailable, "Report.SmallGroupSuppressed")
            : calculator.Count("ClinicalWorkflowCompleted", eligible.Length, eligible.Length);
    }
    private async Task<IReadOnlyList<ReportMetric>> PlatformMetricsAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct)
    {
        var doctors = await db.Doctors.AsNoTracking().Where(d => (scope.DoctorId == null || d.Id == scope.DoctorId) && (d.CreatedOnUtc == default || d.CreatedOnUtc <= w.AsOfUtc))
            .Select(d => new { d.Id, d.CreatedOnUtc, d.ModifiedOnUtc, d.ApprovalStatus, d.ApprovedOnUtc,
                d.RejectedOnUtc, d.SuspendedOnUtc, d.ReactivatedOnUtc }).ToArrayAsync(ct);
        var ids = doctors.Select(d => d.Id).ToArray();
        var histories = await db.DoctorStatusHistories.AsNoTracking().Where(h => ids.Contains(h.DoctorId) && h.OccurredOnUtc <= w.AsOfUtc).ToArrayAsync(ct);
        var states = doctors.Select(d =>
        {
            var history = histories.Where(h => h.DoctorId == d.Id).OrderBy(h => h.OccurredOnUtc).LastOrDefault();
            DoctorApprovalStatus? status = history?.ToStatus;
            var lastKnownChange = new[] { d.ModifiedOnUtc, d.ApprovedOnUtc, d.RejectedOnUtc, d.SuspendedOnUtc, d.ReactivatedOnUtc }
                .Where(t => t != null).Max();
            if (status is null && (w.AsOfUtc == w.GeneratedAtUtc || lastKnownChange <= w.AsOfUtc)) status = d.ApprovalStatus;
            return new { d.Id, Status = status };
        }).ToArray();
        var bounds = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
        var trailing = w.AsOfUtc.AddDays(-30);
        var activeDoctors = await db.Reservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.CreatedOnUtc >= trailing && r.CreatedOnUtc <= w.AsOfUtc)
            .Select(r => r.DoctorId).Union(db.Tickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) && t.CheckInTimeUtc >= trailing && t.CheckInTimeUtc <= w.AsOfUtc).Select(t => t.DoctorId)).Distinct().ToArrayAsync(ct);
        var activePatients = await db.Reservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.CreatedOnUtc >= trailing && r.CreatedOnUtc <= w.AsOfUtc)
            .Select(r => r.PatientId).Union(db.Tickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) && t.CheckInTimeUtc >= trailing && t.CheckInTimeUtc <= w.AsOfUtc).Select(t => t.PatientId)).Distinct().ToArrayAsync(ct);
        var approved = states.Where(d => d.Status == DoctorApprovalStatus.Approved).Select(d => d.Id).ToArray();
        var unknownStates = states.LongCount(d => d.Status == null);
        ReportMetric StatusCount(string metric, long value) => calculator.Count(metric, value, states.LongLength - unknownStates,
            excluded: unknownStates, warning: unknownStates > 0 ? "Report.DoctorStateAtAsOfNotRecorded" : null);
        var registeredPatients = await db.Patients.AsNoTracking().CountAsync(p => p.CreatedOnUtc != default && p.CreatedOnUtc <= w.AsOfUtc, ct);
        var acquisitions = await db.Patients.AsNoTracking().CountAsync(p => p.CreatedOnUtc >= bounds.FromUtc && p.CreatedOnUtc < bounds.BeforeUtc && p.CreatedOnUtc <= w.AsOfUtc, ct);
        var firstApprovals = histories.Where(h => h.ToStatus == DoctorApprovalStatus.Approved).GroupBy(h => h.DoctorId)
            .ToDictionary(g => g.Key, g => g.Min(h => h.OccurredOnUtc));
        var waits = doctors.Where(d => d.CreatedOnUtc != default && firstApprovals.TryGetValue(d.Id, out var date) && date >= d.CreatedOnUtc)
            .Select(d => (decimal)(firstApprovals[d.Id] - d.CreatedOnUtc).TotalMinutes).ToArray();
        var result = new List<ReportMetric>
        {
            calculator.Count("RegisteredDoctors", doctors.Count(d => d.CreatedOnUtc != default && d.CreatedOnUtc <= w.AsOfUtc), doctors.Length,
                doctors.Any(d => d.CreatedOnUtc == default) ? ReportDataStatus.Partial : ReportDataStatus.Complete, doctors.LongCount(d => d.CreatedOnUtc == default)),
            StatusCount("PendingDoctors", states.LongCount(d => d.Status == DoctorApprovalStatus.Pending)),
            StatusCount("ApprovedDoctors", approved.LongLength), StatusCount("RejectedDoctors", states.LongCount(d => d.Status == DoctorApprovalStatus.Rejected)),
            StatusCount("SuspendedDoctors", states.LongCount(d => d.Status == DoctorApprovalStatus.Suspended)),
            w.AsOfUtc == w.GeneratedAtUtc ? calculator.Count("ActivePractices", await db.DoctorPractices.CountAsync(p => scope.PracticeIds.Contains(p.Id) && p.IsActive, ct))
                : calculator.Missing("ActivePractices", ReportDataStatus.NotMeasured, "Report.PracticeLifecycleHistoryNotRecorded"),
            registeredPatients is > 0 and < 5 ? calculator.Missing("RegisteredPatients", ReportDataStatus.NotAvailable, "Report.SmallGroupSuppressed")
                : calculator.Count("RegisteredPatients", registeredPatients), calculator.Count("ReceptionAccounts", await db.Receptions.CountAsync(r => r.CreatedOnUtc <= w.AsOfUtc, ct)),
            calculator.Count("DoctorAcquisition", doctors.Count(d => d.CreatedOnUtc >= bounds.FromUtc && d.CreatedOnUtc < bounds.BeforeUtc && d.CreatedOnUtc <= w.AsOfUtc)),
            acquisitions is > 0 and < 5 ? calculator.Missing("PatientAcquisition", ReportDataStatus.NotAvailable, "Report.SmallGroupSuppressed") : calculator.Count("PatientAcquisition", acquisitions),
            calculator.Count("PlatformActiveDoctor30d", activeDoctors.Intersect(approved).Count()),
            activePatients.Length is > 0 and < 5 ? calculator.Missing("PlatformActivePatient30d", ReportDataStatus.NotAvailable, "Report.SmallGroupSuppressed") : calculator.Count("PlatformActivePatient30d", activePatients.Length),
            calculator.Ratio("FeatureAdoption", activeDoctors.Intersect(approved).Count(), approved.Length),
            calculator.Ratio("ApprovalWaitMinutes", waits.Sum(), waits.Length, doctors.Length - waits.Length),
            await ClinicalCompletionMetricAsync(scope, w, ct)
        };
        result.AddRange(await AdditionalPlatformMetricsAsync(scope, code, w, ct));
        return result;
    }
    private async Task<IReadOnlyList<ReportRecord>> PlatformRecordsAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct)
    {
        if (code is not ("doctor-lifecycle" or "geography" or "overview")) return [];
        var allowed = scope.Permissions.Contains(PermissionNames.DoctorsViewDetails);
        var doctors = await db.Doctors.AsNoTracking().Where(d => (scope.DoctorId == null || d.Id == scope.DoctorId) && d.CreatedOnUtc != default && d.CreatedOnUtc <= w.AsOfUtc)
            .Select(d => new { d.Id, d.NameAr, d.CreatedOnUtc, d.ApprovalStatus, d.ModifiedOnUtc, d.ApprovedOnUtc, d.RejectedOnUtc, d.SuspendedOnUtc, d.ReactivatedOnUtc }).ToArrayAsync(ct);
        var ids = doctors.Select(d => d.Id).ToArray();
        var history = await db.DoctorStatusHistories.AsNoTracking().Where(h => ids.Contains(h.DoctorId) && h.OccurredOnUtc <= w.AsOfUtc).ToArrayAsync(ct);
        return doctors.Select(d =>
        {
            var state = history.Where(h => h.DoctorId == d.Id).OrderBy(h => h.OccurredOnUtc).LastOrDefault()?.ToStatus;
            var lastChange = new[] { d.ModifiedOnUtc, d.ApprovedOnUtc, d.RejectedOnUtc, d.SuspendedOnUtc, d.ReactivatedOnUtc }.Where(t => t != null).Max();
            if (state == null && (w.AsOfUtc == w.GeneratedAtUtc || lastChange <= w.AsOfUtc)) state = d.ApprovalStatus;
            return new ReportRecord(d.Id, "Doctor", null, OperationalKpiCalculator.LocalDate(d.CreatedOnUtc, w.ReportTimeZoneId),
                DateTime.SpecifyKind(d.CreatedOnUtc, DateTimeKind.Utc), d.NameAr, state?.ToString() ?? "Unknown", DetailRoute: allowed ? "/api/v1/doctors/" + d.Id : null);
        }).ToArray();
    }
}
