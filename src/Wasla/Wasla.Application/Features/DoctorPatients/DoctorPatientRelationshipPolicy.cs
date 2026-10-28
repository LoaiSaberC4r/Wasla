using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Auth;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.DoctorPatients;

public sealed class DoctorPatientRelationshipPolicy(IWaslaDataStore store, ICurrentUser current,
    IDoctorPatientReadService reader)
{
    public async Task<Result<DoctorPatientActor>> ActorAsync(string permission, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } userId)
            return Result<DoctorPatientActor>.Fail(ClinicalErrors.AccessDenied);
        var snapshot = await store.GetAccessSnapshotAsync(userId, ct);
        if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false, DoctorStatus: DoctorApprovalStatus.Approved,
                DoctorId: not null } || snapshot.User.UserType != UserType.Doctor)
            return Result<DoctorPatientActor>.Fail(ClinicalErrors.AccessDenied);
        var permissions = LoginCommandHandler.EffectivePermissions(snapshot)
            .Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray();
        var actor = new DoctorPatientActor(userId, snapshot.DoctorId.Value, permissions);
        return actor.Has(permission) ? Result<DoctorPatientActor>.Ok(actor)
            : Result<DoctorPatientActor>.Fail(ClinicalErrors.AccessDenied);
    }
    public async Task<Result<DoctorPatientActor>> AuthorizeAsync(Guid patientId, CancellationToken ct)
    {
        var actor = await ActorAsync(PermissionNames.DoctorPatientsViewOwn, ct);
        if (actor.IsFailure) return actor;
        return await reader.HasAttendanceAsync(actor.Value.DoctorId, patientId, ct) ? actor
            : Result<DoctorPatientActor>.Fail(ClinicalErrors.NotFound);
    }
    public async Task<bool> OwnPracticeAsync(DoctorPatientActor actor, Guid? practiceId, CancellationToken ct)
        => practiceId is null || (await reader.PracticeScopeAsync(practiceId.Value, ct))?.DoctorId == actor.DoctorId;
}
