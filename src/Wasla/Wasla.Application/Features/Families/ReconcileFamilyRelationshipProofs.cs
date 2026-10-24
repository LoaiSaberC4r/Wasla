using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Families;

// Operator-only maintenance command; deliberately has no HTTP route or startup worker.
public sealed record ReconcileFamilyRelationshipProofsCommand(string OperatorIdentifier, string OperatorPassword, bool Apply = false)
    : ICommand<FamilyProofReconciliationReport>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record FamilyProofReconciliationReport(int TotalCandidatePairs, int SafelyVerifiedPairs, int AlreadyVerifiedPairs,
    int AmbiguousPairs, int MissingProofPairs, int ConflictingPairs, int FailedRecords, bool Applied);
public interface IFamilyProofReconciler
{
    Task<FamilyProofReconciliationReport> ReconcileAsync(bool apply, CancellationToken ct);
}
internal sealed class ReconcileFamilyRelationshipProofsCommandHandler(IWaslaDataStore data, IPasswordService passwords, IFamilyProofReconciler reconciler)
    : ICommandHandler<ReconcileFamilyRelationshipProofsCommand, FamilyProofReconciliationReport>
{
    public async Task<Result<FamilyProofReconciliationReport>> Handle(ReconcileFamilyRelationshipProofsCommand c, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.OperatorIdentifier) || string.IsNullOrWhiteSpace(c.OperatorPassword))
            return Result<FamilyProofReconciliationReport>.Fail(FamilyMedicalErrors.Unavailable);
        var user = await data.FindUserByIdentifierAsync(c.OperatorIdentifier.Trim(), ct);
        var access = user is null ? null : await data.GetAccessSnapshotAsync(user.Id, ct);
        if (user is not { IsActive: true, IsFirstLogin: false, UserType: UserType.SuperAdmin } || access?.SuperAdminId is null ||
            !access.Permissions.Contains(PermissionNames.FamilyRelationshipRequestsApprove) ||
            !access.Permissions.Contains(PermissionNames.FamilyRelationshipRequestsViewDetails) ||
            !await passwords.VerifyAsync(c.OperatorPassword, user.PasswordHash, ct))
            return Result<FamilyProofReconciliationReport>.Fail(FamilyMedicalErrors.Unavailable);
        var report = await reconciler.ReconcileAsync(c.Apply, ct);
        var fresh = await data.GetAccessSnapshotAsync(user.Id, ct);
        if (fresh is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.SuperAdmin, SuperAdminId: not null } ||
            !fresh.Permissions.Contains(PermissionNames.FamilyRelationshipRequestsApprove) || !fresh.Permissions.Contains(PermissionNames.FamilyRelationshipRequestsViewDetails))
            return Result<FamilyProofReconciliationReport>.Fail(FamilyMedicalErrors.Unavailable);
        return Result<FamilyProofReconciliationReport>.Ok(report);
    }
}
