using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Practices;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Clinical;

internal sealed record ClinicalActor(Guid UserId, Guid DoctorId, IReadOnlyCollection<string> Permissions);

internal sealed class ClinicalAccessService(IWaslaDataStore dataStore, ICurrentUser currentUser,
    IReceptionPracticeAuthorizationService receptionAuthorization, ReservationApplicationService reservations)
{
    private UserAccessSnapshot? _snapshot;

    public async Task<Result<ClinicalActor>> DoctorAsync(Guid practiceId, string permission, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(ct);
        if (snapshot is null || !Has(snapshot, permission) || snapshot.DoctorId is not { } doctorId ||
            snapshot.DoctorStatus != DoctorApprovalStatus.Approved)
            return Result<ClinicalActor>.Fail(ClinicalErrors.AccessDenied);
        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, ct);
        return practice is not null && practice.IsActive && practice.DoctorId == doctorId
            ? Result<ClinicalActor>.Ok(new ClinicalActor(snapshot.User.Id, doctorId, snapshot.Permissions))
            : Result<ClinicalActor>.Fail(ClinicalErrors.AccessDenied);
    }

    public async Task<Result<Guid>> OwnPatientAsync(CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(ct);
        if (snapshot is null || !Has(snapshot, PermissionNames.MedicalEncountersViewOwnCompleted) ||
            !Has(snapshot, PermissionNames.DiagnosesViewOwnCompleted)) return Result<Guid>.Fail(ClinicalErrors.AccessDenied);
        var link = await dataStore.FindPatientAccountLinkAsync(snapshot.User.Id, ct);
        return link is null ? Result<Guid>.Fail(ClinicalErrors.AccessDenied) : Result<Guid>.Ok(link.PatientId);
    }

    public async Task<Result<IReadOnlyList<Guid>>> BookablePatientIdsAsync(Guid? patientId, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(ct);
        if (snapshot is null || !Has(snapshot, PermissionNames.FollowUpEligibilityViewOwn))
            return Result<IReadOnlyList<Guid>>.Fail(ClinicalErrors.AccessDenied);
        var bookable = await reservations.ListBookablePatientsAsync(ct);
        if (bookable.IsFailure) return Result<IReadOnlyList<Guid>>.Fail(bookable.Errors);
        if (patientId.HasValue && !bookable.Value.Any(p => p.PatientId == patientId))
            return Result<IReadOnlyList<Guid>>.Fail(ClinicalErrors.AccessDenied);
        return Result<IReadOnlyList<Guid>>.Ok(bookable.Value.Where(p => patientId is null || p.PatientId == patientId)
            .Select(p => p.PatientId).ToArray());
    }

    public Task<Result> ReceptionAsync(Guid practiceId, CancellationToken ct)
        => receptionAuthorization.AuthorizeAsync(practiceId, PermissionNames.FollowUpEligibilityViewBookingEligibility, ct);

    public bool HasPermission(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) &&
        (_snapshot is null || _snapshot.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase));
    public Guid ActorId => currentUser.UserId.GetValueOrDefault();
    private bool Has(UserAccessSnapshot snapshot, string permission) =>
        HasPermission(permission) && snapshot.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    private async Task<UserAccessSnapshot?> SnapshotAsync(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId) return null;
        var snapshot = await dataStore.GetAccessSnapshotAsync(userId, ct);
        _snapshot = snapshot;
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false } ? snapshot : null;
    }
}
