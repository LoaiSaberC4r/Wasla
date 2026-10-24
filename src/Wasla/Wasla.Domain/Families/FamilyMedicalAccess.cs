using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Families;

public enum FamilyMedicalRequestType { Restriction = 1, Restoration = 2 }
public enum FamilyMedicalRequestStatus { Pending = 1, ModificationRequested = 2, Approved = 3, Rejected = 4, Withdrawn = 5 }
public enum FamilyMedicalRestrictionStatus { Active = 1, Lifted = 2 }
public enum FamilyMedicalEligibilityStatus { PendingReview = 1, Approved = 2, Rejected = 3, Revoked = 4 }
public static class FamilyMedicalErrors
{
    public static Error Unavailable => Error.NotFound("FamilyMedicalAccess.Unavailable", ErrorMessage.GetString("FamilyMedicalAccess.Unavailable"));
    public static Error Invalid => Error.Validation("FamilyMedicalAccess.Invalid", ErrorMessage.GetString("FamilyMedicalAccess.Invalid"));
    public static Error Conflict => Error.Conflict("FamilyMedicalAccess.Conflict", ErrorMessage.GetString("FamilyMedicalAccess.Conflict"));
    public static Error PolicyUnavailable => Error.NotFound("FamilyMedicalAccess.PolicyUnavailable", ErrorMessage.GetString("FamilyMedicalAccess.PolicyUnavailable"));
}

public sealed class FamilyMedicalAccessRequest : AggregateRoot<Guid>
{
    private FamilyMedicalAccessRequest() { }
    public Guid FamilyId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid AffectedAdultPatientId { get; private set; }
    public Guid SubmittedByPatientId { get; private set; }
    public Guid SubmittedByApplicationUserId { get; private set; }
    public FamilyMedicalRequestType RequestType { get; private set; }
    public FamilyMedicalRequestStatus Status { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime? WithdrawnAtUtc { get; private set; }
    public Guid? ReviewedBySuperAdminApplicationUserId { get; private set; }
    public Guid? WithdrawnByApplicationUserId { get; private set; }
    public string? ReviewMessage { get; private set; }
    public Guid? AffectedRestrictionId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsOpen => Status is FamilyMedicalRequestStatus.Pending or FamilyMedicalRequestStatus.ModificationRequested;

    public static Result<FamilyMedicalAccessRequest> Create(Guid family, Guid child, Guid affected, Guid applicant,
        Guid user, FamilyMedicalRequestType type, string reason, DateTime now, Guid? restriction = null)
    {
        if (family == Guid.Empty || child == Guid.Empty || affected == Guid.Empty || applicant == Guid.Empty || user == Guid.Empty ||
            child == affected || !Enum.IsDefined(type) || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000 ||
            (type == FamilyMedicalRequestType.Restriction ? affected == applicant : affected != applicant || restriction is null))
            return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Invalid);
        return Result<FamilyMedicalAccessRequest>.Ok(new() { Id = Guid.NewGuid(), FamilyId = family, ChildPatientId = child,
            AffectedAdultPatientId = affected, SubmittedByPatientId = applicant, SubmittedByApplicationUserId = user,
            RequestType = type, Status = FamilyMedicalRequestStatus.Pending, CurrentRevisionNumber = 1,
            Reason = reason.Trim(), SubmittedAtUtc = now, AffectedRestrictionId = restriction });
    }
    public Result<FamilyMedicalAccessRequest> Transition(string operation, Guid actor, DateTime now, string? message = null, Guid? restriction = null)
    {
        if (actor == Guid.Empty) return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Invalid);
        if (operation is "withdraw" or "resubmit" && actor != SubmittedByApplicationUserId)
            return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Unavailable);
        if (operation == "withdraw" && IsOpen)
        { Status = FamilyMedicalRequestStatus.Withdrawn; WithdrawnAtUtc = now; WithdrawnByApplicationUserId = actor; }
        else if (operation == "resubmit" && Status == FamilyMedicalRequestStatus.ModificationRequested && ValidMessage(message))
        { Status = FamilyMedicalRequestStatus.Pending; CurrentRevisionNumber++; Reason = message!.Trim(); ReviewMessage = null; }
        else if (Status == FamilyMedicalRequestStatus.Pending && operation is "approve" or "reject" or "request-modification")
        {
            if (operation != "approve" && !ValidMessage(message)) return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Invalid);
            if (message?.Length > 2000) return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Invalid);
            Status = operation switch { "approve" => FamilyMedicalRequestStatus.Approved, "reject" => FamilyMedicalRequestStatus.Rejected,
                _ => FamilyMedicalRequestStatus.ModificationRequested };
            ReviewedAtUtc = now; ReviewedBySuperAdminApplicationUserId = actor; ReviewMessage = message?.Trim();
            if (restriction.HasValue) AffectedRestrictionId = restriction;
        }
        else return Result<FamilyMedicalAccessRequest>.Fail(FamilyMedicalErrors.Conflict);
        ModifiedAtUtc = now;
        return Result<FamilyMedicalAccessRequest>.Ok(this);
    }
    private static bool ValidMessage(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 2000;
}

