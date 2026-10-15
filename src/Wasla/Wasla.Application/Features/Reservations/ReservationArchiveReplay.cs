using BuildingBlock.Domain.Results;
using Wasla.Domain.Security;
using Wasla.Domain.Reservations;

namespace Wasla.Application.Features.Reservations;

internal sealed partial class ReservationApplicationService
{
    private async Task<Result<ReservationDetailsResponse>?> ArchivedReplayAsync(string operation, string key, string fingerprint,
        ReservationOperationScope scope, Guid? practiceId, CancellationToken ct)
    {
        if (currentUser.UserId is not { } actorId) return null;
        var completed = await dataStore.FindReservationIdempotencyAsync(actorId, operation, key, ct);
        if (completed?.ReservationId is not { } id || completed.CompletedOnUtc == null) return null;
        if (completed.RequestFingerprint != fingerprint) return Result<ReservationDetailsResponse>.Fail(ReservationErrors.IdempotencyKeyReused);
        if ((await archive.ReservationAsync(id, ct))?.IsArchived != true) return null;
        var record = await dataStore.GetReservationViewAsync(id, ct);
        if (record == null || practiceId != null && record.Reservation.DoctorPracticeId != practiceId) return Result<ReservationDetailsResponse>.Fail(ReservationErrors.NotFound);
        // Completed retries remain actor/key/fingerprint bound and require both
        // current command authorization and current archive read entitlement.
        var rights = operation switch
        {
            "Cancel" => (PermissionNames.PracticeReservationsCancel, PermissionNames.DoctorPracticeReservationsCancelOwn, PermissionNames.ReservationsCancelOwn, PermissionNames.ReservationsCancelDependents),
            "RestoreNoShow" => (PermissionNames.PracticeReservationsRestoreNoShow, PermissionNames.DoctorPracticeReservationsRescheduleOwn, PermissionNames.ReservationsRescheduleOwn, PermissionNames.ReservationsRescheduleDependents),
            "Reschedule" => (PermissionNames.PracticeReservationsReschedule, PermissionNames.DoctorPracticeReservationsRescheduleOwn, PermissionNames.ReservationsRescheduleOwn, PermissionNames.ReservationsRescheduleDependents),
            _ => (PermissionNames.PracticeReservationsCreate, PermissionNames.DoctorPracticeReservationsViewOwn, PermissionNames.ReservationsCreateOwn, PermissionNames.ReservationsCreateDependents)
        };
        var allowed = await AuthorizeExistingAsync(record.Reservation, scope, rights.Item1, rights.Item2, rights.Item3, rights.Item4, ct);
        return allowed.IsFailure ? Result<ReservationDetailsResponse>.Fail(allowed.Errors) :
            await GetDetailsForActorAsync(id, scope, practiceId, true, ct);
    }
}
