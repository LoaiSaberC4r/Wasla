using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Families;

public enum FamilyRelationshipRequestType
{
    CreateFamily = 1,
    AddFamilyMember = 2
}

public enum FamilyRelationshipRequestStatus
{
    Pending = 1,
    ModificationRequested = 2,
    Approved = 3,
    Rejected = 4
}

public enum FamilyRelationshipRequestAction
{
    Submitted = 1,
    ModificationRequested = 2,
    Resubmitted = 3,
    Approved = 4,
    Rejected = 5
}

public enum FamilyRelationshipDocumentType
{
    RequesterNationalIdFront = 1,
    RequesterNationalIdBack = 2,
    TargetNationalIdFront = 3,
    TargetNationalIdBack = 4,
    BirthCertificate = 5,
    GuardianshipDocument = 6,
    Other = 7
}

public sealed class FamilyRelationshipRequest : AggregateRoot<Guid>, IAuditableEntity
{
    private FamilyRelationshipRequest()
    {
    }

    private FamilyRelationshipRequest(
        Guid id,
        FamilyRelationshipRequestType requestType,
        Guid? familyId,
        Guid requesterPatientId,
        Guid targetPatientId,
        FamilyMemberRole requesterClaimedRole,
        FamilyMemberRole targetClaimedRole,
        Guid submittedByApplicationUserId,
        DateTime submittedOnUtc)
        : base(id)
    {
        RequestType = requestType;
        FamilyId = familyId;
        RequesterPatientId = requesterPatientId;
        TargetPatientId = targetPatientId;
        RequesterClaimedRole = requesterClaimedRole;
        TargetClaimedRole = targetClaimedRole;
        Status = FamilyRelationshipRequestStatus.Pending;
        CurrentRevisionNumber = 1;
        SubmittedByApplicationUserId = submittedByApplicationUserId;
        SubmittedOnUtc = RequireUtc(submittedOnUtc);
    }

