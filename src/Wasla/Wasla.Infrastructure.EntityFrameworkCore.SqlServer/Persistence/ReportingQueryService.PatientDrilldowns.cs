using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<ReportRecord[]> PatientDrilldownAsync(ReportScope scope, OperationalSources source,
        Dictionary<(string ResourceType, Guid Id), ReportRecord> original, ReportingWindow w, string metric, CancellationToken ct)
    {
        var current = source.Tickets.Where(t => OperationalKpiCalculator.InWindow(t.BusinessDate, w)).GroupBy(t => t.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.CheckInAtUtc).First());
        if (metric == "PatientRetention90d")
        {
            var from = DateOnly.FromDayNumber(Math.Max(1, w.FromDate.DayNumber - 90));
            var previous = await db.AllTickets.AsNoTracking().Where(t => t.DoctorId == scope.DoctorId && scope.PracticeIds.Contains(t.DoctorPracticeId) &&
                t.BusinessDate >= from && t.BusinessDate < w.FromDate && t.CheckInTimeUtc <= w.AsOfUtc)
                .GroupBy(t => t.PatientId).Select(g => g.OrderBy(t => t.CheckInTimeUtc).ThenBy(t => t.Id)
                    .Select(t => new { t.Id, t.PatientId, t.DoctorPracticeId, t.BusinessDate, t.CheckInTimeUtc, t.TicketNumber }).First()).ToArrayAsync(ct);
            var permitted = scope.Permissions.Contains(PermissionNames.DoctorPracticeTicketsViewOwn);
            return previous.Select(t => new ReportRecord(t.Id, "PatientActivityCohort", t.DoctorPracticeId, t.BusinessDate,
                DateTime.SpecifyKind(t.CheckInTimeUtc, DateTimeKind.Utc), t.TicketNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), "Prior90DayCohort",
                permitted ? t.PatientId : null, DetailRoute: permitted ? $"/api/v1/practices/{t.DoctorPracticeId}/tickets/{t.Id}" : null,
                MetricValue: current.ContainsKey(t.PatientId) ? 1 : 0, MetricDenominator: 1, SourceRecordId: t.Id)).ToArray();
        }
        var ids = current.Keys.ToArray();
        var first = await db.AllTickets.AsNoTracking().Where(t => t.DoctorId == scope.DoctorId && ids.Contains(t.PatientId) && t.CheckInTimeUtc <= w.AsOfUtc)
            .GroupBy(t => t.PatientId).Select(g => g.OrderBy(t => t.CheckInTimeUtc).ThenBy(t => t.Id).Select(t => new { t.PatientId, t.BusinessDate }).First()).ToArrayAsync(ct);
        var selected = metric == "UniqueDoctorPatients" ? ids.ToHashSet() : first.Where(p => metric == "NewToDoctorPatients"
            ? OperationalKpiCalculator.InWindow(p.BusinessDate, w) : p.BusinessDate < w.FromDate).Select(p => p.PatientId).ToHashSet();
        return current.Where(c => selected.Contains(c.Key)).Select(c => original[("Ticket", c.Value.Id)] with { ResourceType = "PatientActivityCohort", MetricValue = 1, SourceRecordId = c.Value.Id }).ToArray();
    }
    private async Task<IReadOnlyDictionary<Guid, ReportMetric[]>> PracticePatientMetricsAsync(ReportScope scope, OperationalSources source, ReportingWindow w, CancellationToken ct)
    {
        var current = source.Tickets.Where(t => OperationalKpiCalculator.InWindow(t.BusinessDate, w)).ToArray();
        var ids = current.Select(t => t.PatientId).Distinct().ToArray();
        var first = await db.AllTickets.AsNoTracking().Where(t => t.DoctorId == scope.DoctorId && ids.Contains(t.PatientId) && t.CheckInTimeUtc <= w.AsOfUtc)
            .GroupBy(t => t.PatientId).Select(g => g.OrderBy(t => t.CheckInTimeUtc).ThenBy(t => t.Id).Select(t => new { t.PatientId, t.BusinessDate }).First()).ToArrayAsync(ct);
        return scope.PracticeIds.ToDictionary(practice => practice, practice =>
        {
            var patients = current.Where(t => t.PracticeId == practice).Select(t => t.PatientId).Distinct().ToHashSet();
            return new[] { calculator.Count("UniqueDoctorPatients", patients.Count),
                calculator.Count("NewToDoctorPatients", first.Count(p => patients.Contains(p.PatientId) && OperationalKpiCalculator.InWindow(p.BusinessDate, w))),
                calculator.Count("ReturningDoctorPatients", first.Count(p => patients.Contains(p.PatientId) && p.BusinessDate < w.FromDate)) };
        });
    }
}
