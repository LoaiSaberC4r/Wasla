using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private sealed record OperationalSources(IReadOnlyList<ReservationReportingFact> Reservations, IReadOnlyList<TicketReportingFact> Tickets, long ExcludedSourceCount = 0);

    private async Task<OperationalSources> OperationalSourcesAsync(ReportScope scope, ReportFilter f, ReportingWindow w, CancellationToken ct)
    {
        var lower = DateOnly.FromDayNumber(Math.Max(0, w.FromDate.DayNumber - 2)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var upper = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, w.ToDate.DayNumber + 2)).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var reservations = await db.Reservations.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.CreatedOnUtc <= w.AsOfUtc &&
            (f.SegmentId == null || r.SegmentId == f.SegmentId) && (f.VisitTypeId == null || r.VisitTypeId == f.VisitTypeId) &&
            (f.BookingSource == null || r.BookingSource == f.BookingSource) &&
            (r.CreatedOnUtc >= lower && r.CreatedOnUtc <= upper || r.BusinessDate >= w.FromDate && r.BusinessDate <= w.ToDate ||
                r.History.Any(h => h.OccurredOnUtc <= w.AsOfUtc && (h.OccurredOnUtc >= lower && h.OccurredOnUtc <= upper ||
                    h.NewBusinessDate >= w.FromDate && h.NewBusinessDate <= w.ToDate) || h.OldBusinessDate >= w.FromDate && h.OldBusinessDate <= w.ToDate)))
            .Include(r => r.History).AsSplitQuery().ToArrayAsync(ct);
        var tickets = await db.Tickets.AsNoTracking().Where(t => scope.PracticeIds.Contains(t.DoctorPracticeId) && t.CheckInTimeUtc <= w.AsOfUtc &&
            (f.SegmentId == null || t.SegmentId == f.SegmentId) && (f.VisitTypeId == null || t.VisitTypeId == f.VisitTypeId) &&
            (f.BookingSource == null || t.ReservationId != null && db.Reservations.Any(r => r.Id == t.ReservationId && r.BookingSource == f.BookingSource)) &&
            (t.BusinessDate >= w.FromDate && t.BusinessDate <= w.ToDate || t.History.Any(h => h.OccurredOnUtc <= w.AsOfUtc && h.OccurredOnUtc >= lower && h.OccurredOnUtc <= upper) ||
                t.ReservationId != null && reservations.Select(r => r.Id).Contains(t.ReservationId.Value)))
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
                    h.OldScheduledStartUtc, h.NewBusinessDate, h.NewScheduledStartUtc, h.ActionInitiator, h.PerformedByApplicationUserId, h.ReasonCode, h.Reason, h.Id)).ToArray(), r.SlotDurationMinutesSnapshot);
        }).OfType<ReservationReportingFact>().Where(r => f.ReservationStatus == null || r.Status == f.ReservationStatus).ToArray();
        var ticketFacts = tickets.Select(t => new TicketReportingFact(t.Id, t.DoctorId, t.DoctorPracticeId, t.PatientId,
            t.ReservationId, t.SegmentId, t.VisitTypeId, t.VisitTypeCodeSnapshot, t.SegmentPrioritySnapshot, t.Source,
            t.BusinessDate, t.CheckInTimeUtc, t.LastUpdatedOnUtc <= w.AsOfUtc ? t.Status :
                t.History.OrderBy(h => h.OccurredOnUtc).ThenBy(h => h.EventType).LastOrDefault(h => h.ToStatus != null)?.ToStatus,
            t.PracticeTimeZoneIdSnapshot, t.TicketNumber,
            t.History.Select(h => new TicketReportingEvent(h.EventType, h.OccurredOnUtc, h.ToStatus, h.ActorApplicationUserId, h.Id)).ToArray()))
            .Where(t => f.TicketStatus == null || t.Status == f.TicketStatus).ToArray();
        return new(reservationFacts, ticketFacts, reservations.Length - reservationFacts.Length +
            reservationFacts.LongCount(r => r.Status == null) + ticketFacts.LongCount(t => t.Status == null));
    }
    private ReportSeriesPoint[] BuildSeries(OperationalSources source, ReportGrouping grouping, ReportingWindow w)
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
    private async Task<IReadOnlyList<ReportSeriesPoint>> BuildBreakdownsAsync(ReportScope scope, OperationalSources source, ReportingWindow w,
        ReportGrouping grouping, FinancialSources? finance, bool financialOnly, string code,
        Wasla.Domain.Reporting.PracticeReportingPolicySnapshot[] policies, ReportFilter filter, CancellationToken ct)
    {
        var names = await db.DoctorPractices.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameAr, p.NameEn }).ToArrayAsync(ct);
        var patientMetrics = scope.Actor == ReportActor.Doctor && code == "practice-comparison" ? await PracticePatientMetricsAsync(scope, source, w, ct) : null;
        var result = names.Select(p =>
        {
            var operations = calculator.Calculate(source.Reservations.Where(r => r.PracticeId == p.Id).ToArray(), source.Tickets.Where(t => t.PracticeId == p.Id).ToArray(), w);
            var capacity = CapacityMetrics(policies, scope with { PracticeIds = [p.Id] }, source, w, filter).ToDictionary(m => m.MetricCode);
            return new ReportSeriesPoint(p.Id.ToString(), p.NameAr, p.NameEn,
                (financialOnly ? [] : operations.Select(m => capacity.GetValueOrDefault(m.MetricCode) ?? m))
                    .Concat(patientMetrics?.GetValueOrDefault(p.Id) ?? [])
                    .Concat(finance is null ? [] : FinancialMetrics(FinancialPractice(finance, p.Id), w, financialOnly ? code : "operating-finance")).ToArray());
        }).ToList();
        if (!financialOnly && grouping is ReportGrouping.Segment or ReportGrouping.VisitType)
        {
            var keys = grouping == ReportGrouping.Segment ? source.Reservations.Select(r => r.SegmentId).Concat(source.Tickets.Select(t => t.SegmentId)).Distinct().ToArray()
                : source.Reservations.Select(r => r.VisitTypeId).Concat(source.Tickets.Select(t => t.VisitTypeId)).Distinct().ToArray();
            var labels = grouping == ReportGrouping.Segment
                ? await db.DoctorPracticeSegments.AsNoTracking().Where(s => keys.Contains(s.Id)).Select(s => new { s.Id, s.NameAr, s.NameEn }).ToArrayAsync(ct)
                : await db.DoctorPracticeVisitTypes.AsNoTracking().Where(t => keys.Contains(t.Id)).Select(t => new { t.Id, t.NameAr, t.NameEn }).ToArrayAsync(ct);
            result = labels.Select(l => new ReportSeriesPoint(l.Id.ToString(), l.NameAr, l.NameEn, calculator.Calculate(
                source.Reservations.Where(r => (grouping == ReportGrouping.Segment ? r.SegmentId : r.VisitTypeId) == l.Id).ToArray(),
                source.Tickets.Where(t => (grouping == ReportGrouping.Segment ? t.SegmentId : t.VisitTypeId) == l.Id).ToArray(), w))).ToList();
        }
        if (!financialOnly && grouping == ReportGrouping.Source)
            result = Enum.GetValues<ReservationBookingSource>().Select(s => new ReportSeriesPoint("source:" + s, s switch
            { ReservationBookingSource.Patient => "المريض", ReservationBookingSource.FamilyMember => "أحد أفراد الأسرة", _ => "الاستقبال" }, s.ToString(),
                calculator.Calculate(source.Reservations.Where(r => r.BookingSource == s).ToArray(), source.Tickets.Where(t =>
                    source.Reservations.Any(r => r.Id == t.ReservationId && r.BookingSource == s)).ToArray(), w))).ToList();
        if (!financialOnly && grouping == ReportGrouping.Status)
            result = source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.ScheduledBusinessDate, w)).GroupBy(r => r.Status)
                .Select(g => new ReportSeriesPoint("reservation-status:" + (g.Key?.ToString() ?? "Unknown"), g.Key?.ToString() ?? "غير معلوم", g.Key?.ToString() ?? "Unknown",
                    [calculator.Count("ReservationStatusDistribution", g.Count())])).ToList();
        if (!financialOnly && code is "reservations" or "overview")
        {
            foreach (var day in source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w)).GroupBy(r => r.CreatedBusinessDate.DayOfWeek).OrderBy(g => g.Key))
                result.Add(new("BookingWeekday:" + day.Key, "يوم إنشاء الحجز: " + day.Key, "Booking creation weekday: " + day.Key, [calculator.Count("BookingsCreated", day.Count(), day.Count())]));
            foreach (var hour in source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w))
                .GroupBy(r => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId)).Hour).OrderBy(g => g.Key))
                result.Add(new("BookingHour:" + hour.Key, $"ساعة إنشاء الحجز {hour.Key:00}:00", $"Booking creation hour {hour.Key:00}:00", [calculator.Count("BookingsCreated", hour.Count(), hour.Count())]));
            foreach (var reason in source.Reservations.SelectMany(r => r.Events.Where(e => e.EventType == ReservationHistoryEventType.Cancelled &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), w))).GroupBy(e => e.ReasonCode ?? "NotRecorded"))
                result.Add(new("CancellationReason:" + reason.Key, "سبب الإلغاء: " + reason.Key, "Cancellation reason: " + reason.Key, [calculator.Count("CancellationsByEvent", reason.Count(), reason.Count())]));
            foreach (var initiator in source.Reservations.SelectMany(r => r.Events.Where(e => e.EventType == ReservationHistoryEventType.Cancelled &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), w))).GroupBy(e => e.Initiator))
                result.Add(new("CancellationInitiator:" + initiator.Key, "منفذ الإلغاء: " + initiator.Key, "Cancellation initiator: " + initiator.Key, [calculator.Count("CancellationsByEvent", initiator.Count(), initiator.Count())]));
            var receptionActors = await db.Receptions.AsNoTracking().Where(r => r.OwnerDoctorId == scope.DoctorId).Select(r => r.ApplicationUserId).ToArrayAsync(ct);
            var doctorActor = await db.Doctors.AsNoTracking().Where(d => d.Id == scope.DoctorId).Select(d => (Guid?)d.ApplicationUserId).SingleOrDefaultAsync(ct);
            foreach (var creator in source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w))
                .GroupBy(r => r.CreatedBy == doctorActor ? "Doctor" : receptionActors.Contains(r.CreatedBy) ? "Reception" : "OtherRecordedActor"))
                result.Add(new("BookingCreator:" + creator.Key, "منشئ الحجز: " + creator.Key, "Booking creator: " + creator.Key, [calculator.Count("BookingsCreated", creator.Count(), creator.Count())]));
            var created = source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, w)).ToArray();
            var ids = created.Select(r => r.Id).ToHashSet();
            var cohortTickets = source.Tickets.Where(t => t.ReservationId is { } id && ids.Contains(id)).ToArray();
            result.Add(new("BookingCreationCohortFunnel", "مسار دفعة إنشاء الحجز كما في تاريخ التقرير", "Booking creation cohort funnel at AsOfUtc",
                [calculator.Count("BookingsCreated", created.Length), calculator.Count("CheckIns", cohortTickets.Select(t => t.ReservationId).Distinct().Count()),
                    calculator.Count("TicketsCompleted", cohortTickets.Count(t => t.Status == TicketStatus.Completed))]));
        }
        if (!financialOnly && code is "queue" or "overview" or "activity")
        {
            foreach (var priority in source.Tickets.GroupBy(t => t.Priority))
                result.Add(new("SegmentPriority:" + priority.Key, "أولوية الشريحة: " + priority.Key, "Segment priority: " + priority.Key,
                    calculator.Calculate([], priority.ToArray(), w).Where(m => m.MetricCode is "CheckIns" or "WaitToFirstCall" or "WaitToServiceStart" or "ServiceDuration").ToArray()));
            foreach (var age in source.Tickets.Where(t => t.Status == TicketStatus.Waiting && OperationalKpiCalculator.InWindow(t.BusinessDate, w))
                .GroupBy(t => (w.AsOfUtc - t.CheckInAtUtc).TotalMinutes < 15 ? "0-15" : (w.AsOfUtc - t.CheckInAtUtc).TotalMinutes < 30 ? "15-30" : (w.AsOfUtc - t.CheckInAtUtc).TotalMinutes < 60 ? "30-60" : "60+"))
                result.Add(new("WaitingAgeMinutes:" + age.Key, "عمر الانتظار بالدقائق " + age.Key, "Waiting age in minutes " + age.Key, [calculator.Count("Waiting", age.Count(), age.Count())]));
        }
        return result;
    }
    private async Task<IReadOnlyList<ReportRecord>> RecordsAsync(ReportScope scope, string code, OperationalSources source,
        ReportFilter f, ReportingWindow w, CancellationToken ct)
    {
        if (scope.Actor == ReportActor.SuperAdmin) return await PlatformRecordsAsync(scope, code, w, ct);
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
        if (!s.Permissions.Contains(permission)) return Task.FromResult<Guid[]>([]);
        var id = SystemPermissionIds.For(permission);
        return (from a in db.ReceptionPracticeAssignments.AsNoTracking()
            join p in db.ReceptionPracticeAssignmentPermissions.AsNoTracking() on a.Id equals p.AssignmentId
            join r in db.Receptions.AsNoTracking() on a.ReceptionId equals r.Id
            where r.ApplicationUserId == s.ActorId && a.IsActive && p.PermissionId == id && s.PracticeIds.Contains(a.DoctorPracticeId)
            select a.DoctorPracticeId).Distinct().ToArrayAsync(ct);
    }
}
