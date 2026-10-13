using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Reporting;

public sealed record ReservationReportingEvent(ReservationHistoryEventType EventType, DateTime RecordedAtUtc,
    ReservationStatus? ToStatus, DateOnly? OldBusinessDate, DateTime? OldStartUtc, DateOnly? NewBusinessDate,
    DateTime? NewStartUtc, ReservationActionInitiator Initiator, Guid? ActorId, string? ReasonCode, string? Reason);
public sealed record ReservationReportingFact(Guid Id, Guid DoctorId, Guid PracticeId, Guid PatientId,
    Guid SegmentId, Guid VisitTypeId, string VisitTypeCode, ReservationBookingSource BookingSource,
    Guid CreatedBy, DateTime CreatedAtUtc, DateOnly CreatedBusinessDate, DateOnly ScheduledBusinessDate,
    DateTime ScheduledStartUtc, DateTime? OriginalStartUtc, int? GraceMinutes, ReservationStatus? Status,
    string TimeZoneId, IReadOnlyList<ReservationReportingEvent> Events);
public sealed record TicketReportingEvent(TicketHistoryEventType EventType, DateTime RecordedAtUtc, TicketStatus? ToStatus, Guid? ActorId);
public sealed record TicketReportingFact(Guid Id, Guid DoctorId, Guid PracticeId, Guid PatientId,
    Guid? ReservationId, Guid SegmentId, Guid VisitTypeId, string VisitTypeCode, int Priority,
    TicketSource Source, DateOnly BusinessDate, DateTime CheckInAtUtc, TicketStatus? Status,
    string TimeZoneId, int TicketNumber, IReadOnlyList<TicketReportingEvent> Events);

public sealed class OperationalKpiCalculator(IKpiDefinitionRegistry registry)
{
    public ReportMetric Count(string code, decimal value, long sample = 0, ReportDataStatus status = ReportDataStatus.Complete,
        long excluded = 0, string? warning = null)
    {
        var d = registry.FindDefinition(code);
        return new(code, d.NameAr, d.NameEn, value, d.Unit, value, null, sample, excluded, status, warning);
    }
    public ReportMetric Missing(string code, ReportDataStatus status, string warning)
    { var d = registry.FindDefinition(code); return new(code, d.NameAr, d.NameEn, null, d.Unit, null, null, 0, 0, status, warning); }
    public ReportMetric Ratio(string code, decimal numerator, decimal denominator, long excluded = 0,
        ReportDataStatus status = ReportDataStatus.Complete)
    {
        var d = registry.FindDefinition(code);
        return new(code, d.NameAr, d.NameEn, denominator > 0 ? numerator / denominator * (d.Unit == "percent" ? 100 : 1) : null,
            d.Unit, numerator, denominator, (long)denominator, excluded,
            denominator > 0 ? excluded > 0 ? ReportDataStatus.Partial : status : ReportDataStatus.NotAvailable,
            denominator == 0 ? "Report.EmptyDenominator" : excluded > 0 ? "Report.ExcludedInvalidSamples" : null);
    }

