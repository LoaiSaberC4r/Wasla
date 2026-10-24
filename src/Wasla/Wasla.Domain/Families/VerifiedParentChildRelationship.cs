using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Families;

public enum ParentChildVerificationStatus { Active = 1, Revoked = 2 }

// Evidence provenance for one pair. Membership alone never creates this entitlement.
public sealed class VerifiedParentChildRelationship : Entity<Guid>
{
    private VerifiedParentChildRelationship() { }
    public Guid FamilyId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid ParentFamilyMemberId { get; private set; }
    public Guid ChildFamilyMemberId { get; private set; }
    public FamilyMemberRole ParentRole { get; private set; }
    public Guid ApprovedRelationshipRequestId { get; private set; }
    public int ApprovedRevisionNumber { get; private set; }
    public Guid VerifiedBySuperAdminApplicationUserId { get; private set; }
    public DateTime VerifiedAtUtc { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public ParentChildVerificationStatus Status { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static VerifiedParentChildRelationship Verify(FamilyMember parent, FamilyMember child,
        FamilyRelationshipRequest proof, string policyVersion)
    {
        if (!parent.IsActive || !child.IsActive || parent.FamilyId != child.FamilyId || parent.PatientId == child.PatientId ||
            parent.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) || child.Role != FamilyMemberRole.Child ||
            proof.Status != FamilyRelationshipRequestStatus.Approved || proof.FamilyId != parent.FamilyId ||
            proof.RequesterPatientId != parent.PatientId || proof.TargetPatientId != child.PatientId ||
            proof.RequesterClaimedRole != parent.Role || proof.TargetClaimedRole != child.Role ||
            proof.ReviewedBySuperAdminApplicationUserId is null || proof.ReviewedOnUtc is null || string.IsNullOrWhiteSpace(policyVersion) || policyVersion.Length > 100)
            throw new ArgumentException("An approved exact parent-child proof is required.");
        return new() { Id = Guid.NewGuid(), FamilyId = parent.FamilyId, ParentPatientId = parent.PatientId,
            ChildPatientId = child.PatientId, ParentFamilyMemberId = parent.Id, ChildFamilyMemberId = child.Id,
            ParentRole = parent.Role, ApprovedRelationshipRequestId = proof.Id, ApprovedRevisionNumber = proof.CurrentRevisionNumber,
            VerifiedBySuperAdminApplicationUserId = proof.ReviewedBySuperAdminApplicationUserId.Value,
            VerifiedAtUtc = proof.ReviewedOnUtc.Value, PolicyVersion = policyVersion, Status = ParentChildVerificationStatus.Active };
    }

    public void Revoke(DateTime now)
    {
        if (Status != ParentChildVerificationStatus.Active) throw new InvalidOperationException("Verification is already revoked.");
        Status = ParentChildVerificationStatus.Revoked; RevokedAtUtc = now;
    }
}
