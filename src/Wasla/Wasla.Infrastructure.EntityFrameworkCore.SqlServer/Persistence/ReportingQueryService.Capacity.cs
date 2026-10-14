using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Practices;
using Wasla.Domain.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private ReportMetric[] CapacityMetrics(PracticeReportingPolicySnapshot[] snapshots, ReportScope scope, OperationalSources source,
        ReportingWindow w, ReportFilter filter)
    {
        decimal availableMinutes = 0, bookedMinutes = 0, serviceMinutes = 0;
        decimal quota = 0, consumed = 0;
        decimal patientLimit = 0; var patientLimitMissing = false;
        var missing = 0; var days = 0; var forecast = false; var quotaMissing = false;
        foreach (var practiceId in scope.PracticeIds)
        {
            List<(DateTime Start, DateTime End)> available = [], booked = [], serviced = [];
            var history = snapshots.Where(s => s.PracticeId == practiceId).GroupBy(s => s.RecordedAtUtc)
                .Select(g => g.MaxBy(s => s.RevisionNumber)!).OrderBy(s => s.RecordedAtUtc).ToArray();
            if (history.Length == 0) { missing += w.ToDate.DayNumber - w.FromDate.DayNumber + 1; days += w.ToDate.DayNumber - w.FromDate.DayNumber + 1; continue; }
            var policies = history.Select(s => JsonSerializer.Deserialize<PracticeReportingPolicy>(s.PolicyJson)
                ?? throw new InvalidOperationException("Reporting policy snapshot is invalid.")).ToArray();
            var latest = policies[^1]; var today = OperationalKpiCalculator.LocalDate(w.AsOfUtc, latest.TimeZoneId);
            for (var dayNumber = w.FromDate.DayNumber; dayNumber <= w.ToDate.DayNumber; ++dayNumber)
            {
                var date = DateOnly.FromDayNumber(dayNumber); ++days;
                var sections = new List<(PracticeReportingPolicy Policy, DateTime From, DateTime Before)>();
                if (date >= today) { sections.Add((latest, DateTime.MinValue, DateTime.MaxValue)); forecast = true; }
                else
                {
                    var boundary = ReportingTimeScope.DateBoundaryUtc(date, TimeZoneInfo.FindSystemTimeZoneById(latest.TimeZoneId));
                    if (history[0].RecordedAtUtc > boundary) { ++missing; continue; }
                    for (var i = 0; i < history.Length; ++i)
                        sections.Add((policies[i], history[i].RecordedAtUtc, i + 1 < history.Length ? history[i + 1].RecordedAtUtc : w.AsOfUtc));
                }
                foreach (var section in sections.Where(s => s.Policy.IsActive))
                {
                    var policy = section.Policy;
                    var periods = policy.Periods.Select(p => DoctorPracticeSchedulePeriod.Create(p.Id, policy.PracticeId,
                        p.DayOfWeek, p.StartTime, p.EndTime, p.SlotDurationMinutes, policy.OwnerUserId).Value).ToArray();
                    var exceptions = policy.Exceptions.Select(e => DoctorPracticeScheduleException.Create(e.Id, policy.PracticeId,
                        e.Date, e.Type, e.StartTime, e.EndTime, e.SlotDurationMinutes, policy.OwnerUserId).Value).ToArray();
                    var working = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(date, periods, exceptions);
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
                    foreach (var period in working)
                    {
                        var localStart = date.ToDateTime(period.StartTime); var localEnd = date.ToDateTime(period.EndTime);
                        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd)) { ++missing; continue; }
                        var start = TimeZoneInfo.ConvertTimeToUtc(localStart, zone); var end = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
                        start = start < section.From ? section.From : start; end = end > section.Before ? section.Before : end;
                        if (end <= start) continue;
                        available.Add((start, end));
                        foreach (var reservation in source.Reservations.Where(r => r.PracticeId == practiceId && r.ScheduledBusinessDate == date &&
                            r.DurationMinutes > 0 && !r.Events.Any(e => e.EventType == ReservationHistoryEventType.Cancelled && e.RecordedAtUtc < r.ScheduledStartUtc)))
                            AddIntersection(booked, reservation.ScheduledStartUtc, reservation.ScheduledStartUtc.AddMinutes(reservation.DurationMinutes), start, end);
                        foreach (var ticket in source.Tickets.Where(t => t.PracticeId == practiceId))
                        foreach (var completed in ticket.Events.Where(e => e.EventType == TicketHistoryEventType.Completed))
                        {
                            var began = ticket.Events.Where(e => e.EventType == TicketHistoryEventType.InProgress && e.RecordedAtUtc <= completed.RecordedAtUtc)
                                .OrderBy(e => e.RecordedAtUtc).LastOrDefault();
                            if (began != null) AddIntersection(serviced, began.RecordedAtUtc, completed.RecordedAtUtc, start, end);
                        }
                    }
                }
                // Quota is a daily policy. Intraday quota changes have no trustworthy consumption
                // snapshot and remain explicitly unmeasured rather than being inferred.
                var dayPolicy = date >= today ? latest : policies[Array.FindLastIndex(history, s => s.RecordedAtUtc <=
                    ReportingTimeScope.DateBoundaryUtc(date, TimeZoneInfo.FindSystemTimeZoneById(latest.TimeZoneId)))];
                if (date < today)
                {
                    var dayStart = ReportingTimeScope.DateBoundaryUtc(date, TimeZoneInfo.FindSystemTimeZoneById(latest.TimeZoneId));
                    var dayEnd = ReportingTimeScope.DateBoundaryUtc(date.AddDays(1), TimeZoneInfo.FindSystemTimeZoneById(latest.TimeZoneId));
                    if (history.Any(s => s.RecordedAtUtc > dayStart && s.RecordedAtUtc < dayEnd)) quotaMissing = true;
                }
                if (dayPolicy.IsActive)
                {
                    var periods = dayPolicy.Periods.Select(p => DoctorPracticeSchedulePeriod.Create(p.Id, dayPolicy.PracticeId,
                        p.DayOfWeek, p.StartTime, p.EndTime, p.SlotDurationMinutes, dayPolicy.OwnerUserId).Value).ToArray();
                    var exceptions = dayPolicy.Exceptions.Select(e => DoctorPracticeScheduleException.Create(e.Id, dayPolicy.PracticeId,
                        e.Date, e.Type, e.StartTime, e.EndTime, e.SlotDurationMinutes, dayPolicy.OwnerUserId).Value).ToArray();
                    if (DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(date, periods, exceptions).Count > 0)
                    {
                        if (dayPolicy.MaximumDailyPatients is { } limit) patientLimit += limit;
                        else patientLimitMissing = true;
                    }
                }
                if (dayPolicy.IsActive)
                foreach (var segment in dayPolicy.Segments.Where(s => s.IsActive && s.ReservedDailyQuota != null &&
                    (filter.SegmentId == null || s.Id == filter.SegmentId)))
                {
                    quota += segment.ReservedDailyQuota!.Value;
                    consumed += source.Reservations.Count(r => r.PracticeId == practiceId && r.SegmentId == segment.Id && r.ScheduledBusinessDate == date &&
                        r.Status is ReservationStatus.Active or ReservationStatus.ConvertedToTicket or ReservationStatus.NoShow);
                    consumed += source.Tickets.Count(t => t.PracticeId == practiceId && t.SegmentId == segment.Id && t.BusinessDate == date && t.Source == TicketSource.WalkIn);
                }
            }
            availableMinutes += ReportingTimeScope.UnionMinutes(available);
            bookedMinutes += ReportingTimeScope.UnionMinutes(booked);
            serviceMinutes += ReportingTimeScope.UnionMinutes(serviced);
        }
        if (missing > 0) return new[] { calculator.Missing("CapacityMinutes", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            calculator.Missing("UnusedCapacityMinutes", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            calculator.Missing("MaximumDailyPatientCapacity", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            calculator.Missing("BookedUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            calculator.Missing("ActualServiceUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalCapacityNotRecorded"),
            calculator.Missing("SegmentQuotaUtilization", ReportDataStatus.NotMeasured, "Report.HistoricalQuotaNotRecorded") }
            .Select(m => m with { ExcludedSampleCount = missing, EligibleSampleCount = Math.Max(0, days - missing) }).ToArray();
        var result = new[] { calculator.Count("CapacityMinutes", availableMinutes, days),
            calculator.Count("UnusedCapacityMinutes", Math.Max(0, availableMinutes - bookedMinutes), days),
            patientLimitMissing || quotaMissing ? calculator.Missing("MaximumDailyPatientCapacity", ReportDataStatus.NotMeasured, "Report.DailyPatientLimitNotMeasured")
                : calculator.Count("MaximumDailyPatientCapacity", patientLimit, days),
            calculator.Ratio("BookedUtilization", bookedMinutes, availableMinutes),
            calculator.Ratio("ActualServiceUtilization", serviceMinutes, availableMinutes),
            quotaMissing ? calculator.Missing("SegmentQuotaUtilization", ReportDataStatus.NotMeasured, "Report.IntradayQuotaConsumptionNotRecorded")
                : calculator.Ratio("SegmentQuotaUtilization", consumed, quota) };
        return result.Select(m => m with { EligibleSampleCount = days, WarningCode = forecast && m.Value != null ? "Report.CurrentCapacityForecastAsOfSnapshot" : m.WarningCode }).ToArray();
    }
    private static void AddIntersection(List<(DateTime Start, DateTime End)> target, DateTime start, DateTime end, DateTime availableStart, DateTime availableEnd)
    {
        var left = start < availableStart ? availableStart : start;
        var right = end > availableEnd ? availableEnd : end;
        if (right > left) target.Add((left, right));
    }
}
