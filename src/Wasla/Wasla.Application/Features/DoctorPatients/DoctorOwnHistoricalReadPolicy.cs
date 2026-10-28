using BuildingBlock.Domain.Results;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.DoctorPatients;

// A read decision only. Writer guards deliberately do not depend on this policy.
public sealed class DoctorOwnHistoricalReadPolicy(DoctorPatientRelationshipPolicy relationships, IDoctorPatientReadService reader)
{
    public async Task<Result<DoctorOwnReadContext>> AuthorizeOwnReadAsync(Guid practiceId, string permission,
        Guid? patientId, CancellationToken ct)
    {
        var actor = await relationships.ActorAsync(permission, ct);
        if (actor.IsFailure) return Result<DoctorOwnReadContext>.Fail(actor.Errors);
        var practice = await reader.PracticeScopeAsync(practiceId, ct);
        if (practice is null || practice.DoctorId != actor.Value.DoctorId)
            return Result<DoctorOwnReadContext>.Fail(ClinicalErrors.NotFound);
        if (!practice.IsActive && (!actor.Value.Has(PermissionNames.DoctorPatientsViewOwn) ||
            !actor.Value.Has(PermissionNames.DoctorPatientsViewHistoricalOwn)))
            return Result<DoctorOwnReadContext>.Fail(ClinicalErrors.AccessDenied);
        if (patientId is { } patient && !await reader.HasAttendanceAsync(actor.Value.DoctorId, patient, ct))
            return Result<DoctorOwnReadContext>.Fail(ClinicalErrors.NotFound);
        return Result<DoctorOwnReadContext>.Ok(new(actor.Value, practiceId, !practice.IsActive));
    }
}
