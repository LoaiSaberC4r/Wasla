using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private async Task PrepareFamilyMedicalAsync(CancellationToken ct)
    {
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
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
