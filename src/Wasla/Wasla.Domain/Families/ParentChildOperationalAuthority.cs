using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Families;

/// <summary>Reviewed operational authority only. Never a clinical sharing grant.</summary>
public sealed class ParentChildOperationalAuthority : Entity<Guid>
{
    private ParentChildOperationalAuthority() { }
    public Guid FamilyId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid ParentFamilyMemberId { get; private set; }
    public Guid ChildFamilyMemberId { get; private set; }
    public FamilyMemberRole ParentRole { get; private set; }
    public Guid ApprovedRelationshipRequestId { get; private set; }
    public int ApprovedRevisionNumber { get; private set; }
    public Guid ReviewedByApplicationUserId { get; private set; }
    public DateTime ApprovedAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? RevokedByApplicationUserId { get; private set; }
    public string? RevocationIdempotencyKey { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ParentChildOperationalAuthority Approve(FamilyMember parent, FamilyMember child, FamilyRelationshipRequest request)
    {
        if (!parent.IsActive || !child.IsActive || parent.FamilyId != child.FamilyId || parent.PatientId == child.PatientId ||
            parent.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother or FamilyMemberRole.LegalGuardian) || child.Role != FamilyMemberRole.Child ||
            request.Status != FamilyRelationshipRequestStatus.Approved || request.FamilyId != parent.FamilyId ||
            request.RequesterPatientId != parent.PatientId || request.TargetPatientId != child.PatientId ||
            request.RequesterClaimedRole != parent.Role || request.TargetClaimedRole != child.Role ||
            request.ReviewedBySuperAdminApplicationUserId is null || request.ReviewedOnUtc is null)
            throw new ArgumentException("A reviewed exact operational relationship is required.");
        return new() { Id = Guid.NewGuid(), FamilyId = parent.FamilyId, ParentPatientId = parent.PatientId,
            ChildPatientId = child.PatientId, ParentFamilyMemberId = parent.Id, ChildFamilyMemberId = child.Id, ParentRole = parent.Role,
            ApprovedRelationshipRequestId = request.Id, ApprovedRevisionNumber = request.CurrentRevisionNumber,
            ReviewedByApplicationUserId = request.ReviewedBySuperAdminApplicationUserId.Value,
            ApprovedAtUtc = request.ReviewedOnUtc.Value, IsActive = true };
    }

    public void Revoke(Guid actor, DateTime now, string reason, string? idempotencyKey = null)
    {
        if (!IsActive || actor == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("An active authority and bounded revocation reason are required.");
        IsActive = false; RevokedAtUtc = now; RevocationReason = reason.Trim(); RevokedByApplicationUserId = actor; RevocationIdempotencyKey = idempotencyKey;
    }
}

public sealed class OperationalAuthorityHistory : Entity<Guid>
{
    private OperationalAuthorityHistory() { }
    public OperationalAuthorityHistory(Guid authority, Guid actor, string action, DateTime now)
    { Id = Guid.NewGuid(); AuthorityId = authority; ActorApplicationUserId = actor; Action = action; OccurredAtUtc = now; }
    public Guid AuthorityId { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
}