    public IReadOnlyList<ReportMetric> Calculate(IReadOnlyList<ReservationReportingFact> reservations,
        IReadOnlyList<TicketReportingFact> tickets, ReportingWindow w)
    {
        var scheduled = reservations.Where(r => InWindow(r.ScheduledBusinessDate, w)).ToArray();
        var booked = reservations.Where(r => InWindow(r.CreatedBusinessDate, w)).ToArray();
        var attended = tickets.Where(t => InWindow(t.BusinessDate, w)).ToArray();
        var reservationEvents = reservations.SelectMany(r => r.Events.Where(e => InWindow(LocalDate(e.RecordedAtUtc, r.TimeZoneId), w))).ToArray();
        var ticketEvents = tickets.SelectMany(t => t.Events.Where(e => InWindow(LocalDate(e.RecordedAtUtc, t.TimeZoneId), w))).ToArray();
        var cancellation = reservationEvents.Where(e => e.EventType == ReservationHistoryEventType.Cancelled).ToArray();
        var reschedules = reservations.SelectMany(r => r.Events.Where(e => e.EventType == ReservationHistoryEventType.Rescheduled &&
            InWindow(LocalDate(e.RecordedAtUtc, r.TimeZoneId), w)).Select(e => (r.Id, Event: e))).ToArray();
        bool CancelledBeforeSlot(ReservationReportingFact r) => r.Events.Any(e => e.EventType == ReservationHistoryEventType.Cancelled && e.RecordedAtUtc < r.ScheduledStartUtc);
        var matured = scheduled.Where(r => r.ScheduledStartUtc <= w.AsOfUtc && !CancelledBeforeSlot(r)).ToArray();
        var graceKnown = matured.Where(r => r.GraceMinutes != null && r.ScheduledStartUtc.AddMinutes(r.GraceMinutes.Value) <= w.AsOfUtc).ToArray();
        var ticketIds = tickets.Where(t => t.ReservationId != null && t.CheckInAtUtc <= w.AsOfUtc).Select(t => t.ReservationId!.Value).ToHashSet();
        var leads = booked.Where(r => r.OriginalStartUtc >= r.CreatedAtUtc).Select(r => (decimal)(r.OriginalStartUtc!.Value - r.CreatedAtUtc).TotalMinutes).ToArray();
        List<decimal> punctual = [];
        foreach (var t in attended.Where(t => t.ReservationId != null))
        {
            var r = reservations.SingleOrDefault(r => r.Id == t.ReservationId);
            if (r is null) continue;
            var changes = r.Events.Where(e => e.EventType == ReservationHistoryEventType.Rescheduled && e.RecordedAtUtc <= t.CheckInAtUtc)
                .OrderBy(e => e.RecordedAtUtc).ToArray();
            var start = changes.LastOrDefault()?.NewStartUtc ?? r.OriginalStartUtc;
            if (start is { } scheduledStart) punctual.Add(t.CheckInAtUtc <= scheduledStart.AddMinutes(5) ? 1 : 0);
        }
        List<decimal> waits = [], starts = [], callStarts = [], services = [];
        foreach (var t in attended)
        {
            var events = t.Events.OrderBy(e => e.RecordedAtUtc).ThenBy(e => e.EventType).ToArray();
            var calls = events.Where(e => IsCall(e.EventType)).ToArray();
            var firstCall = calls.FirstOrDefault();
            if (firstCall is not null && firstCall.RecordedAtUtc >= t.CheckInAtUtc) waits.Add((decimal)(firstCall.RecordedAtUtc - t.CheckInAtUtc).TotalMinutes);
            var firstStart = events.FirstOrDefault(e => e.EventType == TicketHistoryEventType.InProgress && e.RecordedAtUtc >= t.CheckInAtUtc);
            if (firstStart is not null)
            {
                starts.Add((decimal)(firstStart.RecordedAtUtc - t.CheckInAtUtc).TotalMinutes);
                var preceding = calls.LastOrDefault(e => e.RecordedAtUtc <= firstStart.RecordedAtUtc);
                if (preceding is not null) callStarts.Add((decimal)(firstStart.RecordedAtUtc - preceding.RecordedAtUtc).TotalMinutes);
            }
            foreach (var completed in events.Where(e => e.EventType == TicketHistoryEventType.Completed && InWindow(LocalDate(e.RecordedAtUtc, t.TimeZoneId), w)))
            {
                var began = events.LastOrDefault(e => e.EventType == TicketHistoryEventType.InProgress && e.RecordedAtUtc <= completed.RecordedAtUtc);
                if (began is not null) services.Add((decimal)(completed.RecordedAtUtc - began.RecordedAtUtc).TotalMinutes);
            }
        }
        var completedCount = ticketEvents.Count(e => e.EventType == TicketHistoryEventType.Completed);
        var unknownStates = scheduled.LongCount(r => r.Status == null);
        var ticketUnknown = attended.LongCount(t => t.Status == null);
        return [Count("BookingsCreated", booked.Length, booked.Length), Count("AppointmentsScheduled", scheduled.Length, scheduled.Length),
            Count("CancellationsByEvent", cancellation.Length, cancellation.Length),
            Ratio("CancellationRateScheduledCohort", scheduled.Count(r => r.Status == ReservationStatus.Cancelled), scheduled.Length, unknownStates),
            Ratio("ReservationNoShowRate", graceKnown.Count(r => r.Status == ReservationStatus.NoShow), graceKnown.Length,
                matured.Count(r => r.GraceMinutes == null) + unknownStates),
            Ratio("TicketNoShowRate", attended.Count(t => t.Status == TicketStatus.NoShow), attended.Length, ticketUnknown),
            Count("RescheduleEvents", reschedules.Length, reschedules.Length), Count("RescheduledReservations", reschedules.Select(r => r.Id).Distinct().Count()),
            Ratio("ReservationToTicketConversion", matured.Count(r => ticketIds.Contains(r.Id)), matured.Length, unknownStates),
            Ratio("BookingLeadTimeMinutes", leads.Sum(), leads.Length, booked.Length - leads.Length),
            Ratio("PunctualArrival", punctual.Sum(), punctual.Count, attended.Count(t => t.ReservationId != null) - punctual.Count),
            Count("CheckIns", attended.Length, attended.Length), Count("WalkIns", attended.Count(t => t.Source == TicketSource.WalkIn)),
            Ratio("WalkInShare", attended.Count(t => t.Source == TicketSource.WalkIn), attended.Length),
            Count("Waiting", attended.Count(t => t.Status == TicketStatus.Waiting), attended.Length, excluded: ticketUnknown),
            Count("Called", attended.Count(t => t.Status == TicketStatus.Called), attended.Length, excluded: ticketUnknown),
            Count("InProgress", attended.Count(t => t.Status == TicketStatus.InProgress), attended.Length, excluded: ticketUnknown),
            Count("TicketsCompleted", completedCount, completedCount), Count("TicketsCancelled", attended.Count(t => t.Status == TicketStatus.Cancelled)),
            Count("TicketsNoShow", attended.Count(t => t.Status == TicketStatus.NoShow)),
            Ratio("WaitToFirstCall", waits.Sum(), waits.Count, attended.Length - waits.Count),
            Ratio("WaitToServiceStart", starts.Sum(), starts.Count, attended.Length - starts.Count),
            Ratio("CallToStart", callStarts.Sum(), callStarts.Count, attended.Length - callStarts.Count),
            Ratio("ServiceDuration", services.Sum(), services.Count, completedCount - services.Count),
            Count("CallAttempts", ticketEvents.Count(e => IsCall(e.EventType))), Count("Recalls", ticketEvents.Count(e => e.EventType == TicketHistoryEventType.Recalled)),
            Count("OperationalExceptions", ticketEvents.Count(e => e.EventType is TicketHistoryEventType.ForceCheckIn or TicketHistoryEventType.ManualCalled or
                TicketHistoryEventType.CallNoResponse or TicketHistoryEventType.OperationalDayEnded)),
            Missing("CapacityMinutes", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            Missing("BookedUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            Missing("ActualServiceUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            Missing("SegmentQuotaUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalQuotaNotRecorded")];
    }
    public static bool IsCall(TicketHistoryEventType type) => type is TicketHistoryEventType.Called or TicketHistoryEventType.Recalled or TicketHistoryEventType.ManualCalled;
    public static bool InWindow(DateOnly date, ReportingWindow w) => date >= w.FromDate && date <= w.ToDate;
    public static DateOnly LocalDate(DateTime utc, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(zone)));
}
