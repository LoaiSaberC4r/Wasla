using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public sealed record ChildOnboardingInput(string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender, string? PhoneNumber,
    string? PrimaryContactNameAr, string? PrimaryContactPhone, FamilyMemberRole RelationToChild);
public sealed record ChildOnboardingResponse(Guid RequestId, ChildOnboardingStatus Status, int Revision, DateTime CreatedAtUtc,
    string RowVersion, string? ReviewMessage, bool CanResubmit, Guid? ApprovedPatientId = null, Guid? FamilyRelationshipRequestId = null);
public sealed record ChildOnboardingDuplicateCandidate(Guid PatientId, string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender);
public sealed record ChildOnboardingReview(ChildOnboardingResponse Request, ChildOnboardingInput Intake, IReadOnlyList<FamilyRelationshipDocumentResponse> Evidence,
    IReadOnlyList<ChildOnboardingDuplicateCandidate> PossibleMatches);
public sealed record SubmitChildOnboardingCommand(ChildOnboardingInput Intake, IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey)
    : ICommand<ChildOnboardingResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReviewChildOnboardingCommand(Guid RequestId, ChildOnboardingStatus Decision, string RowVersion, string? Reason,
    Guid? ConfirmedExistingPatientId, string IdempotencyKey) : ICommand<ChildOnboardingResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ResubmitChildOnboardingCommand(Guid RequestId, string RowVersion, IReadOnlyList<FamilyEvidenceUpload> Evidence, string IdempotencyKey, ChildOnboardingInput? Intake = null)
    : ICommand<ChildOnboardingResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record GetChildOnboardingRequestsQuery(Guid? RequestId, bool Admin, int PageNumber = 1, int PageSize = 20) : IQuery<IReadOnlyList<ChildOnboardingReview>>;
public sealed record GetChildOnboardingEvidenceQuery(Guid RequestId, Guid EvidenceId) : IQuery<PrivateMedia>;
public interface IChildOnboardingWorkflow
{
    Task<Result<ChildOnboardingResponse>> SubmitAsync(SubmitChildOnboardingCommand request, CancellationToken ct);
    Task<Result<ChildOnboardingResponse>> ReviewAsync(ReviewChildOnboardingCommand request, CancellationToken ct);
    Task<Result<ChildOnboardingResponse>> ResubmitAsync(ResubmitChildOnboardingCommand request, CancellationToken ct);
    Task<Result<IReadOnlyList<ChildOnboardingReview>>> ReadAsync(GetChildOnboardingRequestsQuery request, CancellationToken ct);
    Task<Result<PrivateMedia>> EvidenceAsync(GetChildOnboardingEvidenceQuery request, CancellationToken ct);
}
internal sealed class SubmitChildOnboardingHandler(IChildOnboardingWorkflow workflow) : ICommandHandler<SubmitChildOnboardingCommand, ChildOnboardingResponse>
{ public Task<Result<ChildOnboardingResponse>> Handle(SubmitChildOnboardingCommand r, CancellationToken ct) => workflow.SubmitAsync(r, ct); }
internal sealed class ReviewChildOnboardingHandler(IChildOnboardingWorkflow workflow) : ICommandHandler<ReviewChildOnboardingCommand, ChildOnboardingResponse>
{ public Task<Result<ChildOnboardingResponse>> Handle(ReviewChildOnboardingCommand r, CancellationToken ct) => workflow.ReviewAsync(r, ct); }
internal sealed class ResubmitChildOnboardingHandler(IChildOnboardingWorkflow workflow) : ICommandHandler<ResubmitChildOnboardingCommand, ChildOnboardingResponse>
{ public Task<Result<ChildOnboardingResponse>> Handle(ResubmitChildOnboardingCommand r, CancellationToken ct) => workflow.ResubmitAsync(r, ct); }
internal sealed class ReadChildOnboardingHandler(IChildOnboardingWorkflow workflow) : IQueryHandler<GetChildOnboardingRequestsQuery, IReadOnlyList<ChildOnboardingReview>>
{ public Task<Result<IReadOnlyList<ChildOnboardingReview>>> Handle(GetChildOnboardingRequestsQuery r, CancellationToken ct) => workflow.ReadAsync(r, ct); }
internal sealed class ChildOnboardingEvidenceHandler(IChildOnboardingWorkflow workflow) : IQueryHandler<GetChildOnboardingEvidenceQuery, PrivateMedia>
{ public Task<Result<PrivateMedia>> Handle(GetChildOnboardingEvidenceQuery r, CancellationToken ct) => workflow.EvidenceAsync(r, ct); }

public sealed record RevokeOperationalAuthorityCommand(Guid AuthorityId, string RowVersion, string Reason, string IdempotencyKey)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;
internal sealed class RevokeOperationalAuthorityHandler(IFamilyOperationalVerification workflow) : ICommandHandler<RevokeOperationalAuthorityCommand>
{ public Task<Result> Handle(RevokeOperationalAuthorityCommand r, CancellationToken ct) => workflow.RevokeAsync(r.AuthorityId, r.RowVersion, r.Reason, r.IdempotencyKey, ct); }
public sealed record GetOperationalAuthoritiesQuery(Guid RelationshipRequestId) : IQuery<IReadOnlyList<OperationalAuthorityResponse>>;
internal sealed class GetOperationalAuthoritiesHandler(IFamilyOperationalVerification workflow) : IQueryHandler<GetOperationalAuthoritiesQuery, IReadOnlyList<OperationalAuthorityResponse>>
{ public Task<Result<IReadOnlyList<OperationalAuthorityResponse>>> Handle(GetOperationalAuthoritiesQuery r, CancellationToken ct) => workflow.ListAsync(r.RelationshipRequestId, ct); }
