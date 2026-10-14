using System.Globalization;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private static bool SupportsDrilldown(ReportScope scope, string metric) => scope.Actor == ReportActor.SuperAdmin
        ? metric is "RegisteredDoctors" or "PendingDoctors" or "ApprovedDoctors" or "RejectedDoctors" or "SuspendedDoctors" or "DoctorAcquisition"
        : OperationalDrilldownMetrics.Contains(metric) || FinancialDrilldownMetrics.Contains(metric) ||
            metric is "CapacityMinutes" or "UnusedCapacityMinutes" or "MaximumDailyPatientCapacity" or "BookedUtilization" or "ActualServiceUtilization" or "SegmentQuotaUtilization" ||
            scope.Actor == ReportActor.Doctor && scope.Permissions.Contains(Wasla.Domain.Security.PermissionNames.MedicalEncountersViewOwn) && metric == "ClinicalWorkflowCompleted" || scope.Actor == ReportActor.Doctor &&
            metric is "UniqueDoctorPatients" or "NewToDoctorPatients" or "ReturningDoctorPatients" or "PatientRetention90d";
    private static readonly HashSet<string> FinancialDrilldownMetrics = ["GrossCollected", "Refunds", "NetCollected", "PaidOperatingExpenses",
        "OutstandingDueInPeriod", "AllOutstandingAsOf", "OverdueAsOf", "NetOperatingCashResult", "ProjectedResultForPeriod"];
    private static ReportRecord[] FinancialDrilldown(FinancialSources source, ReportScope scope, ReportingWindow w, string metric)
    {
        var records = FinancialRecords(source, scope, w with { FromDate = DateOnly.MinValue, ToDate = DateOnly.MaxValue });
        var outstanding = source.Obligations.Where(o => o.Unsettled && o.Status != Wasla.Domain.OperatingFinance.OperatingObligationStatus.Cancelled);
        var due = outstanding.Where(o => OperationalKpiCalculator.InWindow(o.DueDate, w)).Select(o => o.Id).ToHashSet();
        var open = outstanding.Select(o => o.Id).ToHashSet();
        var overdue = outstanding.Where(o => o.DueDate < OperationalKpiCalculator.LocalDate(w.AsOfUtc, w.ReportTimeZoneId)).Select(o => o.Id).ToHashSet();
        bool Include(ReportRecord r) => metric switch
        {
            "GrossCollected" => r.ResourceType == "Payment", "Refunds" => r.ResourceType == "Refund",
            "NetCollected" => r.ResourceType is "Payment" or "Refund", "PaidOperatingExpenses" => r.ResourceType is "OperatingSettlement" or "OperatingReversal",
            "AllOutstandingAsOf" => r.ResourceType == "OperatingObligation" && open.Contains(r.Id),
            "OutstandingDueInPeriod" => r.ResourceType == "OperatingObligation" && due.Contains(r.Id),
            "OverdueAsOf" => r.ResourceType == "OperatingObligation" && overdue.Contains(r.Id),
            "NetOperatingCashResult" => r.ResourceType != "OperatingObligation", _ => r.ResourceType != "OperatingObligation" || due.Contains(r.Id)
        };
        return records.Where(Include).Select(r => r with { SourceRecordId = r.Id, MetricValue = r.Amount *
            (metric == "Refunds" || metric is "NetOperatingCashResult" or "ProjectedResultForPeriod" && r.ResourceType is not ("Payment" or "Refund") ? -1 : 1) }).ToArray();
    }
    private async Task<ReportRecord[]> PlatformDrilldownAsync(ReportScope scope, string code, ReportingWindow w, string metric, CancellationToken ct)
    {
        var records = await PlatformRecordsAsync(scope, code, w, ct);
        return records.Where(r => metric switch
        {
            "RegisteredDoctors" => true, "DoctorAcquisition" => OperationalKpiCalculator.InWindow(r.BusinessDate, w),
            "PendingDoctors" => r.Status == "Pending", "ApprovedDoctors" => r.Status == "Approved",
            "RejectedDoctors" => r.Status == "Rejected", _ => r.Status == "Suspended"
        }).Select(r => r with { MetricValue = 1, SourceRecordId = r.Id }).ToArray();
    }
    private static readonly HashSet<string> OperationalDrilldownMetrics =
    ["BookingsCreated", "AppointmentsScheduled", "CancellationsByEvent", "CancellationRateScheduledCohort", "ReservationNoShowRate", "TicketNoShowRate",
        "RescheduleEvents", "RescheduledReservations", "ReservationToTicketConversion", "BookingLeadTimeMinutes", "PunctualArrival",
        "CheckIns", "WalkIns", "WalkInShare", "Waiting", "Called", "InProgress", "TicketsCompleted", "TicketsCancelled", "TicketsNoShow",
        "WaitToFirstCall", "WaitToServiceStart", "CallToStart", "ServiceDuration", "CallAttempts", "Recalls", "OperationalExceptions", "FollowUpActivity", "ReceptionActivity"];
    private async Task<ReportRecord[]> OperationalDrilldownAsync(ReportScope scope, string metric, OperationalSources source, ReportFilter filter, ReportingWindow w, CancellationToken ct)
    {
        var original = (await RecordsAsync(scope, "overview", source, filter, w with { FromDate = DateOnly.MinValue, ToDate = DateOnly.MaxValue }, ct))
            .ToDictionary(r => (r.ResourceType, r.Id));
        List<ReportRecord> rows = [];
        if (metric == "ReceptionActivity") return await ReceptionActivityRowsAsync(scope, source, original, w, ct);
        if (metric is "CapacityMinutes" or "UnusedCapacityMinutes" or "MaximumDailyPatientCapacity" or "BookedUtilization" or "ActualServiceUtilization" or "SegmentQuotaUtilization")
            return await CapacityDrilldownAsync(scope, source, filter, w, metric, ct);
        if (metric == "ClinicalWorkflowCompleted") return await ClinicalDrilldownAsync(scope, w, ct);
        if (metric is "UniqueDoctorPatients" or "NewToDoctorPatients" or "ReturningDoctorPatients" or "PatientRetention90d")
            return await PatientDrilldownAsync(scope, source, original, w, metric, ct);
        void Reservation(ReservationReportingFact r, DateOnly basis, decimal? value, decimal? denominator = null,
            ReservationReportingEvent? e = null, string? excluded = null)
        {
            var row = original[("Reservation", r.Id)];
            rows.Add(row with { Id = e is { Id: var eventId } && eventId != Guid.Empty ? eventId : r.Id, SourceRecordId = r.Id,
                ResourceType = e == null ? "Reservation" : "ReservationEvent", BusinessDate = basis,
                RecordedAtUtc = e?.RecordedAtUtc ?? r.CreatedAtUtc, MetricValue = value, MetricDenominator = denominator, ExclusionReasonCode = excluded });
        }
        void Ticket(TicketReportingFact t, DateOnly basis, decimal? value, decimal? denominator = null, TicketReportingEvent? e = null, string? excluded = null)
        {
            var row = original[("Ticket", t.Id)];
            rows.Add(row with { Id = e is { Id: var eventId } && eventId != Guid.Empty ? eventId : t.Id, SourceRecordId = t.Id,
                ResourceType = e == null ? "Ticket" : "TicketEvent", BusinessDate = basis, RecordedAtUtc = e?.RecordedAtUtc ?? t.CheckInAtUtc,
                MetricValue = value, MetricDenominator = denominator, ExclusionReasonCode = excluded });
        }
        foreach (var r in source.Reservations)
        {
            var scheduled = OperationalKpiCalculator.InWindow(r.ScheduledBusinessDate, w);
            var created = OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w);
            var matured = scheduled && r.Status != null && r.ScheduledStartUtc <= w.AsOfUtc &&
                !r.Events.Any(e => e.EventType == ReservationHistoryEventType.Cancelled && e.RecordedAtUtc < r.ScheduledStartUtc);
            switch (metric)
            {
                case "BookingsCreated" when created: Reservation(r, r.CreatedBusinessDate, 1); break;
                case "AppointmentsScheduled" when scheduled: Reservation(r, r.ScheduledBusinessDate, 1); break;
                case "CancellationRateScheduledCohort" when scheduled:
                    Reservation(r, r.ScheduledBusinessDate, r.Status == null ? null : r.Status == ReservationStatus.Cancelled ? 1 : 0,
                        r.Status == null ? null : 1, excluded: r.Status == null ? "Report.UnknownHistoricalState" : null); break;
                case "ReservationNoShowRate" when matured:
                    if (r.GraceMinutes is { } grace && r.ScheduledStartUtc.AddMinutes(grace) <= w.AsOfUtc)
                        Reservation(r, r.ScheduledBusinessDate, r.Status == ReservationStatus.NoShow ? 1 : 0, 1);
                    else if (r.GraceMinutes == null) Reservation(r, r.ScheduledBusinessDate, null, excluded: "Report.HistoricalGraceNotRecorded");
                    break;
                case "ReservationToTicketConversion" when matured:
                    Reservation(r, r.ScheduledBusinessDate, source.Tickets.Any(t => t.ReservationId == r.Id && t.CheckInAtUtc <= w.AsOfUtc) ? 1 : 0, 1); break;
                case "BookingLeadTimeMinutes" when created:
                    Reservation(r, r.CreatedBusinessDate, r.OriginalStartUtc >= r.CreatedAtUtc ? (decimal?)(r.OriginalStartUtc.Value - r.CreatedAtUtc).TotalMinutes : null,
                        r.OriginalStartUtc >= r.CreatedAtUtc ? 1 : null, excluded: r.OriginalStartUtc >= r.CreatedAtUtc ? null : "Report.OriginalScheduleNotRecorded"); break;
                case "CancellationsByEvent": case "RescheduleEvents": case "RescheduledReservations":
                    var type = metric == "CancellationsByEvent" ? ReservationHistoryEventType.Cancelled : ReservationHistoryEventType.Rescheduled;
                    var events = r.Events.Where(e => e.EventType == type && OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), w)).ToArray();
                    if (metric == "RescheduledReservations" && events.Length > 0) Reservation(r, OperationalKpiCalculator.LocalDate(events[0].RecordedAtUtc, r.TimeZoneId), 1);
                    else foreach (var e in events) Reservation(r, OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), 1, e: e);
                    break;
            }
        }
        foreach (var t in source.Tickets)
        {
            var attended = OperationalKpiCalculator.InWindow(t.BusinessDate, w);
            var events = t.Events.OrderBy(e => e.RecordedAtUtc).ThenBy(e => e.EventType).ToArray();
            if (metric is "TicketsCompleted" or "ServiceDuration" or "CallAttempts" or "Recalls" or "OperationalExceptions")
            {
                foreach (var e in events.Where(e => OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), w)))
                {
                    var included = metric switch
                    { "TicketsCompleted" or "ServiceDuration" => e.EventType == TicketHistoryEventType.Completed,
                        "CallAttempts" => OperationalKpiCalculator.IsCall(e.EventType), "Recalls" => e.EventType == TicketHistoryEventType.Recalled,
                        _ => e.EventType is TicketHistoryEventType.ForceCheckIn or TicketHistoryEventType.ManualCalled or TicketHistoryEventType.CallNoResponse or TicketHistoryEventType.OperationalDayEnded };
                    if (!included) continue;
                    var start = metric == "ServiceDuration" ? events.LastOrDefault(s => s.EventType == TicketHistoryEventType.InProgress && s.RecordedAtUtc <= e.RecordedAtUtc) : null;
                    Ticket(t, OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), metric != "ServiceDuration" ? 1 :
                        start == null ? null : (decimal)(e.RecordedAtUtc - start.RecordedAtUtc).TotalMinutes,
                        metric == "ServiceDuration" && start != null ? 1 : null, e, metric == "ServiceDuration" && start == null ? "Report.ServiceStartNotRecorded" : null);
                }
                continue;
            }
            if (!attended) continue;
            switch (metric)
            {
                case "CheckIns": Ticket(t, t.BusinessDate, 1); break;
                case "FollowUpActivity" when t.VisitTypeCode == "FollowUp": Ticket(t, t.BusinessDate, 1); break;
                case "WalkIns" when t.Source == TicketSource.WalkIn: Ticket(t, t.BusinessDate, 1); break;
                case "WalkInShare": Ticket(t, t.BusinessDate, t.Source == TicketSource.WalkIn ? 1 : 0, 1); break;
                case "Waiting" when t.Status == TicketStatus.Waiting: case "Called" when t.Status == TicketStatus.Called:
                case "InProgress" when t.Status == TicketStatus.InProgress: case "TicketsCancelled" when t.Status == TicketStatus.Cancelled:
                case "TicketsNoShow" when t.Status == TicketStatus.NoShow: Ticket(t, t.BusinessDate, 1); break;
                case "TicketNoShowRate" when t.Status != null && (t.BusinessDate < OperationalKpiCalculator.LocalDate(w.AsOfUtc, t.TimeZoneId) ||
                    t.Status is TicketStatus.Completed or TicketStatus.Cancelled or TicketStatus.NoShow):
                    Ticket(t, t.BusinessDate, t.Status == TicketStatus.NoShow ? 1 : 0, 1); break;
                case "WaitToFirstCall": case "WaitToServiceStart": case "CallToStart":
                    var call = events.FirstOrDefault(e => OperationalKpiCalculator.IsCall(e.EventType));
                    var began = events.FirstOrDefault(e => e.EventType == TicketHistoryEventType.InProgress && e.RecordedAtUtc >= t.CheckInAtUtc);
                    decimal? value = null;
                    if (metric == "WaitToFirstCall" && call?.RecordedAtUtc >= t.CheckInAtUtc) value = (decimal)(call.RecordedAtUtc - t.CheckInAtUtc).TotalMinutes;
                    if (metric == "WaitToServiceStart" && began != null) value = (decimal)(began.RecordedAtUtc - t.CheckInAtUtc).TotalMinutes;
                    if (metric == "CallToStart" && began != null)
                    {
                        var cycle = events.LastOrDefault(e => e.EventType == TicketHistoryEventType.RestoredFromNoShow && e.RecordedAtUtc <= began.RecordedAtUtc)?.RecordedAtUtc ?? t.CheckInAtUtc;
                        var preceding = events.LastOrDefault(e => OperationalKpiCalculator.IsCall(e.EventType) && e.RecordedAtUtc >= cycle && e.RecordedAtUtc <= began.RecordedAtUtc);
                        if (preceding != null) value = (decimal)(began.RecordedAtUtc - preceding.RecordedAtUtc).TotalMinutes;
                    }
                    Ticket(t, t.BusinessDate, value, value != null ? 1 : null, excluded: value == null ? "Report.RequiredLifecycleEventNotRecorded" : null); break;
                case "PunctualArrival" when t.ReservationId != null:
                    var reservation = source.Reservations.SingleOrDefault(r => r.Id == t.ReservationId);
                    var scheduled = reservation?.Events.Where(e => e.EventType == ReservationHistoryEventType.Rescheduled && e.RecordedAtUtc <= t.CheckInAtUtc)
                        .OrderBy(e => e.RecordedAtUtc).LastOrDefault()?.NewStartUtc ?? reservation?.OriginalStartUtc;
                    Ticket(t, t.BusinessDate, scheduled == null ? null : t.CheckInAtUtc <= scheduled.Value.AddMinutes(5) ? 1 : 0,
                        scheduled == null ? null : 1, excluded: scheduled == null ? "Report.ScheduleAtCheckInNotRecorded" : null); break;
            }
        }
        return rows.ToArray();
    }
}
