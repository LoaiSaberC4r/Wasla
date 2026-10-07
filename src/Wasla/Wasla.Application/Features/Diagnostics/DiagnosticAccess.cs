using System.Linq.Expressions;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using Wasla.Application.Features.Auth;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Diagnostics;

internal sealed record DiagnosticActor(Guid UserId, Guid? DoctorId, Guid? PatientId, IReadOnlyList<string> Permissions)
{
    public Guid OwnerId => DoctorId ?? PatientId ?? UserId;
}
internal sealed class DiagnosticAccess(IWaslaDataStore store, ICurrentUser current)
{
    public async Task<Result<DiagnosticActor>> ActorAsync(string permission, UserType type, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } id) return Result<DiagnosticActor>.Fail(DiagnosticErrors.Denied());
        var snapshot = await store.GetAccessSnapshotAsync(id, ct);
        if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false } || snapshot.User.UserType != type ||
            !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
            !LoginCommandHandler.EffectivePermissions(snapshot).Contains(permission, StringComparer.OrdinalIgnoreCase) ||
            type == UserType.Doctor && (snapshot.DoctorId is null || snapshot.DoctorStatus != DoctorApprovalStatus.Approved) ||
            type == UserType.MedicalCatalogManager && !snapshot.Roles.Contains(SystemRoleNames.MedicalCatalogManager))
            return Result<DiagnosticActor>.Fail(DiagnosticErrors.Denied());
        Guid? patient = null;
        if (type == UserType.Patient)
        {
            patient = (await store.FindPatientAccountLinkAsync(id, ct))?.PatientId;
            if (patient is null) return Result<DiagnosticActor>.Fail(DiagnosticErrors.Denied());
        }
        return Result<DiagnosticActor>.Ok(new(id, snapshot.DoctorId, patient,
            LoginCommandHandler.EffectivePermissions(snapshot).Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray()));
    }
    public async Task<bool> OwnPracticeAsync(Guid doctor, Guid practice, CancellationToken ct)
        => await store.FindDoctorPracticeAsync(practice, ct) is { IsActive: true } p && p.DoctorId == doctor;
}
internal sealed class DiagnosticForUpdate<T> : Specification<T> where T : class
{
    public DiagnosticForUpdate(Expression<Func<T, bool>> predicate, params Expression<Func<T, object?>>[] includes)
    { AddCriteria(predicate); foreach (var include in includes) AddInclude(include); UseTracking(); }
}
