using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<IReadOnlyList<ReportRecord>> ArchiveLinksAsync(IEnumerable<ReportRecord> records, ReportScope scope, CancellationToken ct)
    {
        var rows = records.ToArray();
        var ticketIds = rows.Where(x => x.ResourceType == "Ticket").Select(x => x.Id).ToArray();
        var reservationIds = rows.Where(x => x.ResourceType == "Reservation").Select(x => x.Id).ToArray();
        var tickets = await db.TicketRegistry.AsNoTracking().Where(x => ticketIds.Contains(x.Id) && x.IsArchived).Select(x => x.Id).ToArrayAsync(ct);
        var reservations = await db.ReservationRegistry.AsNoTracking().Where(x => reservationIds.Contains(x.Id) && x.IsArchived).Select(x => x.Id).ToArrayAsync(ct);
        var archivedTickets = tickets.ToHashSet(); var archivedReservations = reservations.ToHashSet();
        var delegated = scope.Actor == ReportActor.Reception ? await (from a in db.ReceptionPracticeAssignments
            join r in db.Receptions on a.ReceptionId equals r.Id
            join p in db.ReceptionPracticeAssignmentPermissions on a.Id equals p.AssignmentId
            where a.IsActive && r.ApplicationUserId == scope.ActorId &&
                (p.PermissionId == SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsAssigned) || p.PermissionId == SystemPermissionIds.For(Phase18PartTwoPermissionNames.ReservationsAssigned))
            select new { a.DoctorPracticeId, p.PermissionId }).ToArrayAsync(ct) : [];
        return rows.Select(row =>
        {
            var archived = row.ResourceType == "Ticket" ? archivedTickets.Contains(row.Id) : row.ResourceType == "Reservation" && archivedReservations.Contains(row.Id);
            if (!archived) return row;
            var permission = row.ResourceType == "Ticket"
                ? scope.Actor == ReportActor.Doctor ? Phase18PartTwoPermissionNames.TicketsPractice : scope.Actor == ReportActor.Reception ? Phase18PartTwoPermissionNames.TicketsAssigned : Phase18PartTwoPermissionNames.TicketsOwn
                : scope.Actor == ReportActor.Doctor ? Phase18PartTwoPermissionNames.ReservationsPractice : scope.Actor == ReportActor.Reception ? Phase18PartTwoPermissionNames.ReservationsAssigned : Phase18PartTwoPermissionNames.ReservationsOwn;
            var canRead = scope.Actor != ReportActor.SuperAdmin && scope.Permissions.Contains(permission) &&
                (scope.Actor != ReportActor.Reception || delegated.Any(x => x.DoctorPracticeId == row.PracticeId && x.PermissionId == SystemPermissionIds.For(permission)));
            var route = row.ResourceType == "Ticket" ? scope.Actor == ReportActor.Patient ? $"/api/v1/tickets/mine/{row.Id}" : $"/api/v1/practices/{row.PracticeId}/tickets/{row.Id}" :
                scope.Actor == ReportActor.Patient ? $"/api/v1/reservations/mine/{row.Id}" : scope.Actor == ReportActor.Doctor ? $"/api/v1/doctors/me/practices/{row.PracticeId}/reservations/{row.Id}" : $"/api/v1/reception/practices/{row.PracticeId}/reservations/{row.Id}";
            return row with { IsArchived = true, DetailRoute = canRead ? route : null,
                AvailableActions = row.AvailableActions?.Select(action => action.Code is "Cancel" or "Restore" or "Reschedule" or "CheckIn" or "Start" or "Complete"
                    ? action with { Allowed = false, Route = null, UnavailableReasonCode = "Archive.ReadOnly" }
                    : action.Code is "Details" or "ViewDetails" ? action with { Allowed = canRead, Route = canRead ? route : null, UnavailableReasonCode = canRead ? null : "Archive.AccessDenied" } : action).ToArray() };
        }).ToArray();
    }
}
