using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public sealed class FamilyMedicalPolicyOptions
{
    public bool Approved { get; set; }
    public bool MasterSourceOfTruthAdopted { get; set; }
    public string? PolicyVersion { get; set; }
    public string? ApprovalReference { get; set; }
    public string? Jurisdiction { get; set; }
    public string? TimeZoneId { get; set; }
    public string[] BasisReferences { get; set; } = [];
    public FamilyRelationshipDocumentType[] AllowedDocumentTypes { get; set; } = [];
    public bool RestrictionEvidenceRequired { get; set; }
    public bool RestorationEvidenceRequired { get; set; }
    // Signed-policy references; an empty list prevents new restriction approvals.
    public string[] RestrictionGroundsReferences { get; set; } = [];
    public int MaximumFiles { get; set; } = 5;
    public long MaximumFileBytes { get; set; } = 10 * 1024 * 1024;
    public bool IsConfigured => Approved && MasterSourceOfTruthAdopted && !string.IsNullOrWhiteSpace(PolicyVersion) &&
        !string.IsNullOrWhiteSpace(ApprovalReference) && !string.IsNullOrWhiteSpace(Jurisdiction) && !string.IsNullOrWhiteSpace(TimeZoneId) &&
        PolicyVersion.Length <= 100 && BasisReferences.Length > 0 && BasisReferences.All(b => !string.IsNullOrWhiteSpace(b) && b.Length <= 500) &&
        AllowedDocumentTypes.Length > 0 && AllowedDocumentTypes.All(Enum.IsDefined) &&
        MaximumFiles is > 0 and <= 20 && MaximumFileBytes is > 0 and <= 25 * 1024 * 1024 && ValidTimeZone();
    private bool ValidTimeZone()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId)) return false;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
public sealed record FamilyMedicalAccessStatusResponse(string Status, bool CanView, bool CanRequestRestriction,
    bool CanRequestRestoration, Guid? ActiveOwnRequestId, FamilyMedicalRequestType? ActiveOwnRequestType, string? UnavailableReasonCode);
public sealed record FamilyMedicalDependent(Guid PatientId, string NameAr, string? NameEn, FamilyMedicalAccessStatusResponse MedicalAccess);
public sealed record FamilyMedicalRequestCapabilities(bool CanWithdraw, bool CanResubmit, bool CanApprove, bool CanReject, bool CanRequestModification);
public sealed record FamilyMedicalDocumentResponse(Guid DocumentId, int RevisionNumber, FamilyRelationshipDocumentType DocumentType,
    string FileName, string ContentType, long SizeBytes, bool CanDownload);
public sealed record FamilyMedicalCaseResponse(Guid RequestId, Guid FamilyId, Guid ChildPatientId, Guid AffectedAdultPatientId,
    FamilyMedicalRequestType RequestType, FamilyMedicalRequestStatus Status, int Revision, string Reason,
    DateTime SubmittedAtUtc, string RowVersion, string? ReviewMessage, string? RestrictionStatus,
    FamilyMedicalRequestCapabilities Capabilities, IReadOnlyList<FamilyMedicalDocumentResponse> Documents);
public sealed record FamilyMedicalHistoryResponse(Guid HistoryId, string Action, int? OldStatus, int NewStatus,
    int Revision, DateTime OccurredAtUtc, string? Message);
public sealed record FamilyMedicalAdminHistoryResponse(Guid HistoryId, string Action, int? OldStatus, int NewStatus,
    int Revision, Guid? ActorApplicationUserId, DateTime OccurredAtUtc, string? Message);
public sealed record FamilyMedicalQueueResponse(Guid RequestId, Guid FamilyId, Guid ChildPatientId, Guid AffectedAdultPatientId,
    FamilyMedicalRequestType RequestType, FamilyMedicalRequestStatus Status, int Revision, DateTime SubmittedAtUtc, string RowVersion);
public sealed record FamilyMedicalRelationshipVerification(Guid FamilyMemberId, Guid PatientId, FamilyMemberRole Role,
    bool IsActive, DateTime VerifiedOnUtc, Guid VerifiedBySuperAdminUserId, Guid RelationshipRequestId);
public sealed record FamilyMedicalAdminCaseResponse(FamilyMedicalCaseResponse Case, Guid SubmittedByPatientId,
    Guid SubmittedByApplicationUserId, IReadOnlyList<FamilyMedicalRelationshipVerification> RelationshipVerification);
public sealed record FamilyMedicalSummaryCount(FamilyMedicalRequestType RequestType, FamilyMedicalRequestStatus Status, int Count);
public sealed record FamilyMedicalSummaryResponse(IReadOnlyList<FamilyMedicalSummaryCount> Counts);
public sealed record FamilyMedicalEligibilityQueueResponse(Guid ReviewId, Guid FamilyId, Guid AdultPatientId, Guid ChildPatientId,
    FamilyMedicalEligibilityStatus Status, string? PolicyVersion, string RowVersion);
