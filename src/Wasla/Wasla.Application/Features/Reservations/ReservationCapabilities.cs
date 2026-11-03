using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Reservations;

internal sealed partial class ReservationApplicationService
{
    private sealed record ReservationMutationAuthorization(SubscriptionPermissionDecision Cancel,
        SubscriptionPermissionDecision Reschedule, SubscriptionPermissionDecision Restore);

    private async Task<ReservationMutationAuthorization> ResolveMutationCapabilitiesAsync(Reservation reservation,
        ReservationOperationScope scope, CancellationToken ct)
    {
        var denied = new SubscriptionPermissionDecision(false, "Reservation.AccessDenied");
        var none = new ReservationMutationAuthorization(denied, denied, denied);
        if (currentUser.UserId is not { } userId) return none;
        var user = await dataStore.GetAccessSnapshotAsync(userId, ct);
        if (user is not { User.IsActive: true, User.IsFirstLogin: false }) return none;
        if (scope == ReservationOperationScope.Patient)
        {
            var own = user.PatientId == reservation.PatientId;
            return new(new(user.Permissions.Contains(own ? PermissionNames.ReservationsCancelOwn : PermissionNames.ReservationsCancelDependents), "Reservation.AccessDenied"),
                new(user.Permissions.Contains(own ? PermissionNames.ReservationsRescheduleOwn : PermissionNames.ReservationsRescheduleDependents), "Reservation.AccessDenied"), denied);
        }
        if (scope is not (ReservationOperationScope.Doctor or ReservationOperationScope.Reception)) return none;
        var doctor = scope == ReservationOperationScope.Doctor;
        var view = doctor ? PermissionNames.DoctorPracticeReservationsViewOwn : PermissionNames.PracticeReservationsView;
        var cancel = doctor ? PermissionNames.DoctorPracticeReservationsCancelOwn : PermissionNames.PracticeReservationsCancel;
        var reschedule = doctor ? PermissionNames.DoctorPracticeReservationsRescheduleOwn : PermissionNames.PracticeReservationsReschedule;
        var restore = PermissionNames.PracticeReservationsRestoreNoShow;
        string[] requested = doctor ? [view, cancel, reschedule] : [view, cancel, reschedule, restore];
        var evaluated = await subscriptionPermissions.EvaluateManyAsync(user, requested, ct, new(SubscriptionResourceKind.Reservation, reservation.Id));
        HashSet<Guid>? delegated = null;
        if (!doctor)
        {
            var reception = await dataStore.FindReceptionByApplicationUserIdAsync(userId, ct);
            if (reception?.OwnerDoctorId != reservation.DoctorId) return none;
            var assignment = (await dataStore.ListReceptionAssignmentsAsync(reception.Id, ct))
                .SingleOrDefault(a => a.DoctorPracticeId == reservation.DoctorPracticeId && a.IsActive);
            if (assignment is null) return none;
            delegated = (await dataStore.ListReceptionAssignmentPermissionsAsync(assignment.Id, ct)).Select(p => p.PermissionId).ToHashSet();
        }
        SubscriptionPermissionDecision Action(string permission) => delegated is not null && !delegated.Contains(SystemPermissionIds.For(permission))
            ? denied : evaluated[permission];
        if (!Action(view).Allowed) return new(Action(view), Action(view), Action(view));
        return new(Action(cancel), Action(reschedule), doctor ? denied : Action(restore));
    }
}
