using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyProofReconciler(WaslaDbContext db, IFamilyRelationshipMedicalVerification verification,
    IOptions<FamilyMedicalPolicyOptions> options) : IFamilyProofReconciler
{
    public async Task<FamilyProofReconciliationReport> ReconcileAsync(bool apply, CancellationToken ct)
    {
        // Candidate means an approved request explicitly naming parent and child, never a family-wide cross product.
        var candidates = await db.FamilyRelationshipRequests.Where(r => r.Status == FamilyRelationshipRequestStatus.Approved &&
            (r.RequesterClaimedRole == FamilyMemberRole.Father || r.RequesterClaimedRole == FamilyMemberRole.Mother) &&
            r.TargetClaimedRole == FamilyMemberRole.Child).OrderBy(r => r.Id).ToArrayAsync(ct);
        int safe = 0, already = 0, ambiguous = 0, missing = 0, conflicting = 0, failed = 0;
        var pairs = candidates.GroupBy(r => (r.FamilyId, r.RequesterPatientId, r.TargetPatientId)).ToArray();
        foreach (var pair in pairs)
        {
            if (await db.Set<VerifiedParentChildRelationship>().AnyAsync(r => r.FamilyId == pair.Key.FamilyId &&
                r.ParentPatientId == pair.Key.RequesterPatientId && r.ChildPatientId == pair.Key.TargetPatientId, ct))
            { already++; continue; } // Revoked proofs must never be resurrected by maintenance.
            if (pair.Count() != 1) { ambiguous++; continue; }
            var proof = pair.Single();
            if (proof.RequestType is not (FamilyRelationshipRequestType.CreateFamily or FamilyRelationshipRequestType.AddFamilyMember or FamilyRelationshipRequestType.VerifyParentChild))
            { conflicting++; continue; }
            var family = await db.Families.Include(f => f.Members).SingleOrDefaultAsync(f => f.Id == proof.FamilyId, ct);
            var adult = family?.Members.SingleOrDefault(m => m.PatientId == proof.RequesterPatientId && m.IsActive);
            var child = family?.Members.SingleOrDefault(m => m.PatientId == proof.TargetPatientId && m.IsActive);
            if (family is not { Status: FamilyStatus.Active } || adult?.Role != proof.RequesterClaimedRole || child?.Role != FamilyMemberRole.Child)
            { conflicting++; continue; }
            if (!options.Value.IsConfigured || proof.ReviewedBySuperAdminApplicationUserId is null || proof.ReviewedOnUtc is null)
            { missing++; continue; }
            // The same verifier validates real private media and current approved revision. A dry run rolls back its staged additions.
            var result = await verification.RecordAsync(proof, family, ct);
            var added = db.ChangeTracker.Entries<VerifiedParentChildRelationship>().Where(e => e.State == EntityState.Added).ToArray();
            if (result.IsFailure) { failed++; continue; }
            if (added.Length == 0) { missing++; continue; }
            safe++;
            if (apply) await db.SaveChangesAsync(ct);
            else foreach (var entry in added) entry.State = EntityState.Detached;
        }
        return new(pairs.Length, safe, already, ambiguous, missing, conflicting, failed, apply);
    }
}
