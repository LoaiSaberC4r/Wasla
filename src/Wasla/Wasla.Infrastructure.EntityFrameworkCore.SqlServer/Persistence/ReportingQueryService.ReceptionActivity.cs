using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<ReportRecord[]> ReceptionActivityRowsAsync(ReportScope scope, OperationalSources source,
        Dictionary<(string ResourceType, Guid Id), ReportRecord> original, ReportingWindow w, CancellationToken ct)
    {
        var actors = await db.Receptions.AsNoTracking().Where(r => db.DoctorPractices.Any(p => p.DoctorId == r.OwnerDoctorId && scope.PracticeIds.Contains(p.Id)))
            .Select(r => r.ApplicationUserId).ToArrayAsync(ct);
        List<ReportRecord> result = [];
        foreach (var r in source.Reservations)
        foreach (var e in r.Events.Where(e => e.ActorId != null && actors.Contains(e.ActorId.Value) &&
            OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), w)))
            result.Add(original[("Reservation", r.Id)] with { Id = e.Id, ResourceType = "ReservationEvent", SourceRecordId = r.Id,
                BusinessDate = OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), RecordedAtUtc = e.RecordedAtUtc,
                Status = e.EventType.ToString(), MetricValue = 1, RecordedByApplicationUserId = e.ActorId });
        foreach (var t in source.Tickets)
        foreach (var e in t.Events.Where(e => e.ActorId != null && actors.Contains(e.ActorId.Value) &&
            OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), w)))
            result.Add(original[("Ticket", t.Id)] with { Id = e.Id, ResourceType = "TicketEvent", SourceRecordId = t.Id,
                BusinessDate = OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), RecordedAtUtc = e.RecordedAtUtc,
                Status = e.EventType.ToString(), MetricValue = 1, RecordedByApplicationUserId = e.ActorId });
        return result.ToArray();
    }
}