public sealed class FamilyMedicalAccessRestriction : AggregateRoot<Guid>
{
    private FamilyMedicalAccessRestriction() { }
    public Guid FamilyId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid AffectedAdultPatientId { get; private set; }
    public FamilyMedicalRestrictionStatus Status { get; private set; }
    public Guid RestrictionApprovedByRequestId { get; private set; }
    public DateTime RestrictedAtUtc { get; private set; }
    public Guid RestrictedBySuperAdminApplicationUserId { get; private set; }
    public Guid? LiftedByRequestId { get; private set; }
    public DateTime? LiftedAtUtc { get; private set; }
    public Guid? LiftedBySuperAdminApplicationUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static FamilyMedicalAccessRestriction Activate(FamilyMedicalAccessRequest request, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), FamilyId = request.FamilyId, ChildPatientId = request.ChildPatientId,
            AffectedAdultPatientId = request.AffectedAdultPatientId, Status = FamilyMedicalRestrictionStatus.Active,
            RestrictionApprovedByRequestId = request.Id, RestrictedBySuperAdminApplicationUserId = actor, RestrictedAtUtc = now };
    public Result<FamilyMedicalAccessRestriction> Lift(FamilyMedicalAccessRequest request, Guid actor, DateTime now)
    {
        if (Status != FamilyMedicalRestrictionStatus.Active || request.RequestType != FamilyMedicalRequestType.Restoration ||
            request.FamilyId != FamilyId || request.ChildPatientId != ChildPatientId || request.AffectedAdultPatientId != AffectedAdultPatientId || request.AffectedRestrictionId != Id)
            return Result<FamilyMedicalAccessRestriction>.Fail(FamilyMedicalErrors.Conflict);
        Status = FamilyMedicalRestrictionStatus.Lifted; LiftedByRequestId = request.Id; LiftedAtUtc = now; LiftedBySuperAdminApplicationUserId = actor;
        return Result<FamilyMedicalAccessRestriction>.Ok(this);
    }
}

