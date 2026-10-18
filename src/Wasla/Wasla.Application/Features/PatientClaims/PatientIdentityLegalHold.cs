using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Application.Features.PatientClaims;

// Secured operator-only command. It has no HTTP route and requires fresh password authentication.
public sealed record SetPatientClaimLegalHoldCommand(string OperatorIdentifier, string OperatorPassword, Guid ClaimId, bool Held, string Reason)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;
internal sealed class SetPatientClaimLegalHoldCommandHandler(IWaslaDataStore data, IPatientIdentityStore store, IPasswordService passwords, IDateTimeProvider clock)
    : ICommandHandler<SetPatientClaimLegalHoldCommand>
{
    public async Task<Result> Handle(SetPatientClaimLegalHoldCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorIdentifier) || string.IsNullOrWhiteSpace(request.OperatorPassword) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Result.Fail(PatientIdentityErrors.Denied);
        var user = await data.FindUserByIdentifierAsync(request.OperatorIdentifier.Trim(), cancellationToken);
        var access = user is null ? null : await data.GetAccessSnapshotAsync(user.Id, cancellationToken);
        if (user is not { IsActive: true, IsFirstLogin: false, UserType: UserType.SuperAdmin } || access?.SuperAdminId is null ||
            !access.Roles.Contains(SystemRoleNames.SuperAdmin) || !access.Permissions.Contains(PermissionNames.PatientClaimsApprove) || !access.Permissions.Contains(PermissionNames.PatientClaimsViewEvidence) ||
            !await passwords.VerifyAsync(request.OperatorPassword, user.PasswordHash, cancellationToken)) return Result.Fail(PatientIdentityErrors.Denied);
        await store.LockAsync(cancellationToken); var c = await store.ClaimAsync(request.ClaimId, cancellationToken);
        if (c is null) return Result.Fail(PatientIdentityErrors.NotFound);
        if (c.IsUnderLegalHold == request.Held) return Result.Ok();
        c.SetLegalHold(request.Held, user.Id, request.Reason, clock.UtcNow);
        data.Add(new PatientClaimHistory(Guid.NewGuid(), c.Id, c.EvidenceRevision, c.Status, c.Status, request.Held ? PatientClaimAction.LegalHoldPlaced : PatientClaimAction.LegalHoldReleased, user.Id, clock.UtcNow, request.Reason));
        await data.SaveChangesAsync(cancellationToken); return Result.Ok();
    }
}
