using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Operations;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ReceptionOperationsService(WaslaDbContext db, IReportingAccessGuard guard,
    IDateTimeProvider clock, IKpiDefinitionRegistry registry) : IReceptionOperationsService
{
    public async Task<Result<ReportPage<WorklistItem>>> WorklistAsync(Guid practiceId, WorklistFilter f, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.Reception, Phase18PermissionNames.ReceptionDashboard, practiceId, null, ct);
        if (access.IsFailure) return Result<ReportPage<WorklistItem>>.Fail(access.Errors);
        if (f.PageNumber is < 1 or > 1_000_000 || f.PageSize is < 1 or > 100 || f.Category is { } category && !Enum.IsDefined(category) ||
            f.ReservationStatus is { } rs && !Enum.IsDefined(rs) || f.TicketStatus is { } ts && !Enum.IsDefined(ts))
            return Result<ReportPage<WorklistItem>>.Fail(ReportingErrors.InvalidFilter);
        var configuration = await db.DoctorPracticeConfigurations.AsNoTracking().SingleAsync(c => c.DoctorPracticeId == practiceId, ct);
        var practiceActive = await db.DoctorPractices.AsNoTracking().Where(p => p.Id == practiceId).Select(p => p.IsActive).SingleAsync(ct);
        var today = OperationalKpiCalculator.LocalDate(clock.UtcNow, configuration.TimeZoneId);
        var date = f.BusinessDate ?? today;
        if (date == DateOnly.MinValue || date == DateOnly.MaxValue) return Result<ReportPage<WorklistItem>>.Fail(ReportingErrors.InvalidFilter);
        var permissions = await (from a in db.ReceptionPracticeAssignments.AsNoTracking()
            join r in db.Receptions.AsNoTracking() on a.ReceptionId equals r.Id
            join delegated in db.ReceptionPracticeAssignmentPermissions.AsNoTracking() on a.Id equals delegated.AssignmentId
            join permission in db.Permissions.AsNoTracking() on delegated.PermissionId equals permission.Id
            where a.DoctorPracticeId == practiceId && a.IsActive && r.ApplicationUserId == access.Value.ActorId
            select permission.Name).ToArrayAsync(ct);
        bool Can(string code) => permissions.Contains(code) && access.Value.Permissions.Contains(code);
        if (f.Category == WorklistCategory.OpenHandover && !Can(Phase18PermissionNames.HandoverView))
            return Result<ReportPage<WorklistItem>>.Fail(ReportingErrors.Denied);
        var reservations = await (from r in db.Reservations.AsNoTracking()
            join p in db.Patients.AsNoTracking() on r.PatientId equals p.Id
            where r.DoctorPracticeId == practiceId && r.BusinessDate == date &&
                (f.SegmentId == null || r.SegmentId == f.SegmentId) && (f.VisitTypeId == null || r.VisitTypeId == f.VisitTypeId) &&
                (f.ReservationStatus == null || r.Status == f.ReservationStatus)
            select new { r.Id, r.PatientId, r.ReservationReference, r.Status, r.ScheduledStartUtc, p.NameAr, p.NameEn,
                HasOpenTicket = db.Tickets.Any(t => t.PatientId == r.PatientId && t.DoctorPracticeId == practiceId &&
                    (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Called || t.Status == TicketStatus.InProgress)) }).ToArrayAsync(ct);
        var tickets = await (from t in db.Tickets.AsNoTracking()
            join p in db.Patients.AsNoTracking() on t.PatientId equals p.Id
            where t.DoctorPracticeId == practiceId && t.BusinessDate == date &&
                (f.SegmentId == null || t.SegmentId == f.SegmentId) && (f.VisitTypeId == null || t.VisitTypeId == f.VisitTypeId) &&
                (f.TicketStatus == null || t.Status == f.TicketStatus)
            select new { t.Id, t.PatientId, t.TicketNumber, t.Status, t.CheckInTimeUtc, p.NameAr, p.NameEn,
                HasRefund = db.Refunds.Any(r => r.TicketId == t.Id) }).ToArrayAsync(ct);
        var handovers = Can(Phase18PermissionNames.HandoverView) ? await db.Set<ReceptionHandover>().AsNoTracking()
            .Where(h => h.DoctorPracticeId == practiceId && h.Status != HandoverStatus.Closed)
            .Select(h => new { h.Id, h.CreatedOnUtc, h.Status }).ToArrayAsync(ct) : [];
        List<WorklistItem> rows = [];
        foreach (var r in reservations.Where(r => r.Status is ReservationStatus.Active or ReservationStatus.NoShow))
        {
            var c = r.Status == ReservationStatus.NoShow ? WorklistCategory.ReservationNoShow
                : clock.UtcNow > r.ScheduledStartUtc.AddMinutes(configuration.CheckInGracePeriodMinutes) ? WorklistCategory.LateArrival
                : r.ScheduledStartUtc > clock.UtcNow ? WorklistCategory.UpcomingBooking : WorklistCategory.AwaitingCheckIn;
            var canView = Can(PermissionNames.PracticeReservationsView);
            var checkIn = practiceActive && !r.HasOpenTicket && r.Status == ReservationStatus.Active && date == today &&
                clock.UtcNow >= r.ScheduledStartUtc.AddMinutes(-configuration.CheckInOpenBeforeMinutes) && Can(PermissionNames.PracticeTicketsCheckIn);
            var route = $"/api/v1/reception/practices/{practiceId}/reservations/{r.Id}";
            var label = Label(c);
            rows.Add(new(r.Id, "Reservation", c, label.Ar, label.En, date, DateTime.SpecifyKind(r.ScheduledStartUtc, DateTimeKind.Utc),
                r.ReservationReference, r.Status.ToString(), canView ? r.PatientId : null, canView ? r.NameAr : null, canView ? r.NameEn : null,
                [new("ViewReservation", canView, canView ? route : null), new("CheckIn", checkIn, $"/api/v1/practices/{practiceId}/reservations/{r.Id}/check-in"),
                 new("ForceCheckIn", practiceActive && !r.HasOpenTicket && r.Status == ReservationStatus.Active && date == today && Can(PermissionNames.PracticeTicketsForceCheckIn), $"/api/v1/practices/{practiceId}/reservations/{r.Id}/force-check-in"),
                 new("RestoreNoShow", practiceActive && r.Status == ReservationStatus.NoShow && date == today && Can(PermissionNames.PracticeReservationsRestoreNoShow), route + "/restore-no-show")]));
        }
        foreach (var t in tickets.Where(t => t.Status is TicketStatus.Waiting or TicketStatus.Called or TicketStatus.InProgress or TicketStatus.NoShow))
        {
            var c = t.Status == TicketStatus.NoShow ? WorklistCategory.TicketNoShow : WorklistCategory.QueueAttention;
            var label = Label(c); var canView = Can(PermissionNames.PracticeTicketsView);
            var route = $"/api/v1/practices/{practiceId}/tickets/{t.Id}";
            rows.Add(new(t.Id, "Ticket", c, label.Ar, label.En, date, DateTime.SpecifyKind(t.CheckInTimeUtc, DateTimeKind.Utc),
                t.TicketNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), t.Status.ToString(), canView ? t.PatientId : null,
                canView ? t.NameAr : null, canView ? t.NameEn : null, [new("ViewTicket", canView, canView ? route : null),
                    new("OpenQueue", Can(PermissionNames.PracticeTicketsView), $"/api/v1/practices/{practiceId}/queue"),
                    new("RestoreNoShow", practiceActive && date == today && !t.HasRefund && t.Status == TicketStatus.NoShow && Can(PermissionNames.PracticeTicketsRestoreNoShow), route + "/restore-no-show")]));
        }
        foreach (var h in handovers)
            rows.Add(new(h.Id, "Handover", WorklistCategory.OpenHandover, "تسليم مفتوح", "Open handover", date,
                DateTime.SpecifyKind(h.CreatedOnUtc, DateTimeKind.Utc), h.Id.ToString(), h.Status.ToString(), null, null, null,
                [new("ViewHandover", true, $"/api/v1/reception/practices/{practiceId}/handovers/{h.Id}")]));
        var filtered = rows.Where(r => f.Category == null || r.Category == f.Category).OrderBy(r => r.OccurredAtUtc).ThenBy(r => r.Id).ToArray();
        return Result<ReportPage<WorklistItem>>.Ok(new(filtered.Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).ToArray(), filtered.LongLength, f.PageNumber, f.PageSize,
            Warnings: Can(Phase18PermissionNames.HandoverView) ? ["Operations.ActionsAreRevalidatedByExistingWorkflow"]
                : ["Operations.ActionsAreRevalidatedByExistingWorkflow", "Handovers.UnavailableForCurrentDelegation"]));
    }
    public async Task<Result<ReceptionAttentionResponse>> AttentionAsync(Guid? practiceId, CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.Reception, Phase18PermissionNames.ReceptionDashboard, practiceId, null, ct);
        if (access.IsFailure) return Result<ReceptionAttentionResponse>.Fail(access.Errors);
        List<WorklistItem> items = []; long total = 0;
        foreach (var practice in access.Value.PracticeIds)
        {
            var result = await WorklistAsync(practice, new(PageSize: 100), ct);
            if (result.IsFailure) return Result<ReceptionAttentionResponse>.Fail(result.Errors);
            total += result.Value.TotalCount; items.AddRange(result.Value.Items);
        }
        var kpis = new OperationalKpiCalculator(registry);
        var handoverAccess = await guard.AuthorizeAsync(ReportActor.Reception, Phase18PermissionNames.HandoverView, practiceId, null, ct);
        var handoversAvailable = handoverAccess.IsSuccess && access.Value.PracticeIds.All(p => handoverAccess.Value.PracticeIds.Contains(p));
        var open = handoversAvailable ? await db.Set<ReceptionHandover>().AsNoTracking().CountAsync(h =>
            handoverAccess.Value.PracticeIds.Contains(h.DoctorPracticeId) && h.Status != HandoverStatus.Closed, ct) : 0;
        var page = new ReportPage<WorklistItem>(items.OrderBy(i => i.OccurredAtUtc).Take(100).ToArray(), total, 1, 100);
        return Result<ReceptionAttentionResponse>.Ok(new(clock.UtcNow, [handoversAvailable ? kpis.Count("OpenHandovers", open)
                : kpis.Missing("OpenHandovers", ReportDataStatus.NotAvailable, "Handovers.UnavailableForCurrentDelegation")], page,
            handoversAvailable ? ["Operations.QueueAttentionIsAnOperationalSignalNotAnSla"]
                : ["Operations.QueueAttentionIsAnOperationalSignalNotAnSla", "Handovers.UnavailableForCurrentDelegation"]));
    }
    private static (string Ar, string En) Label(WorklistCategory c) => c switch
    {
        WorklistCategory.UpcomingBooking => ("حجز قادم", "Upcoming booking"), WorklistCategory.AwaitingCheckIn => ("بانتظار تسجيل الحضور", "Awaiting check-in"),
        WorklistCategory.LateArrival => ("وصول متأخر", "Late arrival"), WorklistCategory.QueueAttention => ("متابعة الطابور", "Queue attention"),
        WorklistCategory.ReservationNoShow => ("عدم حضور الحجز", "Reservation no-show"), WorklistCategory.TicketNoShow => ("عدم حضور التذكرة", "Ticket no-show"),
        _ => ("تسليم مفتوح", "Open handover")
    };
}