public sealed class FamilyMedicalEligibilityReview : AggregateRoot<Guid>
{
    private FamilyMedicalEligibilityReview() { }
    public Guid FamilyId { get; private set; }
    public Guid AdultPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid AdultFamilyMemberId { get; private set; }
    public Guid ChildFamilyMemberId { get; private set; }
    public FamilyMedicalEligibilityStatus Status { get; private set; }
    public string? PolicyVersion { get; private set; }
    public string? BasisReference { get; private set; }
    public Guid? AuthorityRelationshipRequestId { get; private set; }
    public string? ReviewReason { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static FamilyMedicalEligibilityReview Pending(FamilyMember adult, FamilyMember child, DateTime now)
        => new() { Id = Guid.NewGuid(), FamilyId = adult.FamilyId, AdultPatientId = adult.PatientId, ChildPatientId = child.PatientId,
            AdultFamilyMemberId = adult.Id, ChildFamilyMemberId = child.Id, Status = FamilyMedicalEligibilityStatus.PendingReview, CreatedAtUtc = now };
    public Result<FamilyMedicalEligibilityReview> Decide(string operation, Guid actor, DateTime now, string? version, string? basis, string? reason,
        Guid? authorityRelationshipRequestId = null)
    {
        // Reapproval is an explicit, concurrency-protected administrative review, including policy/provenance renewal.
        if (operation == "revoke" ? Status != FamilyMedicalEligibilityStatus.Approved : operation != "approve" && Status != FamilyMedicalEligibilityStatus.PendingReview)
            return Result<FamilyMedicalEligibilityReview>.Fail(FamilyMedicalErrors.Conflict);
        if (operation == "approve" && (string.IsNullOrWhiteSpace(version) || version.Length > 100 || string.IsNullOrWhiteSpace(basis) || basis.Length > 500) ||
            operation != "approve" && (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000))
            return Result<FamilyMedicalEligibilityReview>.Fail(FamilyMedicalErrors.Invalid);
        if (operation is not ("approve" or "reject" or "revoke")) return Result<FamilyMedicalEligibilityReview>.Fail(FamilyMedicalErrors.Invalid);
        Status = operation switch { "approve" => FamilyMedicalEligibilityStatus.Approved, "reject" => FamilyMedicalEligibilityStatus.Rejected, _ => FamilyMedicalEligibilityStatus.Revoked };
        PolicyVersion = version ?? PolicyVersion; BasisReference = basis ?? BasisReference; ReviewReason = reason;
        if (operation == "approve") AuthorityRelationshipRequestId = authorityRelationshipRequestId;
        ReviewedByApplicationUserId = actor; ReviewedAtUtc = now;
        return Result<FamilyMedicalEligibilityReview>.Ok(this);
    }
}

public sealed class FamilyMedicalAccessRequestDocument : Entity<Guid>
{
    private FamilyMedicalAccessRequestDocument() { }
    public Guid RequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public FamilyRelationshipDocumentType DocumentType { get; private set; }
    public string MediaKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public Guid UploadedByApplicationUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public static FamilyMedicalAccessRequestDocument Create(Guid request, int revision, FamilyRelationshipDocumentType type,
        string key, string name, string mime, long size, string hash, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), RequestId = request, RevisionNumber = revision, DocumentType = type, MediaKey = key,
            FileName = name, ContentType = mime, SizeBytes = size, Sha256 = hash, UploadedByApplicationUserId = actor, UploadedAtUtc = now };
}

// Administrative metadata only. Histories are separate tables and never generic clinical logs.
public abstract class FamilyMedicalHistory : Entity<Guid>
{
    public Guid ParentId { get; protected set; }
    public string Action { get; protected set; } = string.Empty;
    public int? OldStatus { get; protected set; }
    public int NewStatus { get; protected set; }
    public int Revision { get; protected set; }
    public Guid? ActorApplicationUserId { get; protected set; }
    public DateTime OccurredAtUtc { get; protected set; }
    public string? ReasonOrMessage { get; protected set; }
    protected void Initialize(Guid parent, string action, int? old, int next, int revision, Guid? actor, DateTime now, string? message)
    { ParentId = parent; Action = action; OldStatus = old; NewStatus = next; Revision = revision; ActorApplicationUserId = actor; OccurredAtUtc = now; ReasonOrMessage = message; }
}
public sealed class FamilyMedicalAccessRequestHistory : FamilyMedicalHistory
{
    private FamilyMedicalAccessRequestHistory() { }
    public static FamilyMedicalAccessRequestHistory Create(Guid parent, string action, int? old, int next, int revision, Guid? actor, DateTime now, string? message = null)
    { var h = new FamilyMedicalAccessRequestHistory { Id = Guid.NewGuid() }; h.Initialize(parent, action, old, next, revision, actor, now, message); return h; }
}
public sealed class FamilyMedicalAccessRestrictionHistory : FamilyMedicalHistory
{
    private FamilyMedicalAccessRestrictionHistory() { }
    public static FamilyMedicalAccessRestrictionHistory Create(Guid parent, string action, int? old, int next, Guid actor, DateTime now)
    { var h = new FamilyMedicalAccessRestrictionHistory { Id = Guid.NewGuid() }; h.Initialize(parent, action, old, next, 1, actor, now, null); return h; }
}
public sealed class FamilyMedicalEligibilityReviewHistory : FamilyMedicalHistory
{
    private FamilyMedicalEligibilityReviewHistory() { }
    public static FamilyMedicalEligibilityReviewHistory Create(Guid parent, string action, int? old, int next, Guid? actor, DateTime now, string? decisionMetadata = null)
    { var h = new FamilyMedicalEligibilityReviewHistory { Id = Guid.NewGuid() }; h.Initialize(parent, action, old, next, 1, actor, now, decisionMetadata); return h; }
}
