using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase18OperationalAnalyticsTests
{
    private static readonly DateTime AsOf = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ReportingWindow Window = new(new(2026, 10, 13), new(2026, 10, 13), AsOf, AsOf, "UTC");
    private static readonly OperationalKpiCalculator Calculator = new(new KpiDefinitionRegistry());
    [Fact]
    public void Booking_creation_and_scheduled_cohorts_are_distinct_and_walkins_never_convert_bookings()
    {
        var reservation = ReservationFact(AsOf.AddDays(-10), AsOf.AddHours(-3));
        var walkin = TicketFact(AsOf.AddHours(-1), null, TicketSource.WalkIn, []);
        var metrics = Calculator.Calculate([reservation], [walkin], Window);
        Assert.Equal(0, Metric(metrics, "BookingsCreated").Value);
        Assert.Equal(1, Metric(metrics, "AppointmentsScheduled").Value);
        Assert.Equal(0, Metric(metrics, "ReservationToTicketConversion").Numerator);
        Assert.Equal(1, Metric(metrics, "ReservationToTicketConversion").Denominator);
    }
    [Fact]
    public void Multiple_reschedules_count_events_once_per_event_and_distinct_reservations_once()
    {
        var reservation = ReservationFact(AsOf.AddDays(-2), AsOf.AddHours(-1)) with
        { Events = [new(ReservationHistoryEventType.Rescheduled, AsOf.AddHours(-4), null, Window.FromDate, AsOf.AddHours(-6), Window.FromDate, AsOf.AddHours(-2), ReservationActionInitiator.Doctor, null, null, null),
            new(ReservationHistoryEventType.Rescheduled, AsOf.AddHours(-3), null, Window.FromDate, AsOf.AddHours(-2), Window.FromDate, AsOf.AddHours(-1), ReservationActionInitiator.Doctor, null, null, null)] };
        var metrics = Calculator.Calculate([reservation], [], Window);
        Assert.Equal(2, Metric(metrics, "RescheduleEvents").Value);
        Assert.Equal(1, Metric(metrics, "RescheduledReservations").Value);
        Assert.NotEqual(Metric(metrics, "RescheduleEvents").Value, Metric(metrics, "RescheduledReservations").Value);
    }
    [Fact]
    public void Missing_calls_are_excluded_from_wait_averages_and_operational_duration_uses_inprogress()
    {
        var checkin = AsOf.AddHours(-2);
        var first = TicketFact(checkin, null, TicketSource.WalkIn,
            [new(TicketHistoryEventType.Called, checkin.AddMinutes(10), TicketStatus.Called, null),
             new(TicketHistoryEventType.Recalled, checkin.AddMinutes(12), TicketStatus.Called, null),
             new(TicketHistoryEventType.InProgress, checkin.AddMinutes(15), TicketStatus.InProgress, null),
             new(TicketHistoryEventType.Completed, checkin.AddMinutes(35), TicketStatus.Completed, null)]) with { Status = TicketStatus.Completed };
        var missing = TicketFact(checkin, null, TicketSource.WalkIn, []);
        var metrics = Calculator.Calculate([], [first, missing], Window);
        Assert.Equal(10, Metric(metrics, "WaitToFirstCall").Value);
        Assert.Equal(1, Metric(metrics, "WaitToFirstCall").ExcludedSampleCount);
        Assert.Equal(3, Metric(metrics, "CallToStart").Value);
        Assert.Equal(20, Metric(metrics, "ServiceDuration").Value);
        Assert.Equal(2, Metric(metrics, "CallAttempts").Value);
        Assert.Equal(1, Metric(metrics, "TicketsCompleted").Value);
    }
    [Fact]
    public void Ratios_recompute_from_true_denominators_and_missing_history_is_not_zero()
    {
        var ratio = Calculator.Ratio("ReservationToTicketConversion", 1, 10);
        Assert.Equal(10, ratio.Value);
        Assert.Null(Calculator.Ratio("ReservationToTicketConversion", 0, 0).Value);
        Assert.Null(Calculator.Missing("CapacityMinutes", ReportDataStatus.NotMeasured, "missing").Value);
    }
    private static ReportMetric Metric(IReadOnlyList<ReportMetric> metrics, string code) => metrics.Single(m => m.MetricCode == code);
    private static ReservationReportingFact ReservationFact(DateTime created, DateTime scheduled) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), "NewConsultation", ReservationBookingSource.Patient, Guid.NewGuid(), created,
        DateOnly.FromDateTime(created), DateOnly.FromDateTime(scheduled), scheduled, scheduled, 15, ReservationStatus.Active, "UTC", [], 20);
    private static TicketReportingFact TicketFact(DateTime checkin, Guid? reservationId, TicketSource source, IReadOnlyList<TicketReportingEvent> events)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reservationId, Guid.NewGuid(), Guid.NewGuid(), "NewConsultation", 0,
            source, DateOnly.FromDateTime(checkin), checkin, TicketStatus.Waiting, "UTC", 1, events);
}
