using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private async Task PrepareFamilyMedicalAsync(CancellationToken ct)
    {
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            if (e.Entity is FamilyJoinInvitation && (e.State == EntityState.Deleted || e.Property("ConsumedAtUtc").OriginalValue is not null || e.Property("RevokedAtUtc").OriginalValue is not null ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ConsumedAtUtc" or "RedeemedByUserId" or "RedemptionKey" or "RedemptionFingerprint" or "RelationshipRequestId" or "GovernanceCaseId" or "RevokedAtUtc" or "RevocationKey"))))
                throw new InvalidOperationException("Invitation provenance is immutable and redemption is one-use.");
            if (e.Entity is FamilyGovernanceRevision or FamilyGovernanceEvidence or FamilyGovernanceHistory or FamilyGovernanceOperation)
                throw new InvalidOperationException("Family governance history and evidence are immutable.");
            if (e.Entity is FamilyGovernanceCase && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is "Kind" or "ActorUserId" or "ParentPatientId" or "ChildPatientId" or "CreatedAtUtc")))
                throw new InvalidOperationException("Family governance provenance is immutable.");
            if (e.Entity is OperationalAuthorityHistory or ChildOnboardingHistory or ChildOnboardingEvidence or ChildOnboardingRevision)
                throw new InvalidOperationException("Operational authority history is immutable.");
            if (e.Entity is ParentChildOperationalAuthority && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("IsActive" or "RevokedAtUtc" or "RevocationReason" or "RevokedByApplicationUserId" or "RevocationIdempotencyKey")) ||
                e.Property("IsActive").OriginalValue is false))
                throw new InvalidOperationException("Operational authority provenance is immutable; revoke and review a new proof.");
            if (e.Entity is FamilyCandidateSelection && (e.State == EntityState.Deleted || e.Property("ConsumedAtUtc").OriginalValue is not null ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ConsumedAtUtc" or "RelationshipRequestId" or "GovernanceCaseId" or "IdempotencyKey" or "SubmissionFingerprint"))))
                throw new InvalidOperationException("Candidate provenance is immutable and redemption is one-use.");
            if (e.Entity is ChildOnboardingRequest && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is "ActorApplicationUserId" or "ParentPatientId" or "ParentRole" or "Fingerprint" or "IdempotencyKey" or "CreatedAtUtc")))
                throw new InvalidOperationException("Private child intake is retained and immutable.");
            if (e.Entity is ChildOnboardingRequest intake && e.Properties.Any(p => p.IsModified && p.Metadata.Name is "NameAr" or "NameEn" or "DateOfBirth" or "Gender" or "PhoneNumber" or "ContactNameAr" or "ContactPhone" or "IntentHash") &&
                !(e.Property("Status").OriginalValue is ChildOnboardingStatus.ModificationRequested && intake.Status == ChildOnboardingStatus.PendingReview &&
                    e.Property("Revision").OriginalValue is int previous && intake.Revision == previous + 1 && ChangeTracker.Entries<ChildOnboardingRevision>().Any(v => v.Entity.RequestId == intake.Id && v.Entity.Revision == previous)))
                throw new InvalidOperationException("Intake corrections require a retained revision and requested resubmission.");
            if (e.Entity is VerifiedParentChildRelationship && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Status" or "RevokedAtUtc"))))
                throw new InvalidOperationException("Verified relationship provenance is immutable; revoke and review a new proof.");
            if (e.Entity is FamilyMedicalHistory or FamilyMedicalAccessRequestDocument)
                throw new InvalidOperationException("Family medical history and evidence are immutable.");
            if (e.Entity is FamilyMedicalAccessRequest or FamilyMedicalAccessRestriction or FamilyMedicalEligibilityReview)
            {
                if (e.State == EntityState.Deleted) throw new InvalidOperationException("Family medical cases cannot be deleted.");
                if (e.Properties.Any(p => p.IsModified && p.Metadata.Name is "FamilyId" or "ChildPatientId" or "AdultPatientId" or "AffectedAdultPatientId" or
                    "SubmittedByPatientId" or "SubmittedByApplicationUserId" or "AdultFamilyMemberId" or "ChildFamilyMemberId" or "RequestType"))
                    throw new InvalidOperationException("Family medical relationship scope is immutable.");
            }
        }
        var added = ChangeTracker.Entries<FamilyMember>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        if (added.Length == 0) return;
        var ids = added.Select(m => m.FamilyId).Distinct().ToArray();
        var members = (await FamilyMembers.AsNoTracking().Where(m => ids.Contains(m.FamilyId) && m.IsActive).ToArrayAsync(ct))
            .Concat(added).DistinctBy(m => m.Id).ToArray();
        var existing = await Set<FamilyMedicalEligibilityReview>().AsNoTracking().Where(r => ids.Contains(r.FamilyId)).Select(r => new { r.FamilyId, r.AdultPatientId, r.ChildPatientId }).ToArrayAsync(ct);
        var known = existing.Select(r => (r.FamilyId, r.AdultPatientId, r.ChildPatientId)).ToHashSet();
        foreach (var r in ChangeTracker.Entries<FamilyMedicalEligibilityReview>().Select(e => e.Entity)) known.Add((r.FamilyId, r.AdultPatientId, r.ChildPatientId));
        foreach (var adult in members.Where(m => m.IsActive && m.Role is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian))
        foreach (var child in members.Where(m => m.FamilyId == adult.FamilyId && m.Role == FamilyMemberRole.Child))
        {
            if (!known.Add((adult.FamilyId, adult.PatientId, child.PatientId))) continue;
            var r = FamilyMedicalEligibilityReview.Pending(adult, child, child.JoinedOnUtc > adult.JoinedOnUtc ? child.JoinedOnUtc : adult.JoinedOnUtc);
            Set<FamilyMedicalEligibilityReview>().Add(r);
            Set<FamilyMedicalEligibilityReviewHistory>().Add(FamilyMedicalEligibilityReviewHistory.Create(r.Id, "CandidateCreated", null, (int)r.Status, null, r.CreatedAtUtc));
        }
    }
}
