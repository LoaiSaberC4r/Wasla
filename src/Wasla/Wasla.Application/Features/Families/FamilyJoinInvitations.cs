using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public sealed record FamilyJoinInvitationResponse(string InvitationReference, DateTime ExpiresAtUtc, string NextAction);
public sealed record CreateFamilyJoinInvitationCommand(Guid ChildPatientId, FamilyMemberRole IntendedParentRole, string IdempotencyKey)
    : ICommand<FamilyJoinInvitationResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RedeemFamilyJoinInvitationCommand(string InvitationReference, FamilyMemberRole IntendedParentRole,
    IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey)
    : ICommand<FamilyCandidateSubmissionResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RevokeFamilyJoinInvitationCommand(string InvitationReference, string IdempotencyKey)
    : ICommand<object>, ITransactionalCommand<WaslaWritePersistence>;
public interface IFamilyJoinInvitations
{
    Task<Result<FamilyJoinInvitationResponse>> CreateAsync(CreateFamilyJoinInvitationCommand command, CancellationToken ct);
    Task<Result<object>> RevokeAsync(RevokeFamilyJoinInvitationCommand command, CancellationToken ct);
    Task<Result<FamilyCandidateSubmissionResponse>> RedeemAsync(RedeemFamilyJoinInvitationCommand command, CancellationToken ct);
}
internal sealed class RevokeFamilyJoinInvitationHandler(IFamilyJoinInvitations service) : ICommandHandler<RevokeFamilyJoinInvitationCommand, object>
{ public Task<Result<object>> Handle(RevokeFamilyJoinInvitationCommand r, CancellationToken ct) => service.RevokeAsync(r, ct); }
internal sealed class CreateFamilyJoinInvitationHandler(IFamilyJoinInvitations service) : ICommandHandler<CreateFamilyJoinInvitationCommand, FamilyJoinInvitationResponse>
{ public Task<Result<FamilyJoinInvitationResponse>> Handle(CreateFamilyJoinInvitationCommand r, CancellationToken ct) => service.CreateAsync(r, ct); }
internal sealed class RedeemFamilyJoinInvitationHandler(IFamilyJoinInvitations service) : ICommandHandler<RedeemFamilyJoinInvitationCommand, FamilyCandidateSubmissionResponse>
{ public Task<Result<FamilyCandidateSubmissionResponse>> Handle(RedeemFamilyJoinInvitationCommand r, CancellationToken ct) => service.RedeemAsync(r, ct); }
