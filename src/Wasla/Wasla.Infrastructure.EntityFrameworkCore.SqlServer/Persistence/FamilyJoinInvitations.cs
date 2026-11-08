using System.Security.Cryptography;
using System.Text;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyJoinInvitations(WaslaDbContext db, IWaslaDataStore store, ICurrentUser current,
    IDependentAccessPolicy access, IOperationalAuthorityReader proofs, IPatientIdentityStore identities, IDateTimeProvider clock,
    IOptions<DependentManagementOptions> options, IDataProtectionProvider protection, FamilyRelationshipWorkflowService workflow,
    IFamilyGovernanceWorkflow governance) : IFamilyJoinInvitations
{
    private static Error Missing => DependentAccessErrors.Unavailable;
    private static Error Conflict => Error.Conflict("FamilyInvitation.Unavailable", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyInvitation_Unavailable"));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private IDataProtector Protector => protection.CreateProtector("Wasla.FamilyJoinInvitation.Response.v1");
    private static bool Valid(string key, FamilyMemberRole role) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100 && role is FamilyMemberRole.Father or FamilyMemberRole.Mother;
    public async Task<Result<FamilyJoinInvitationResponse>> CreateAsync(CreateFamilyJoinInvitationCommand c, CancellationToken ct)
    {
        if (!Valid(c.IdempotencyKey, c.IntendedParentRole)) return Result<FamilyJoinInvitationResponse>.Fail(Conflict);
        var scope = await access.AuthorizeAsync(c.ChildPatientId, FamilyGovernancePermissionNames.InviteCreate, ct);
        if (scope.IsFailure || scope.Value.ParentRole is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) || scope.Value.ParentRole == c.IntendedParentRole)
            return Result<FamilyJoinInvitationResponse>.Fail(Missing);
        await identities.LockAsync(ct);
        var fingerprint = Hash($"{c.ChildPatientId:N}:{c.IntendedParentRole}");
        var previous = await db.Set<FamilyJoinInvitation>().AsNoTracking().SingleOrDefaultAsync(i => i.IssuedByUserId == scope.Value.ActorUserId && i.IdempotencyKey == c.IdempotencyKey, ct);
        if (previous is not null)
        {
            if (previous.Fingerprint != fingerprint || previous.ExpiresAtUtc <= clock.UtcNow || previous.ConsumedAtUtc.HasValue || previous.RevokedAtUtc.HasValue)
                return Result<FamilyJoinInvitationResponse>.Fail(Conflict);
            try { return Result<FamilyJoinInvitationResponse>.Ok(new(Protector.Unprotect(previous.ProtectedReference), previous.ExpiresAtUtc, "SharePrivatelyWithIntendedParent")); }
            catch (CryptographicException) { return Result<FamilyJoinInvitationResponse>.Fail(Conflict); }
        }
        var member = await store.FindActiveFamilyMemberByPatientIdAsync(scope.Value.AdultPatientId, ct);
        if (member is null || !member.CanManageFamily || await db.FamilyMembers.AnyAsync(m => m.FamilyId == scope.Value.FamilyId && m.IsActive && m.Role == c.IntendedParentRole, ct) ||
            await db.Set<FamilyJoinInvitation>().CountAsync(i => i.IssuedByUserId == scope.Value.ActorUserId && i.CreatedAtUtc >= clock.UtcNow.Date, ct) >= 10)
            return Result<FamilyJoinInvitationResponse>.Fail(Conflict);
        var reference = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var expires = clock.UtcNow.AddMinutes(30);
        var row = new FamilyJoinInvitation(scope.Value.ActorUserId, scope.Value.AdultPatientId, c.ChildPatientId, scope.Value.FamilyId,
            scope.Value.AuthorityId, c.IntendedParentRole, Hash(reference), c.IdempotencyKey, fingerprint, Protector.Protect(reference), clock.UtcNow, expires);
        if ((await access.AuthorizeAsync(c.ChildPatientId, FamilyGovernancePermissionNames.InviteCreate, ct)).IsFailure)
            return Result<FamilyJoinInvitationResponse>.Fail(Missing);
        db.Add(row); await db.SaveChangesAsync(ct);
        return Result<FamilyJoinInvitationResponse>.Ok(new(reference, expires, "SharePrivatelyWithIntendedParent"));
    }
    public async Task<Result<object>> RevokeAsync(RevokeFamilyJoinInvitationCommand c, CancellationToken ct)
    {
        var actor = await FamilySelfServiceActor.ResolveAsync(store, current, FamilyGovernancePermissionNames.InviteCreate, clock, options.Value, ct);
        if (actor.IsFailure) return Result<object>.Fail(actor.Errors);
        if (string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 100 || c.InvitationReference is not { Length: 64 } || !c.InvitationReference.All(Uri.IsHexDigit)) return Result<object>.Fail(Conflict);
        await identities.LockAsync(ct); var hash = Hash(c.InvitationReference);
        var row = await db.Set<FamilyJoinInvitation>().SingleOrDefaultAsync(i => i.TokenHash == hash && i.IssuedByUserId == current.UserId, ct);
        if (row is null) return Result<object>.Fail(Missing);
        if (row.RevokedAtUtc.HasValue) return row.RevocationKey == c.IdempotencyKey ? Result<object>.Ok(new { revoked = true }) : Result<object>.Fail(Conflict);
        if (row.ConsumedAtUtc.HasValue || await db.Set<FamilyJoinInvitation>().AnyAsync(i => i.IssuedByUserId == current.UserId && i.RevocationKey == c.IdempotencyKey && i.Id != row.Id, ct)) return Result<object>.Fail(Conflict);
        row.Revoke(c.IdempotencyKey, clock.UtcNow); await db.SaveChangesAsync(ct); return Result<object>.Ok(new { revoked = true });
    }
    private async Task<bool> IssuerCurrentAsync(FamilyJoinInvitation invitation, CancellationToken ct)
    {
        var issuer = await store.GetAccessSnapshotAsync(invitation.IssuedByUserId, ct);
        var link = await store.FindPatientAccountLinkAsync(invitation.IssuedByUserId, ct);
        var member = await store.FindActiveFamilyMemberByPatientIdAsync(invitation.IssuerPatientId, ct);
        var proof = await proofs.FindAsync(invitation.IssuerPatientId, invitation.ChildPatientId, ct);
        var today = options.Value.Today(clock.UtcNow);
        return issuer is { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient } &&
            issuer.PatientId == invitation.IssuerPatientId && link?.PatientId == invitation.IssuerPatientId &&
            issuer.Permissions.Contains(FamilyGovernancePermissionNames.InviteCreate) && member is { CanManageFamily: true } && member.FamilyId == invitation.FamilyId &&
            proof?.Id == invitation.OperationalAuthorityId && proof.ParentRole != invitation.IntendedRole && proof.ParentRole is FamilyMemberRole.Father or FamilyMemberRole.Mother &&
            await db.Patients.AnyAsync(p => p.Id == invitation.ChildPatientId && p.DateOfBirth > today.AddYears(-18) && p.DateOfBirth <= today, ct) &&
            await db.Patients.AnyAsync(p => p.Id == invitation.IssuerPatientId && p.DateOfBirth <= today.AddYears(-18), ct);
    }
    public async Task<Result<FamilyCandidateSubmissionResponse>> RedeemAsync(RedeemFamilyJoinInvitationCommand c, CancellationToken ct)
    {
        var parent = await FamilySelfServiceActor.ResolveAsync(store, current, FamilyGovernancePermissionNames.InviteRedeem, clock, options.Value, ct);
        if (parent.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(parent.Errors);
        if (!Valid(c.IdempotencyKey, c.IntendedParentRole) || c.InvitationReference is not { Length: 64 } || !c.InvitationReference.All(Uri.IsHexDigit) ||
            c.Evidence.Count < 1 || c.Evidence.Count > options.Value.MaxEvidenceFiles || c.Evidence.Any(e => !Enum.IsDefined(e.DocumentType) ||
            !e.File.Content.CanSeek || e.File.Length <= 0 || e.File.Length > options.Value.MaxEvidenceFileBytes)) return Result<FamilyCandidateSubmissionResponse>.Fail(Conflict);
        var snapshot = await store.GetAccessSnapshotAsync(current.UserId!.Value, ct);
        if (!current.Permissions.Contains(FamilySelfServicePermissionNames.Submit) || snapshot?.Permissions.Contains(FamilySelfServicePermissionNames.Submit) != true)
            return Result<FamilyCandidateSubmissionResponse>.Fail(Missing);
        var parts = new List<string> { c.IntendedParentRole.ToString(), c.InvitationReference };
        foreach (var file in c.Evidence)
        {
            file.File.Content.Position = 0; parts.Add($"{file.DocumentType}:{Path.GetFileName(file.File.FileName)}:{Convert.ToHexString(await SHA256.HashDataAsync(file.File.Content, ct))}");
            file.File.Content.Position = 0;
        }
        var fingerprint = Hash(string.Join('|', parts)); var tokenHash = Hash(c.InvitationReference);
        await identities.LockAsync(ct);
        var row = await db.Set<FamilyJoinInvitation>().SingleOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
        if (row is null || row.RevokedAtUtc.HasValue || row.IssuedByUserId == current.UserId || row.IntendedRole != c.IntendedParentRole || !await IssuerCurrentAsync(row, ct))
            return Result<FamilyCandidateSubmissionResponse>.Fail(Missing);
        if (row.ConsumedAtUtc.HasValue)
            return row.RedeemedByUserId == current.UserId && row.RedemptionKey == c.IdempotencyKey && row.RedemptionFingerprint == fingerprint
                ? Result<FamilyCandidateSubmissionResponse>.Ok(new(row.RelationshipRequestId, row.GovernanceCaseId.HasValue ? "ConflictReview" : "PendingReview", row.GovernanceCaseId, "WaitForReview"))
                : Result<FamilyCandidateSubmissionResponse>.Fail(Conflict);
        if (row.ExpiresAtUtc <= clock.UtcNow || await db.Set<FamilyJoinInvitation>().AnyAsync(i => i.RedeemedByUserId == current.UserId && i.RedemptionKey == c.IdempotencyKey, ct) ||
            await db.FamilyMembers.AnyAsync(m => m.FamilyId == row.FamilyId && m.IsActive && m.Role == c.IntendedParentRole, ct)) return Result<FamilyCandidateSubmissionResponse>.Fail(Conflict);
        var member = await store.FindActiveFamilyMemberByPatientIdAsync(parent.Value.Id, ct);
        Guid? requestId = null; Guid? reviewId = null;
        if (member is not null && member.FamilyId != row.FamilyId)
        {
            var review = await governance.OpenConflictAsync(current.UserId.Value, parent.Value.Id, row.ChildPatientId, c.IntendedParentRole, c.Evidence, c.IdempotencyKey, fingerprint, ct);
            if (review.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(review.Errors);
            reviewId = review.Value.CaseId;
        }
        else
        {
            var type = member is null ? FamilyRelationshipRequestType.JoinFamilyAsParent : FamilyRelationshipRequestType.VerifyOperationalParentChild;
            var submitted = await workflow.SubmitAsync(type, row.FamilyId, parent.Value.Id, row.ChildPatientId, c.IntendedParentRole,
                FamilyMemberRole.Child, c.Evidence, current.UserId.Value, ct);
            if (submitted.IsFailure) return Result<FamilyCandidateSubmissionResponse>.Fail(submitted.Errors);
            requestId = submitted.Value.RequestId;
        }
        if (!await IssuerCurrentAsync(row, ct) || (await FamilySelfServiceActor.ResolveAsync(store, current, FamilyGovernancePermissionNames.InviteRedeem, clock, options.Value, ct)).IsFailure)
            return Result<FamilyCandidateSubmissionResponse>.Fail(Missing);
        row.Consume(current.UserId.Value, c.IdempotencyKey, fingerprint, requestId, reviewId, clock.UtcNow); await db.SaveChangesAsync(ct);
        return Result<FamilyCandidateSubmissionResponse>.Ok(new(requestId, reviewId.HasValue ? "ConflictReview" : "PendingReview", reviewId, "WaitForReview"));
    }
}
