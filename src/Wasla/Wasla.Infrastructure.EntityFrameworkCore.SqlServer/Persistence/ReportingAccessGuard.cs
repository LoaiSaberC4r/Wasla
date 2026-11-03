using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Application.Features.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ReportingAccessGuard(WaslaDbContext db, ICurrentUser currentUser,
    IWaslaDataStore store, ISubscriptionPermissionGuard subscriptions) : IReportingAccessGuard
{
    public async Task<Result<ReportScope>> AuthorizeAsync(ReportActor actor, string permission,
        Guid? practiceId, Guid? doctorId, CancellationToken cancellationToken, SubscriptionResource? resource = null)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result<ReportScope>.Fail(ReportingErrors.Denied);
        var access = await store.GetAccessSnapshotAsync(userId, cancellationToken);
        if (access is null || !access.User.IsActive || access.User.IsFirstLogin || !access.Permissions.Contains(permission, StringComparer.Ordinal))
            return Result<ReportScope>.Fail(ReportingErrors.Denied);
        Guid? ownDoctor = null;
        Guid? patient = null;
        Guid[] practices;
        switch (actor)
        {
            case ReportActor.Doctor:
                if (access.User.UserType != UserType.Doctor || access.DoctorId is not { } doctor || access.DoctorStatus != DoctorApprovalStatus.Approved ||
                    !access.Roles.Contains(SystemRoleNames.Doctor) || doctorId is { } selectedDoctor && selectedDoctor != doctor)
                    return Result<ReportScope>.Fail(ReportingErrors.Denied);
                ownDoctor = doctor;
                practices = await db.DoctorPractices.AsNoTracking().Where(p => p.DoctorId == doctor &&
                    (practiceId == null || p.Id == practiceId)).Select(p => p.Id).ToArrayAsync(cancellationToken);
                break;
            case ReportActor.Reception:
                if (access.User.UserType != UserType.Reception || access.ReceptionId is not { } reception || !access.Roles.Contains(SystemRoleNames.Reception) || doctorId != null)
                    return Result<ReportScope>.Fail(ReportingErrors.Denied);
                var permissionId = SystemPermissionIds.For(permission);
                practices = await (from assignment in db.ReceptionPracticeAssignments.AsNoTracking()
                    join delegated in db.ReceptionPracticeAssignmentPermissions.AsNoTracking() on assignment.Id equals delegated.AssignmentId
                    join r in db.Receptions.AsNoTracking() on assignment.ReceptionId equals r.Id
                    join p in db.DoctorPractices.AsNoTracking() on assignment.DoctorPracticeId equals p.Id
                    join owner in db.Doctors.AsNoTracking() on p.DoctorId equals owner.Id
                    join ownerAccount in db.ApplicationUsers.AsNoTracking() on owner.ApplicationUserId equals ownerAccount.Id
                    where assignment.ReceptionId == reception && assignment.IsActive &&
                        delegated.PermissionId == permissionId && p.DoctorId == r.OwnerDoctorId &&
                        owner.ApprovalStatus == DoctorApprovalStatus.Approved && ownerAccount.IsActive &&
                        (practiceId == null || p.Id == practiceId)
                    select p.Id).Distinct().ToArrayAsync(cancellationToken);
                break;
            case ReportActor.SuperAdmin:
                if (access.User.UserType != UserType.SuperAdmin || access.SuperAdminId == null || !access.Roles.Contains(SystemRoleNames.SuperAdmin))
                    return Result<ReportScope>.Fail(ReportingErrors.Denied);
                ownDoctor = doctorId;
                practices = await db.DoctorPractices.AsNoTracking().Where(p =>
                    (doctorId == null || p.DoctorId == doctorId) && (practiceId == null || p.Id == practiceId))
                    .Select(p => p.Id).ToArrayAsync(cancellationToken);
                if (practiceId != null && practices.Length == 1)
                    ownDoctor = await db.DoctorPractices.AsNoTracking().Where(p => p.Id == practiceId).Select(p => p.DoctorId).SingleAsync(cancellationToken);
                break;
            case ReportActor.Patient:
                if (access.User.UserType != UserType.Patient || access.PatientId is not { } patientId || !access.Roles.Contains(SystemRoleNames.Patient) || practiceId != null || doctorId != null)
                    return Result<ReportScope>.Fail(ReportingErrors.Denied);
                patient = patientId;
                practices = [];
                break;
            default:
                return Result<ReportScope>.Fail(ReportingErrors.Denied);
        }
        if (practiceId != null && practices.Length == 0) return Result<ReportScope>.Fail(ReportingErrors.NotFound);
        var effectivePermissions = access.Permissions;
        SubscriptionReadScope? readScope = null;
        IReadOnlyDictionary<string, SubscriptionReadScope>? readScopes = null;
        if (actor is ReportActor.Doctor or ReportActor.Reception)
        {
            var evaluated = await subscriptions.EvaluateManyAsync(access, access.Permissions.ToArray(), cancellationToken);
            var requested = evaluated[permission];
            if (resource is not null)
                requested = (await subscriptions.EvaluateManyAsync(access, [permission], cancellationToken, resource))[permission];
            if (!requested.Allowed) return Result<ReportScope>.Fail(ReportingErrors.Denied);
            readScope = requested.ReadScope;
            readScopes = evaluated.Where(p => p.Value.ReadScope?.HasAccess == true)
                .GroupBy(p => CommercialFeatures.FeatureForPermission(p.Key)!, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Value.ReadScope!, StringComparer.Ordinal);
            effectivePermissions = evaluated.Where(p => p.Value.Allowed).Select(p => p.Key).Append(permission).Distinct(StringComparer.Ordinal).ToArray();
        }
        return Result<ReportScope>.Ok(new(actor, userId, ownDoctor, patient, practices, effectivePermissions, readScope, readScopes));
    }
}
