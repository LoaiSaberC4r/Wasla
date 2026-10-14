using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Operations;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class PlatformOperationsService(WaslaDbContext db, IReportingAccessGuard guard, IDateTimeProvider clock,
    IOptions<Phase18Options> options) : IPlatformOperationsService
{
    public async Task<Result<PlatformDataQualityResponse>> DataQualityAsync(CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.DataQualityView, null, null, ct);
        if (access.IsFailure) return Result<PlatformDataQualityResponse>.Fail(access.Errors);
        var reservations = await db.Reservations.LongCountAsync(r => !db.ReservationHistories.Any(h => h.ReservationId == r.Id), ct);
        var tickets = await db.Tickets.LongCountAsync(t => !db.TicketHistories.Any(h => h.TicketId == t.Id), ct);
        var uncaptured = await db.DoctorPractices.LongCountAsync(p => !db.Set<PracticeReportingPolicySnapshot>().Any(s => s.PracticeId == p.Id), ct);
        var doctors = await db.Doctors.LongCountAsync(d => d.CreatedOnUtc == default, ct);
        var patients = await db.Patients.LongCountAsync(p => p.CreatedOnUtc == default, ct);
        return Result<PlatformDataQualityResponse>.Ok(new(clock.UtcNow,
            [new("Reservations.MissingHistory", "حجوزات بدون تاريخ", "Reservations without history", reservations, ReportDataStatus.Complete, "Current persisted source"),
                new("Tickets.MissingHistory", "تذاكر بدون تاريخ", "Tickets without history", tickets, ReportDataStatus.Complete, "Current persisted source"),
                new("Practices.UncapturedPolicy", "سياسات لم تسجل بعد", "Uncaptured practice policies", uncaptured, ReportDataStatus.Complete, "Current captured policy"),
                new("Doctors.MissingRegistrationTimestamp", "تاريخ تسجيل الطبيب مفقود", "Missing Doctor registration timestamp", doctors, ReportDataStatus.Complete, "Current source quality"),
                new("Patients.MissingRegistrationTimestamp", "تاريخ تسجيل المريض مفقود", "Missing patient registration timestamp", patients is > 0 and < 5 ? 0 : patients,
                    patients is > 0 and < 5 ? ReportDataStatus.NotAvailable : ReportDataStatus.Complete, "Current source quality",
                    patients is > 0 and < 5 ? "Report.SmallGroupSuppressed" : null)],
            ["Report.HistoricalStateIsNotInferredFromCurrentState", "Report.NoClinicalOrPrivateFinancialDetails"]));
    }
    public async Task<Result<PlatformJobHealthResponse>> JobsAsync(CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.JobsView, null, null, ct);
        if (access.IsFailure) return Result<PlatformJobHealthResponse>.Fail(access.Errors);
        var rows = await db.Set<OperatingGenerationRun>().AsNoTracking().OrderByDescending(r => r.StartedAtUtc).Take(20)
            .Select(r => new JobRunMetadata(r.Id, r.StartedAtUtc, r.FinishedAtUtc, r.Status, r.FailureCode)).ToArrayAsync(ct);
        var state = !options.Value.ObligationGenerationEnabled ? "Disabled" : rows.Length == 0 ? "NotMeasured"
            : rows[0].Status == "Failed" ? "Failed" : clock.UtcNow - (rows[0].FinishedAtUtc ?? rows[0].StartedAtUtc) >
                TimeSpan.FromSeconds(options.Value.ObligationGenerationIntervalSeconds * 3) ? "Stale" : rows[0].Status;
        var outbox = await db.EmailOutboxMessages.AsNoTracking().GroupBy(m => m.Status).Select(g => new { Status = g.Key, Count = g.LongCount() }).ToArrayAsync(ct);
        long Count(EmailOutboxStatus s) => outbox.Where(o => o.Status == s).Sum(o => o.Count);
        var oldest = await db.EmailOutboxMessages.Where(m => m.Status == EmailOutboxStatus.Pending || m.Status == EmailOutboxStatus.Failed).MinAsync(m => (DateTime?)m.CreatedOnUtc, ct);
        var expired = await db.EmailOutboxMessages.LongCountAsync(m => m.Status == EmailOutboxStatus.Processing && m.NextAttemptOnUtc < clock.UtcNow, ct);
        var refresh = await db.Set<ReportingRefreshRun>().AsNoTracking().OrderByDescending(r => r.StartedAtUtc).Take(20)
            .Select(r => new JobRunMetadata(r.Id, r.StartedAtUtc, r.FinishedAtUtc, r.Status, r.FailureCode)).ToArrayAsync(ct);
        var refreshHealth = !options.Value.ReportingPolicyRefreshEnabled ? "Disabled" : refresh.Length == 0 ? "NotMeasured" :
            clock.UtcNow - (refresh[0].FinishedAtUtc ?? refresh[0].StartedAtUtc) > TimeSpan.FromSeconds(options.Value.ReportingPolicyRefreshIntervalSeconds * 3)
                ? "Stale" : refresh[0].Status;
        return Result<PlatformJobHealthResponse>.Ok(new(clock.UtcNow, state, options.Value.ObligationGenerationEnabled,
            options.Value.ObligationGenerationIntervalSeconds, rows, new(Count(EmailOutboxStatus.Pending), Count(EmailOutboxStatus.Processing),
                Count(EmailOutboxStatus.Sent), Count(EmailOutboxStatus.Failed), oldest, expired),
            rows.Length == 0 ? ["Jobs.NoPersistedGenerationRun"] : [], refreshHealth, refresh));
    }
    public async Task<Result<PlatformAttentionResponse>> AttentionAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.PlatformDashboard, null, null, ct);
        if (access.IsFailure) return Result<PlatformAttentionResponse>.Fail(access.Errors);
        if (pageNumber is < 1 or > 1_000_000 || pageSize is < 1 or > 100) return Result<PlatformAttentionResponse>.Fail(ReportingErrors.InvalidFilter);
        var permissions = access.Value.Permissions;
        var pendingQuery = db.Doctors.AsNoTracking().Where(d => d.ApprovalStatus == DoctorApprovalStatus.Pending);
        var specializationQuery = db.DoctorSpecializationRequests.AsNoTracking().Where(r => r.Status == DoctorSpecializationRequestStatus.PendingReview);
        var caseQuery = db.Set<AdministrativeInvestigation>().AsNoTracking().Where(c => permissions.Contains(Phase18PermissionNames.InvestigationsView) &&
            (c.Status == InvestigationStatus.Open || c.Status == InvestigationStatus.InReview));
        var pendingCount = await pendingQuery.LongCountAsync(ct);
        var specializationCount = await specializationQuery.LongCountAsync(ct);
        var caseCount = await caseQuery.LongCountAsync(ct);
        var keys = pendingQuery.Select(d => new { d.Id, At = d.CreatedOnUtc, Kind = 1 })
            .Concat(specializationQuery.Select(r => new { r.Id, At = r.SubmittedOnUtc, Kind = 2 }))
            .Concat(caseQuery.Select(c => new { c.Id, At = c.CreatedOnUtc, Kind = 3 }));
        var selected = await keys.OrderBy(k => k.At).ThenBy(k => k.Kind).ThenBy(k => k.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var doctorIds = selected.Where(k => k.Kind == 1).Select(k => k.Id).ToArray();
        var specializationIds = selected.Where(k => k.Kind == 2).Select(k => k.Id).ToArray();
        var caseIds = selected.Where(k => k.Kind == 3).Select(k => k.Id).ToArray();
        var pending = await pendingQuery.Where(d => doctorIds.Contains(d.Id)).Select(d =>
            new PlatformAttentionItem(d.Id, "Doctor", "اعتماد طبيب", "Doctor approval", "Pending", d.CreatedOnUtc,
                permissions.Contains(PermissionNames.DoctorsViewDetails) ? "/api/v1/doctors/" + d.Id : null,
                new ReportAction[] { new("OpenApproval", permissions.Contains(PermissionNames.DoctorsViewDetails)) })).ToArrayAsync(ct);
        var specializations = await specializationQuery.Where(r => specializationIds.Contains(r.Id))
            .Select(r => new PlatformAttentionItem(r.Id, "SpecializationRequest", "مراجعة تخصص", "Specialization review", "PendingReview", r.SubmittedOnUtc,
                permissions.Contains(PermissionNames.DoctorSpecializationRequestsViewDetails) ? "/api/v1/doctor-specialization-requests/" + r.Id : null,
                new ReportAction[] { new("OpenSpecializationReview", permissions.Contains(PermissionNames.DoctorSpecializationRequestsViewDetails)) })).ToArrayAsync(ct);
        ReportAction[] caseActions = [new("ViewInvestigation", true)];
        var cases = await caseQuery.Where(c => caseIds.Contains(c.Id)).Select(c =>
                new PlatformAttentionItem(c.Id, "Investigation", c.TitleAr, c.TitleEn ?? "Investigation", c.Status.ToString(), c.CreatedOnUtc,
                    "/api/v1/admin/investigation-cases/" + c.Id, caseActions)).ToArrayAsync(ct);
        var failures = permissions.Contains(Phase18PermissionNames.JobsView)
            ? await db.EmailOutboxMessages.LongCountAsync(m => m.Status == EmailOutboxStatus.Failed, ct) : (long?)null;
        var rows = pending.Concat(specializations).Concat(cases).OrderBy(r => r.RecordedAtUtc).ThenBy(r => r.Id).ToArray();
        return Result<PlatformAttentionResponse>.Ok(new(clock.UtcNow, pendingCount, specializationCount,
            permissions.Contains(Phase18PermissionNames.InvestigationsView) ? caseCount : null, failures,
            new(rows, pendingCount + specializationCount + caseCount, pageNumber, pageSize),
            ["Operations.PriorityDoesNotImplySlaOrNotifications"]));
    }
}