public sealed record FamilyMedicalVerificationReference(Guid FamilyMemberId, Guid RelationshipRequestId, DateTime VerifiedOnUtc);
public sealed record FamilyMedicalEligibilityResponse(Guid ReviewId, Guid FamilyId, Guid AdultPatientId, Guid ChildPatientId,
    FamilyMedicalEligibilityStatus Status, string? PolicyVersion, string? BasisReference, string RowVersion,
    FamilyMedicalVerificationReference AdultVerification, FamilyMedicalVerificationReference ChildVerification,
    bool CanApprove, bool CanReject, bool CanRevoke, string? ReviewReason, IReadOnlyList<FamilyMedicalAdminHistoryResponse> History,
    Guid? AuthorityRelationshipRequestId = null);
public sealed record FamilyMedicalPage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public interface IFamilyMedicalAccessPolicy
{
    Task<Result<MedicalRecordScope>> ScopeAsync(Guid? child, CancellationToken ct);
    Task<Result<FamilyMedicalAccessStatusResponse>> StatusAsync(Guid child, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, FamilyMedicalAccessStatusResponse>> StatusesAsync(Guid family, CancellationToken ct);
    Task<Result<RecordPage<FamilyMedicalDependent>>> DependentsAsync(CancellationToken ct);
}
public sealed record FamilyMedicalCommand(string Operation, Guid? RequestId = null, FamilyMedicalRequestType? RequestType = null,
    Guid? ChildPatientId = null, Guid? AffectedAdultPatientId = null, string? Reason = null, string? RowVersion = null,
    IReadOnlyList<FamilyEvidenceUpload>? Evidence = null, string? IdempotencyKey = null, string? PolicyVersion = null,
    string? BasisReference = null, Guid? AuthorityRelationshipRequestId = null) : ICommand<object>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record FamilyMedicalQuery(string Operation, Guid? Id = null, FamilyMedicalRequestType? RequestType = null,
    int? Status = null, Guid? FamilyId = null, string? Search = null, int PageNumber = 1, int PageSize = 20) : IQuery<object>;
public sealed record FamilyMedicalEvidenceQuery(Guid RequestId, Guid DocumentId, bool Admin) : IQuery<PrivateMedia>;
public interface IFamilyMedicalWorkflow
{
    Task<Result<object>> ExecuteAsync(FamilyMedicalCommand command, CancellationToken ct);
    Task<Result<object>> ReadAsync(FamilyMedicalQuery query, CancellationToken ct);
    Task<Result<PrivateMedia>> EvidenceAsync(FamilyMedicalEvidenceQuery query, CancellationToken ct);
}
internal sealed class FamilyMedicalCommandHandler(IFamilyMedicalWorkflow workflow) : ICommandHandler<FamilyMedicalCommand, object>
{ public Task<Result<object>> Handle(FamilyMedicalCommand request, CancellationToken ct) => workflow.ExecuteAsync(request, ct); }
internal sealed class FamilyMedicalQueryHandler(IFamilyMedicalWorkflow workflow) : IQueryHandler<FamilyMedicalQuery, object>
{ public Task<Result<object>> Handle(FamilyMedicalQuery request, CancellationToken ct) => workflow.ReadAsync(request, ct); }
internal sealed class FamilyMedicalEvidenceQueryHandler(IFamilyMedicalWorkflow workflow) : IQueryHandler<FamilyMedicalEvidenceQuery, PrivateMedia>
{ public Task<Result<PrivateMedia>> Handle(FamilyMedicalEvidenceQuery request, CancellationToken ct) => workflow.EvidenceAsync(request, ct); }
internal sealed class FamilyMedicalCommandValidator : AbstractValidator<FamilyMedicalCommand>
{
    public FamilyMedicalCommandValidator()
    {
        RuleFor(x => x.Operation).Must(o => o is "create" or "withdraw" or "resubmit" or "approve" or "reject" or "request-modification" or "eligibility-approve" or "eligibility-reject" or "eligibility-revoke");
        RuleFor(x => x.RequestId).NotEmpty().When(x => x.Operation != "create");
        RuleFor(x => x.RowVersion).NotEmpty().Must(v => v is not null && RowVersionCodec.IsValid(v)).When(x => x.Operation != "create");
        RuleFor(x => x.RequestType).NotNull().IsInEnum().When(x => x.Operation == "create");
        RuleFor(x => x.ChildPatientId).NotEmpty().When(x => x.Operation == "create");
        RuleFor(x => x.AffectedAdultPatientId).NotEmpty().When(x => x.Operation == "create");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000).When(x => x.Operation is "create" or "resubmit" or "reject" or "request-modification" or "eligibility-reject" or "eligibility-revoke");
        RuleFor(x => x.Reason).MaximumLength(2000);
        RuleFor(x => x.PolicyVersion).NotEmpty().MaximumLength(100).When(x => x.Operation == "eligibility-approve");
        RuleFor(x => x.BasisReference).NotEmpty().MaximumLength(500).When(x => x.Operation == "eligibility-approve");
        RuleFor(x => x.AuthorityRelationshipRequestId).NotEmpty().When(x => x.Operation == "eligibility-approve");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).When(x => x.Operation is "create" or "approve");
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
    }
}
internal sealed class FamilyMedicalQueryValidator : AbstractValidator<FamilyMedicalQuery>
{
    public FamilyMedicalQueryValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.RequestType).IsInEnum().When(x => x.RequestType.HasValue);
        RuleFor(x => x.Status).InclusiveBetween(1, 5).When(x => x.Status.HasValue);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
