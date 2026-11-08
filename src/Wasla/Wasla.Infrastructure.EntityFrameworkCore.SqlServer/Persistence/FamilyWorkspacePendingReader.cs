using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyWorkspacePendingReader(WaslaDbContext db, BuildingBlock.Application.Time.IDateTimeProvider clock) : IFamilyWorkspacePendingReader
{
    public async Task<IReadOnlyList<FamilyWorkspacePendingItem>> ListAsync(Guid actorUserId, CancellationToken ct)
    {
        var onboarding = await db.Set<ChildOnboardingRequest>().AsNoTracking().Where(r => r.ActorApplicationUserId == actorUserId &&
            (r.Status == ChildOnboardingStatus.PendingReview || r.Status == ChildOnboardingStatus.ModificationRequested || r.Status == ChildOnboardingStatus.ConflictReview))
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Take(20).ToArrayAsync(ct);
        var cases = await db.Set<FamilyGovernanceCase>().AsNoTracking().Where(r => r.ActorUserId == actorUserId &&
            (r.Status == FamilyGovernanceCaseStatus.PendingReview || r.Status == FamilyGovernanceCaseStatus.ModificationRequested || r.Status == FamilyGovernanceCaseStatus.PendingGovernanceDecision))
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Take(20).ToArrayAsync(ct);
        var invitations = await db.Set<FamilyJoinInvitation>().AsNoTracking().Where(r => r.IssuedByUserId == actorUserId && r.RevokedAtUtc == null && r.ConsumedAtUtc == null && r.ExpiresAtUtc > clock.UtcNow)
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Take(20).ToArrayAsync(ct);
        return onboarding.Select(r => new FamilyWorkspacePendingItem("ChildOnboarding", r.Id, r.Status.ToString(),
                r.Status == ChildOnboardingStatus.ModificationRequested ? "ReviseSubmittedEvidence" : r.Status == ChildOnboardingStatus.ConflictReview ? "WaitForConfidentialReview" : "WaitForReview", r.CreatedAtUtc))
            .Concat(cases.Select(r => new FamilyWorkspacePendingItem(r.Kind.ToString(), r.Id, r.Status.ToString(),
                r.Status == FamilyGovernanceCaseStatus.ModificationRequested ? "ReviseSubmittedEvidence" : "WaitForConfidentialReview", r.CreatedAtUtc)))
            .Concat(invitations.Select(r => new FamilyWorkspacePendingItem("SecondParentInvitation", r.Id, "PendingRedemption", "SharePrivatelyWithIntendedParent", r.CreatedAtUtc)))
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Reference).ToArray();
    }
}
