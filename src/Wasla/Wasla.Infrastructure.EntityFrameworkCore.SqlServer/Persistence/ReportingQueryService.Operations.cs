using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private sealed record OperationalSources(IReadOnlyList<ReservationReportingFact> Reservations, IReadOnlyList<TicketReportingFact> Tickets);

    private async Task<OperationalSources> OperationalSourcesAsync(ReportScope scope, ReportFilter f, ReportingWindow w, CancellationToken ct)
    {
        var lower = w.FromDate.AddDays(-2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var upper = w.ToDate.AddDays(2).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var reservations = await db.Reservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.CreatedOnUtc <= w.AsOfUtc &&
            (f.SegmentId == null || r.SegmentId == f.SegmentId) && (f.VisitTypeId == null || r.VisitTypeId == f.VisitTypeId) &&
            (f.BookingSource == null || r.BookingSource == f.BookingSource) &&
            (r.CreatedOnUtc >= lower && r.CreatedOnUtc <= upper || r.BusinessDate >= w.FromDate && r.BusinessDate <= w.ToDate ||
                r.History.Any(h => h.OccurredOnUtc <= w.AsOfUtc && (h.OccurredOnUtc >= lower && h.OccurredOnUtc <= upper ||
                    h.NewBusinessDate >= w.FromDate && h.NewBusinessDate <= w.ToDate || h.OldBusinessDate >= w.FromDate && h.OldBusinessDate <= w.ToDate))))
            .Include(r => r.History).AsSplitQuery().ToArrayAsync(ct);
        var tickets = await db.Tickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) && t.CheckInTimeUtc <= w.AsOfUtc &&
            (f.SegmentId == null || t.SegmentId == f.SegmentId) && (f.VisitTypeId == null || t.VisitTypeId == f.VisitTypeId) &&
            (f.BookingSource == null || t.ReservationId != null && db.Reservations.Any(r => r.Id == t.ReservationId && r.BookingSource == f.BookingSource)) &&
            (t.BusinessDate >= w.FromDate && t.BusinessDate <= w.ToDate || t.History.Any(h => h.OccurredOnUtc <= w.AsOfUtc && h.OccurredOnUtc >= lower && h.OccurredOnUtc <= upper)))
            .Include(t => t.History.Where(h => h.OccurredOnUtc <= w.AsOfUtc)).AsSplitQuery().ToArrayAsync(ct);
        var reservationFacts = reservations.Select(r =>
        {
            var allHistory = r.History.OrderBy(h => h.OccurredOnUtc).ThenBy(h => h.EventType).ToArray();
            var history = allHistory.Where(h => h.OccurredOnUtc <= w.AsOfUtc).ToArray();
            var changes = history.Where(h => h.EventType == ReservationHistoryEventType.Rescheduled).ToArray();
            var original = allHistory.FirstOrDefault(h => h.EventType == ReservationHistoryEventType.Rescheduled)?.OldScheduledStartUtc
                ?? (r.LastRescheduledOnUtc is null ? r.ScheduledStartUtc : (DateTime?)null);
            var latest = changes.LastOrDefault();
            DateTime start;
            DateOnly date;
            if (r.LastRescheduledOnUtc is null || r.LastRescheduledOnUtc <= w.AsOfUtc) { start = r.ScheduledStartUtc; date = r.BusinessDate; }
            else if (latest?.NewScheduledStartUtc is { } known && latest.NewBusinessDate is { } knownDate) { start = known; date = knownDate; }
            else
            {
                var next = allHistory.FirstOrDefault(h => h.EventType == ReservationHistoryEventType.Rescheduled && h.OccurredOnUtc > w.AsOfUtc);
                if (next?.OldScheduledStartUtc is not { } previousStart || next.OldBusinessDate is not { } previousDate)
                    return null;
                start = previousStart; date = previousDate;
            }
            ReservationStatus? state = r.ModifiedOnUtc is null || r.ModifiedOnUtc <= w.AsOfUtc ? r.Status :
                history.LastOrDefault(h => h.ToStatus != null)?.ToStatus;
            return new ReservationReportingFact(r.Id, r.DoctorId, r.DoctorPracticeId, r.PatientId, r.SegmentId, r.VisitTypeId,
                r.VisitTypeCodeSnapshot, r.BookingSource, r.CreatedByApplicationUserId, r.CreatedOnUtc,
                OperationalKpiCalculator.LocalDate(r.CreatedOnUtc, r.TimeZoneIdSnapshot), date, start, original,
                r.CheckInGracePeriodMinutesSnapshot, state, r.TimeZoneIdSnapshot,
                history.Select(h => new ReservationReportingEvent(h.EventType, h.OccurredOnUtc, h.ToStatus, h.OldBusinessDate,
                    h.OldScheduledStartUtc, h.NewBusinessDate, h.NewScheduledStartUtc, h.ActionInitiator, h.PerformedByApplicationUserId, h.ReasonCode, h.Reason)).ToArray());
        }).OfType<ReservationReportingFact>().Where(r => f.ReservationStatus == null || r.Status == f.ReservationStatus).ToArray();
        var ticketFacts = tickets.Select(t => new TicketReportingFact(t.Id, t.DoctorId, t.DoctorPracticeId, t.PatientId,
            t.ReservationId, t.SegmentId, t.VisitTypeId, t.VisitTypeCodeSnapshot, t.SegmentPrioritySnapshot, t.Source,
            t.BusinessDate, t.CheckInTimeUtc, t.LastUpdatedOnUtc <= w.AsOfUtc ? t.Status :
                t.History.OrderBy(h => h.OccurredOnUtc).ThenBy(h => h.EventType).LastOrDefault(h => h.ToStatus != null)?.ToStatus,
            t.PracticeTimeZoneIdSnapshot, t.TicketNumber,
            t.History.Select(h => new TicketReportingEvent(h.EventType, h.OccurredOnUtc, h.ToStatus, h.ActorApplicationUserId)).ToArray()))
            .Where(t => f.TicketStatus == null || t.Status == f.TicketStatus).ToArray();
        return new(reservationFacts, ticketFacts);
    }
    private IReadOnlyList<ReportSeriesPoint> BuildSeries(OperationalSources source, ReportGrouping grouping, ReportingWindow w)
    {
        if (grouping is not (ReportGrouping.Day or ReportGrouping.Week or ReportGrouping.Month)) return [];
        string Key(DateOnly d) => grouping == ReportGrouping.Month ? d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : grouping == ReportGrouping.Week ? d.AddDays(-((int)d.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var keys = Enumerable.Range(w.FromDate.DayNumber, w.ToDate.DayNumber - w.FromDate.DayNumber + 1)
            .Select(n => Key(DateOnly.FromDayNumber(n))).Distinct().ToArray();
        return keys.Select(key =>
        {
            var days = Enumerable.Range(w.FromDate.DayNumber, w.ToDate.DayNumber - w.FromDate.DayNumber + 1)
                .Select(DateOnly.FromDayNumber).Where(d => Key(d) == key).ToArray();
            var window = w with { FromDate = days[0], ToDate = days[^1] };
            return new ReportSeriesPoint(key, key, key, calculator.Calculate(source.Reservations, source.Tickets, window)
                .Where(m => m.MetricCode is "BookingsCreated" or "AppointmentsScheduled" or "CheckIns" or "TicketsCompleted" or "WalkInShare" or "WaitToFirstCall").ToArray());
        }).ToArray();
    }
    private async Task<IReadOnlyList<ReportSeriesPoint>> BuildBreakdownsAsync(ReportScope scope, OperationalSources source, ReportingWindow w, CancellationToken ct)
    {
        var names = await db.DoctorPractices.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameAr, p.NameEn }).ToArrayAsync(ct);
        return names.Select(p => new ReportSeriesPoint(p.Id.ToString(), p.NameAr, p.NameEn,
            calculator.Calculate(source.Reservations.Where(r => r.PracticeId == p.Id).ToArray(), source.Tickets.Where(t => t.PracticeId == p.Id).ToArray(), w))).ToArray();
    }
    private async Task<IReadOnlyList<ReportRecord>> RecordsAsync(ReportScope scope, string code, OperationalSources source,
        ReportFilter f, ReportingWindow w, CancellationToken ct)
    {
        if (scope.Actor == ReportActor.SuperAdmin) return await PlatformRecordsAsync(scope, code, w, ct);
        if (ReportDefinitionCatalog.Definitions.Single(d => d.Code == code).Financial) return await FinancialRecordsAsync(scope, code, f, w, ct);
        var reservationView = scope.Permissions.Contains(PermissionNames.DoctorPracticeReservationsViewOwn);
        var ticketView = scope.Permissions.Contains(PermissionNames.DoctorPracticeTicketsViewOwn);
        var receptionReservationPractices = scope.Actor == ReportActor.Reception ? await DelegatedPracticesAsync(scope, PermissionNames.PracticeReservationsView, ct) : [];
        var receptionTicketPractices = scope.Actor == ReportActor.Reception ? await DelegatedPracticesAsync(scope, PermissionNames.PracticeTicketsView, ct) : [];
        var reservations = source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.ScheduledBusinessDate, w) ||
            OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w)).Select(r =>
        {
            var allowed = reservationView && scope.Actor == ReportActor.Doctor || receptionReservationPractices.Contains(r.PracticeId);
            var route = scope.Actor == ReportActor.Doctor ? $"/api/v1/doctors/me/practices/{r.PracticeId}/reservations/{r.Id}"
                : $"/api/v1/reception/practices/{r.PracticeId}/reservations/{r.Id}";
            return new ReportRecord(r.Id, "Reservation", r.PracticeId, r.ScheduledBusinessDate, r.CreatedAtUtc, r.Id.ToString(),
                r.Status?.ToString() ?? "Unknown", allowed ? r.PatientId : null, DetailRoute: allowed ? route : null,
                AvailableActions: [new("ViewDetails", allowed, allowed ? route : null, allowed ? null : "Report.UnderlyingPermissionRequired")]);
        });
        var tickets = source.Tickets.Where(t => OperationalKpiCalculator.InWindow(t.BusinessDate, w)).Select(t =>
        {
            var allowed = ticketView && scope.Actor == ReportActor.Doctor || receptionTicketPractices.Contains(t.PracticeId);
            var route = $"/api/v1/practices/{t.PracticeId}/tickets/{t.Id}";
            return new ReportRecord(t.Id, "Ticket", t.PracticeId, t.BusinessDate, t.CheckInAtUtc,
                t.TicketNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), t.Status?.ToString() ?? "Unknown",
                allowed ? t.PatientId : null, DetailRoute: allowed ? route : null, AvailableActions: [new("ViewDetails", allowed, allowed ? route : null)]);
        });
        return code == "reservations" ? reservations.ToArray() : code is "queue" or "patients" ? tickets.ToArray() : reservations.Concat(tickets).ToArray();
    }
    private Task<Guid[]> DelegatedPracticesAsync(ReportScope s, string permission, CancellationToken ct)
    {
        var id = SystemPermissionIds.For(permission);
        return (from a in db.ReceptionPracticeAssignments.AsNoTracking()
            join p in db.ReceptionPracticeAssignmentPermissions.AsNoTracking() on a.Id equals p.AssignmentId
            join r in db.Receptions.AsNoTracking() on a.ReceptionId equals r.Id
            where r.ApplicationUserId == s.ActorId && a.IsActive && p.PermissionId == id && s.PracticeIds.Contains(a.DoctorPracticeId)
            select a.DoctorPracticeId).Distinct().ToArrayAsync(ct);
    }
}
