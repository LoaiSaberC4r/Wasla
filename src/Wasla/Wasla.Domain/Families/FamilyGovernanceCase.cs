namespace Wasla.Domain.Families;

public enum FamilyGovernanceCaseKind { IdentityCorrection = 1, MembershipConflict = 2 }
public enum FamilyGovernanceCaseStatus { PendingReview = 1, ModificationRequested = 2, Approved = 3, Rejected = 4, PendingGovernanceDecision = 5 }

public sealed class FamilyGovernanceCase
{
    private FamilyGovernanceCase() { }
    public FamilyGovernanceCase(FamilyGovernanceCaseKind kind, Guid actor, Guid parent, Guid child, Guid? authority, DateTime now)
    {
        if (!Enum.IsDefined(kind) || actor == Guid.Empty || parent == Guid.Empty || child == Guid.Empty || parent == child ||
            kind == FamilyGovernanceCaseKind.IdentityCorrection && authority is null) throw new ArgumentException("Invalid governance case.");
        Id = Guid.NewGuid(); Kind = kind; ActorUserId = actor; ParentPatientId = parent; ChildPatientId = child; OperationalAuthorityId = authority;
        CreatedAtUtc = UpdatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public FamilyGovernanceCaseKind Kind { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid? OperationalAuthorityId { get; private set; }
    public FamilyGovernanceCaseStatus Status { get; private set; } = FamilyGovernanceCaseStatus.PendingReview;
    public int Revision { get; private set; } = 1;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Decide(FamilyGovernanceCaseStatus status, Guid reviewer, DateTime now)
    {
        if (!(Status == FamilyGovernanceCaseStatus.PendingReview || Status == FamilyGovernanceCaseStatus.PendingGovernanceDecision && status == FamilyGovernanceCaseStatus.Rejected) || reviewer == Guid.Empty ||
            status is not (FamilyGovernanceCaseStatus.Approved or FamilyGovernanceCaseStatus.Rejected or FamilyGovernanceCaseStatus.ModificationRequested or FamilyGovernanceCaseStatus.PendingGovernanceDecision) ||
            Kind == FamilyGovernanceCaseKind.MembershipConflict && status == FamilyGovernanceCaseStatus.Approved)
            throw new InvalidOperationException("Governance transition unavailable. Family transfers are not an approved action.");
        Status = status; ReviewedByUserId = reviewer; ReviewedAtUtc = UpdatedAtUtc = now;
    }
    public void Resubmit(Guid? authority, DateTime now)
    {
        if (Status != FamilyGovernanceCaseStatus.ModificationRequested || Kind == FamilyGovernanceCaseKind.IdentityCorrection && authority is null)
            throw new InvalidOperationException("Resubmission unavailable.");
        Revision++; OperationalAuthorityId = authority; Status = FamilyGovernanceCaseStatus.PendingReview;
        ReviewedByUserId = null; ReviewedAtUtc = null; UpdatedAtUtc = now;
    }
}

public sealed class FamilyGovernanceRevision
{
    private FamilyGovernanceRevision() { }
    public FamilyGovernanceRevision(Guid request, int revision, string before, string after, string reason, string? authorityVersion, DateTime now)
    {
        if (request == Guid.Empty || revision < 1 || before.Length > 4000 || after.Length > 4000 || string.IsNullOrWhiteSpace(reason) || reason.Length > 2000 || authorityVersion?.Length > 2000)
            throw new ArgumentException("Invalid governance revision.");
        Id = Guid.NewGuid(); CaseId = request; Revision = revision; BeforeSnapshot = before; AfterSnapshot = after; Reason = reason; AuthorityVersion = authorityVersion; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public int Revision { get; private set; }
    public string BeforeSnapshot { get; private set; } = "";
    public string AfterSnapshot { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string? AuthorityVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
public sealed class FamilyGovernanceEvidence
{
    private FamilyGovernanceEvidence() { }
    public FamilyGovernanceEvidence(Guid request, int revision, FamilyRelationshipDocumentType type, string key, string name, string mime, long size, DateTime now)
    { Id = Guid.NewGuid(); CaseId = request; Revision = revision; DocumentType = type; MediaKey = key; FileName = name; ContentType = mime; FileSize = size; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public int Revision { get; private set; }
    public FamilyRelationshipDocumentType DocumentType { get; private set; }
    public string MediaKey { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long FileSize { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
public sealed class FamilyGovernanceHistory
{
    private FamilyGovernanceHistory() { }
    public FamilyGovernanceHistory(Guid request, int revision, Guid actor, string action, string? reason, DateTime now)
    { Id = Guid.NewGuid(); CaseId = request; Revision = revision; ActorUserId = actor; Action = action; Reason = reason; OccurredAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public int Revision { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = "";
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}
public sealed class FamilyGovernanceOperation
{
    private FamilyGovernanceOperation() { }
    public FamilyGovernanceOperation(Guid actor, Guid request, string operation, string key, string hash, string response, DateTime now)
    { Id = Guid.NewGuid(); ActorUserId = actor; CaseId = request; Operation = operation; IdempotencyKey = key; Fingerprint = hash; ResponseJson = response; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid CaseId { get; private set; }
    public string Operation { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string ResponseJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
}