    public FamilyRelationshipRequestType RequestType { get; private set; }
    public Guid? FamilyId { get; private set; }
    public Guid RequesterPatientId { get; private set; }
    public Guid TargetPatientId { get; private set; }
    public FamilyMemberRole RequesterClaimedRole { get; private set; }
    public FamilyMemberRole TargetClaimedRole { get; private set; }
    public FamilyRelationshipRequestStatus Status { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public Guid SubmittedByApplicationUserId { get; private set; }
    public DateTime SubmittedOnUtc { get; private set; }
    public Guid? ReviewedBySuperAdminApplicationUserId { get; private set; }
    public DateTime? ReviewedOnUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ModificationMessage { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<FamilyRelationshipRequest> Create(
        Guid id,
        FamilyRelationshipRequestType requestType,
        Guid? familyId,
        Guid requesterPatientId,
        Guid targetPatientId,
        FamilyMemberRole requesterClaimedRole,
        FamilyMemberRole targetClaimedRole,
        Guid submittedByApplicationUserId,
        DateTime submittedOnUtc)
    {
        var familyShapeValid = requestType switch
        {
            FamilyRelationshipRequestType.CreateFamily => familyId is null,
            FamilyRelationshipRequestType.AddFamilyMember => familyId is not null && familyId != Guid.Empty,
            _ => false
        };
        if (id == Guid.Empty || requesterPatientId == Guid.Empty || targetPatientId == Guid.Empty ||
            requesterPatientId == targetPatientId || submittedByApplicationUserId == Guid.Empty ||
            !familyShapeValid || !Enum.IsDefined(requesterClaimedRole) || !Enum.IsDefined(targetClaimedRole) ||
            requesterClaimedRole == FamilyMemberRole.Child)
        {
            return Result<FamilyRelationshipRequest>.Fail(FamilyErrors.RequestInvalid);
        }

        return Result<FamilyRelationshipRequest>.Ok(new FamilyRelationshipRequest(
            id, requestType, familyId, requesterPatientId, targetPatientId, requesterClaimedRole,
            targetClaimedRole, submittedByApplicationUserId, submittedOnUtc));
    }

    public Result RequestModification(string message, Guid actorId, DateTime occurredOnUtc)
    {
        var normalized = NormalizeReason(message);
        if (normalized is null)
        {
            return Result.Fail(FamilyErrors.ModificationRequired);
        }

        var transition = BeginReview(actorId, occurredOnUtc);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = FamilyRelationshipRequestStatus.ModificationRequested;
        ModificationMessage = normalized;
        return Result.Ok();
    }

    public Result Resubmit(Guid actorId, DateTime occurredOnUtc)
    {
        if (Status != FamilyRelationshipRequestStatus.ModificationRequested)
        {
            return Result.Fail(IsFinal ? FamilyErrors.RequestAlreadyFinalized : FamilyErrors.RequestInvalidState);
        }

        CurrentRevisionNumber++;
        Status = FamilyRelationshipRequestStatus.Pending;
        SubmittedByApplicationUserId = actorId;
        SubmittedOnUtc = RequireUtc(occurredOnUtc);
        ReviewedBySuperAdminApplicationUserId = null;
        ReviewedOnUtc = null;
        return Result.Ok();
    }

    public Result Approve(Guid actorId, DateTime occurredOnUtc)
    {
        var transition = BeginReview(actorId, occurredOnUtc);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = FamilyRelationshipRequestStatus.Approved;
        return Result.Ok();
    }

    public Result AssignCreatedFamily(Guid familyId)
    {
        if (RequestType != FamilyRelationshipRequestType.CreateFamily || Status != FamilyRelationshipRequestStatus.Pending ||
            FamilyId.HasValue || familyId == Guid.Empty)
        {
            return Result.Fail(FamilyErrors.RequestInvalidState);
        }

        FamilyId = familyId;
        return Result.Ok();
    }

    public Result Reject(string reason, Guid actorId, DateTime occurredOnUtc)
    {
        var normalized = NormalizeReason(reason);
        if (normalized is null)
        {
            return Result.Fail(FamilyErrors.RejectionReasonRequired);
        }

        var transition = BeginReview(actorId, occurredOnUtc);
        if (transition.IsFailure)
        {
            return transition;
        }

        Status = FamilyRelationshipRequestStatus.Rejected;
        RejectionReason = normalized;
        return Result.Ok();
    }

    private bool IsFinal => Status is FamilyRelationshipRequestStatus.Approved or FamilyRelationshipRequestStatus.Rejected;

    private Result BeginReview(Guid actorId, DateTime occurredOnUtc)
    {
        if (Status != FamilyRelationshipRequestStatus.Pending)
        {
            return Result.Fail(IsFinal ? FamilyErrors.RequestAlreadyFinalized : FamilyErrors.RequestInvalidState);
        }

        if (actorId == Guid.Empty)
        {
            return Result.Fail(FamilyErrors.RequestInvalid);
        }

        ReviewedBySuperAdminApplicationUserId = actorId;
        ReviewedOnUtc = RequireUtc(occurredOnUtc);
        return Result.Ok();
    }

    private static string? NormalizeReason(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 2000 ? null : normalized;
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class FamilyRelationshipDocument : Entity<Guid>
{
    private FamilyRelationshipDocument()
    {
    }

    public FamilyRelationshipDocument(Guid id, Guid familyRelationshipRequestId, int revisionNumber, FamilyRelationshipDocumentType documentType, string mediaKey, string originalFileName, string contentType, long fileSize, Guid uploadedByApplicationUserId, DateTime uploadedOnUtc)
        : base(id)
    {
        if (id == Guid.Empty || familyRelationshipRequestId == Guid.Empty || revisionNumber < 1 || !Enum.IsDefined(documentType) ||
            string.IsNullOrWhiteSpace(mediaKey) || mediaKey.Length > 1000 || string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Length > 500 || string.IsNullOrWhiteSpace(contentType) || contentType.Length > 200 ||
            fileSize <= 0 || uploadedByApplicationUserId == Guid.Empty)
        {
            throw new ArgumentException("Invalid family relationship document.");
        }

        FamilyRelationshipRequestId = familyRelationshipRequestId;
        RevisionNumber = revisionNumber;
        DocumentType = documentType;
        MediaKey = mediaKey.Trim();
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim();
        FileSize = fileSize;
        UploadedByApplicationUserId = uploadedByApplicationUserId;
        UploadedOnUtc = RequireUtc(uploadedOnUtc);
    }

    public Guid FamilyRelationshipRequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public FamilyRelationshipDocumentType DocumentType { get; private set; }
    public string MediaKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public Guid UploadedByApplicationUserId { get; private set; }
    public DateTime UploadedOnUtc { get; private set; }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class FamilyRelationshipRequestHistory : Entity<Guid>
{
    private FamilyRelationshipRequestHistory()
    {
    }

    public FamilyRelationshipRequestHistory(Guid id, Guid requestId, int revisionNumber, FamilyRelationshipRequestStatus? oldStatus, FamilyRelationshipRequestStatus newStatus, FamilyRelationshipRequestAction action, string? messageOrReason, Guid performedByApplicationUserId, DateTime performedOnUtc)
        : base(id)
    {
        var normalized = string.IsNullOrWhiteSpace(messageOrReason) ? null : messageOrReason.Trim();
        if (id == Guid.Empty || requestId == Guid.Empty || revisionNumber < 1 || !Enum.IsDefined(newStatus) ||
            !Enum.IsDefined(action) || normalized?.Length > 2000 || performedByApplicationUserId == Guid.Empty)
        {
            throw new ArgumentException("Invalid family relationship request history.");
        }

        RequestId = requestId;
        RevisionNumber = revisionNumber;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Action = action;
        MessageOrReason = normalized;
        PerformedByApplicationUserId = performedByApplicationUserId;
        PerformedOnUtc = RequireUtc(performedOnUtc);
    }

    public Guid RequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public FamilyRelationshipRequestStatus? OldStatus { get; private set; }
    public FamilyRelationshipRequestStatus NewStatus { get; private set; }
    public FamilyRelationshipRequestAction Action { get; private set; }
    public string? MessageOrReason { get; private set; }
    public Guid PerformedByApplicationUserId { get; private set; }
    public DateTime PerformedOnUtc { get; private set; }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
