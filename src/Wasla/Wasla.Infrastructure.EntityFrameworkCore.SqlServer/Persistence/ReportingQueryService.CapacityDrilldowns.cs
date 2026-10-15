using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private Task<PracticeReportingPolicySnapshot[]> CapacitySnapshotsAsync(ReportScope scope, ReportingWindow w, CancellationToken ct)
    {
        var anchor = DateOnly.FromDayNumber(Math.Max(0, w.FromDate.DayNumber - 2)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        // One prior anchor per Practice plus changes within the report window;
        // older policy revisions cannot affect these interval calculations.
        return db.Set<PracticeReportingPolicySnapshot>().AsNoTracking().Where(s => scope.PracticeIds.Contains(s.PracticeId) && s.RecordedAtUtc <= w.AsOfUtc &&
            (s.RecordedAtUtc >= anchor || !db.Set<PracticeReportingPolicySnapshot>().Any(later => later.PracticeId == s.PracticeId &&
                later.RecordedAtUtc < anchor && later.RecordedAtUtc <= w.AsOfUtc && later.RevisionNumber > s.RevisionNumber)))
            .OrderBy(s => s.RecordedAtUtc).ThenBy(s => s.RevisionNumber).ToArrayAsync(ct);
    }
    private async Task<ReportRecord[]> CapacityDrilldownAsync(ReportScope scope, OperationalSources source, ReportFilter filter,
        ReportingWindow w, string metric, CancellationToken ct)
    {
        var snapshots = await CapacitySnapshotsAsync(scope, w, ct);
        var names = await db.DoctorPractices.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.Id)).Select(p => new { p.Id, p.NameAr }).ToArrayAsync(ct);
        return names.Select(p =>
        {
            var value = CapacityMetrics(snapshots, scope with { PracticeIds = [p.Id] }, source, w, filter).Single(m => m.MetricCode == metric);
            return new ReportRecord(p.Id, "PracticeCapacityContribution", p.Id, w.FromDate, w.AsOfUtc, p.NameAr, value.DataStatus.ToString(),
                MetricValue: value.Numerator, MetricDenominator: value.Denominator, ExclusionReasonCode: value.WarningCode, SourceRecordId: p.Id,
                EvidenceSourceIds: snapshots.Where(s => s.PracticeId == p.Id).Select(s => s.Id).ToArray());
        }).ToArray();
    }
    private async Task<ReportRecord[]> ClinicalDrilldownAsync(ReportScope scope, ReportingWindow w, CancellationToken ct)
    {
        var lowerBound = DateOnly.FromDayNumber(Math.Max(0, w.FromDate.DayNumber - 2)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var before = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, w.ToDate.DayNumber + 2)).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var rows = await (from e in db.MedicalEncounters.AsNoTracking() join t in db.AllTickets.AsNoTracking() on e.TicketId equals t.Id
            where e.DoctorId == scope.DoctorId && scope.PracticeIds.Contains(e.DoctorPracticeId) && e.CompletedAtUtc != null && e.CompletedAtUtc <= w.AsOfUtc &&
                e.CompletedAtUtc >= lowerBound && e.CompletedAtUtc <= before
            select new { e.Id, e.DoctorPracticeId, e.CompletedAtUtc, t.PracticeTimeZoneIdSnapshot }).ToArrayAsync(ct);
        return rows.Where(e => OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.CompletedAtUtc!.Value, e.PracticeTimeZoneIdSnapshot), w))
            .Select(e => new ReportRecord(e.Id, "CompletedClinicalWorkflow", e.DoctorPracticeId,
                OperationalKpiCalculator.LocalDate(e.CompletedAtUtc!.Value, e.PracticeTimeZoneIdSnapshot), DateTime.SpecifyKind(e.CompletedAtUtc.Value, DateTimeKind.Utc),
                e.Id.ToString(), "Completed", DetailRoute: $"/api/v1/doctors/me/practices/{e.DoctorPracticeId}/encounters/{e.Id}", MetricValue: 1, SourceRecordId: e.Id)).ToArray();
    }
}
