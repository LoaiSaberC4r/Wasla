using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public sealed record DependentIdentityCorrectionInput(string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender);
public sealed record FamilyGovernanceReceipt(Guid CaseId, FamilyGovernanceCaseKind Kind, FamilyGovernanceCaseStatus Status,
    int Revision, string RowVersion, string NextAction);
public sealed record SubmitDependentIdentityCorrectionCommand(Guid ChildPatientId, DependentIdentityCorrectionInput ProposedIdentity,
    string Reason, IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey, Guid? CaseId = null, string? RowVersion = null)
    : ICommand<FamilyGovernanceReceipt>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ResubmitFamilyConflictCommand(Guid CaseId, string RowVersion, IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey)
    : ICommand<FamilyGovernanceReceipt>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DecideFamilyGovernanceCaseCommand(Guid CaseId, FamilyGovernanceCaseStatus Decision, string RowVersion, string Reason,
    string IdempotencyKey) : ICommand<FamilyGovernanceReceipt>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record GetFamilyGovernanceCasesQuery(Guid? CaseId, bool Admin, FamilyGovernanceCaseKind? Kind = null,
    int PageNumber = 1, int PageSize = 20) : IQuery<FamilyGovernancePage>;
public sealed record FamilyGovernanceEvidenceQuery(Guid CaseId, Guid EvidenceId) : IQuery<PrivateMedia>;
public sealed record FamilyGovernanceRevisionResponse(int Revision, string BeforeSnapshot, string AfterSnapshot, string Reason, DateTime CreatedAtUtc);
public sealed record FamilyGovernanceHistoryResponse(int Revision, Guid ActorUserId, string Action, string? Reason, DateTime OccurredAtUtc);
public sealed record FamilyGovernanceDetails(FamilyGovernanceReceipt Request, DateTime CreatedAtUtc, string? Feedback,
    IReadOnlyList<FamilyGovernanceRevisionResponse> Revisions, IReadOnlyList<FamilyRelationshipDocumentResponse> Evidence,
    IReadOnlyList<FamilyGovernanceHistoryResponse> History);
public sealed record FamilyGovernancePage(IReadOnlyList<FamilyGovernanceDetails> Items, int PageNumber, int PageSize, int TotalCount);
public interface IFamilyGovernanceWorkflow
{
    Task<Result<FamilyGovernanceReceipt>> SubmitIdentityAsync(SubmitDependentIdentityCorrectionCommand command, CancellationToken ct);
    Task<Result<FamilyGovernanceReceipt>> ResubmitConflictAsync(ResubmitFamilyConflictCommand command, CancellationToken ct);
    Task<Result<FamilyGovernanceReceipt>> DecideAsync(DecideFamilyGovernanceCaseCommand command, CancellationToken ct);
    Task<Result<FamilyGovernancePage>> ReadAsync(GetFamilyGovernanceCasesQuery query, CancellationToken ct);
    Task<Result<PrivateMedia>> EvidenceAsync(FamilyGovernanceEvidenceQuery query, CancellationToken ct);
    // Called only after an actor-bound opaque candidate redemption, inside its transaction.
    Task<Result<FamilyGovernanceReceipt>> OpenConflictAsync(Guid actorUserId, Guid parentPatientId, Guid childPatientId,
        FamilyMemberRole intendedRole, IReadOnlyList<FamilyEvidenceUpload> evidence, string key, string fingerprint, CancellationToken ct);
}
internal sealed class SubmitDependentIdentityCorrectionHandler(IFamilyGovernanceWorkflow workflow) : ICommandHandler<SubmitDependentIdentityCorrectionCommand, FamilyGovernanceReceipt>
{ public Task<Result<FamilyGovernanceReceipt>> Handle(SubmitDependentIdentityCorrectionCommand request, CancellationToken ct) => workflow.SubmitIdentityAsync(request, ct); }
internal sealed class ResubmitFamilyConflictHandler(IFamilyGovernanceWorkflow workflow) : ICommandHandler<ResubmitFamilyConflictCommand, FamilyGovernanceReceipt>
{ public Task<Result<FamilyGovernanceReceipt>> Handle(ResubmitFamilyConflictCommand request, CancellationToken ct) => workflow.ResubmitConflictAsync(request, ct); }
internal sealed class DecideFamilyGovernanceCaseHandler(IFamilyGovernanceWorkflow workflow) : ICommandHandler<DecideFamilyGovernanceCaseCommand, FamilyGovernanceReceipt>
{ public Task<Result<FamilyGovernanceReceipt>> Handle(DecideFamilyGovernanceCaseCommand request, CancellationToken ct) => workflow.DecideAsync(request, ct); }
internal sealed class GetFamilyGovernanceCasesHandler(IFamilyGovernanceWorkflow workflow) : IQueryHandler<GetFamilyGovernanceCasesQuery, FamilyGovernancePage>
{ public Task<Result<FamilyGovernancePage>> Handle(GetFamilyGovernanceCasesQuery request, CancellationToken ct) => workflow.ReadAsync(request, ct); }
internal sealed class FamilyGovernanceEvidenceHandler(IFamilyGovernanceWorkflow workflow) : IQueryHandler<FamilyGovernanceEvidenceQuery, PrivateMedia>
{ public Task<Result<PrivateMedia>> Handle(FamilyGovernanceEvidenceQuery request, CancellationToken ct) => workflow.EvidenceAsync(request, ct); }
