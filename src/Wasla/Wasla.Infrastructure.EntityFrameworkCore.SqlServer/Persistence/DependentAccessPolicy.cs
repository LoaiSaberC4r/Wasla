using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class OperationalAuthorityReader(WaslaDbContext db, IDateTimeProvider clock,
    IOptions<DependentManagementOptions> options) : IOperationalAuthorityReader
{
    public async Task<ParentChildOperationalAuthority?> FindAsync(Guid adultPatientId, Guid childPatientId, CancellationToken ct)
    {
        if (!options.Value.Enabled) return null;
        var today = options.Value.Today(clock.UtcNow);
        var cutoff = today.AddYears(-18);
        var proof = await db.Set<ParentChildOperationalAuthority>().AsNoTracking().SingleOrDefaultAsync(p => p.IsActive && p.ParentPatientId == adultPatientId && p.ChildPatientId == childPatientId, ct);
        if (proof is null || !await db.Families.AnyAsync(f => f.Id == proof.FamilyId && f.Status == FamilyStatus.Active, ct)) return null;
        var members = await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == proof.FamilyId && m.IsActive && (m.Id == proof.ParentFamilyMemberId || m.Id == proof.ChildFamilyMemberId)).ToArrayAsync(ct);
        var adult = members.SingleOrDefault(m => m.Id == proof.ParentFamilyMemberId && m.PatientId == adultPatientId && m.Role == proof.ParentRole);
        var child = members.SingleOrDefault(m => m.Id == proof.ChildFamilyMemberId && m.PatientId == childPatientId && m.Role == FamilyMemberRole.Child);
        if (adult is null || child is null || adult.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother or FamilyMemberRole.LegalGuardian)) return null;
        if (!await db.Patients.AnyAsync(p => p.Id == childPatientId && p.DateOfBirth > cutoff && p.DateOfBirth <= today, ct) ||
            !await db.Patients.AnyAsync(p => p.Id == adultPatientId && p.DateOfBirth <= cutoff, ct)) return null;
        var request = await db.FamilyRelationshipRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == proof.ApprovedRelationshipRequestId, ct);
        if (request is null || request.RequestType is not (FamilyRelationshipRequestType.CreateFamily or FamilyRelationshipRequestType.AddFamilyMember or
                FamilyRelationshipRequestType.VerifyParentChild or FamilyRelationshipRequestType.VerifyGuardianChild or
                FamilyRelationshipRequestType.VerifyOperationalParentChild or FamilyRelationshipRequestType.JoinFamilyAsParent) ||
            request.Status != FamilyRelationshipRequestStatus.Approved || request.CurrentRevisionNumber != proof.ApprovedRevisionNumber || request.ReviewedOnUtc != proof.ApprovedAtUtc ||
            request.FamilyId != proof.FamilyId || request.RequesterPatientId != adultPatientId || request.TargetPatientId != childPatientId ||
            request.RequesterClaimedRole != adult.Role || request.TargetClaimedRole != child.Role || request.ReviewedBySuperAdminApplicationUserId != proof.ReviewedByApplicationUserId ||
            !await db.SuperAdmins.AnyAsync(s => s.ApplicationUserId == proof.ReviewedByApplicationUserId &&
                db.ApplicationUsers.Any(u => u.Id == s.ApplicationUserId && u.UserType == UserType.SuperAdmin), ct) ||
            !await db.FamilyRelationshipDocuments.AnyAsync(d => d.FamilyRelationshipRequestId == request.Id && d.RevisionNumber == request.CurrentRevisionNumber && d.FileSize > 0, ct)) return null;
        return proof;
    }
}

internal sealed class DependentAccessPolicy(IWaslaDataStore store, ICurrentUser current, IOperationalAuthorityReader proofs) : IDependentAccessPolicy
{
    public async Task<Result<DependentOperationalScope>> AuthorizeAsync(Guid childPatientId, string permission, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } actor || !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            return Result<DependentOperationalScope>.Fail(DependentAccessErrors.Unavailable);
        var access = await store.GetAccessSnapshotAsync(actor, ct);
        if (access is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient, PatientId: not null } ||
            !access.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return Result<DependentOperationalScope>.Fail(DependentAccessErrors.Unavailable);
        var link = await store.FindPatientAccountLinkAsync(actor, ct);
        if (link is null || link.PatientId != access.PatientId) return Result<DependentOperationalScope>.Fail(DependentAccessErrors.Unavailable);
        var proof = await proofs.FindAsync(link.PatientId, childPatientId, ct);
        return proof is null ? Result<DependentOperationalScope>.Fail(DependentAccessErrors.Unavailable) :
            Result<DependentOperationalScope>.Ok(new(actor, link.PatientId, childPatientId, proof.FamilyId, proof.Id, proof.ParentRole,
                $"{proof.Id:N}:{Convert.ToBase64String(proof.RowVersion)}:{proof.ApprovedRevisionNumber}"));
    }
}

internal sealed class FamilyOperationalVerification(WaslaDbContext db, ITicketQueueLock locks, IPrivateMediaReader media,
    IOptions<DependentManagementOptions> options, IWaslaDataStore store, IPatientIdentityStore identity, ICurrentUser current,
    IDateTimeProvider clock) : IFamilyOperationalVerification
{
    public async Task<Result<IReadOnlyList<OperationalAuthorityResponse>>> ListAsync(Guid relationshipRequestId, CancellationToken ct)
    {
        var permission = Wasla.Domain.Security.FamilySelfServicePermissionNames.RevokeAuthority;
        if (current.UserId is not { } actor || !current.Permissions.Contains(permission)) return Result<IReadOnlyList<OperationalAuthorityResponse>>.Fail(DependentAccessErrors.Unavailable);
        var access = await store.GetAccessSnapshotAsync(actor, ct);
        if (access is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.SuperAdmin, SuperAdminId: not null } ||
            !access.Permissions.Contains(permission) || relationshipRequestId == Guid.Empty) return Result<IReadOnlyList<OperationalAuthorityResponse>>.Fail(DependentAccessErrors.Unavailable);
        var rows = await db.Set<ParentChildOperationalAuthority>().AsNoTracking().Where(p => p.ApprovedRelationshipRequestId == relationshipRequestId).Take(20).ToArrayAsync(ct);
        return Result<IReadOnlyList<OperationalAuthorityResponse>>.Ok(rows.Select(p => new OperationalAuthorityResponse(p.Id, p.FamilyId, p.ParentPatientId,
            p.ChildPatientId, p.IsActive, p.ApprovedRelationshipRequestId, p.ApprovedRevisionNumber, Convert.ToBase64String(p.RowVersion))).ToArray());
    }
    private static byte[]? ConvertVersion(string value) { try { return Convert.FromBase64String(value); } catch (FormatException) { return null; } }
    public async Task<Result> RevokeAsync(Guid authorityId, string rowVersion, string reason, string idempotencyKey, CancellationToken ct)
    {
        var permission = Wasla.Domain.Security.FamilySelfServicePermissionNames.RevokeAuthority;
        if (current.UserId is not { } actor || !current.Permissions.Contains(permission)) return Result.Fail(DependentAccessErrors.Unavailable);
        var user = await store.GetAccessSnapshotAsync(actor, ct);
        if (user is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.SuperAdmin, SuperAdminId: not null } ||
            !user.Permissions.Contains(permission)) return Result.Fail(DependentAccessErrors.Unavailable);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return Result.Fail(FamilyErrors.RequestInvalid);
        await identity.LockAsync(ct);
        var proof = await db.Set<ParentChildOperationalAuthority>().SingleOrDefaultAsync(p => p.Id == authorityId, ct);
        if (proof is null) return Result.Fail(DependentAccessErrors.Unavailable);
        if (!proof.IsActive) return proof.RevokedByApplicationUserId == actor && proof.RevocationIdempotencyKey == idempotencyKey && proof.RevocationReason == reason.Trim() ?
            Result.Ok() : Result.Fail(FamilyErrors.ConcurrencyConflict);
        var version = ConvertVersion(rowVersion);
        if (version is null || !version.AsSpan().SequenceEqual(proof.RowVersion)) return Result.Fail(FamilyErrors.ConcurrencyConflict);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Family:OperationalPair", $"{proof.FamilyId:N}:{proof.ParentPatientId:N}:{proof.ChildPatientId:N}", ct);
        proof.Revoke(actor, clock.UtcNow, reason, idempotencyKey);
        db.Entry(proof).Property(p => p.RowVersion).OriginalValue = version;
        db.Add(new OperationalAuthorityHistory(proof.Id, actor, "Revoked", clock.UtcNow));
        var confirmed = await store.GetAccessSnapshotAsync(actor, ct);
        if (confirmed is not { User.IsActive: true, User.IsFirstLogin: false } || !confirmed.Permissions.Contains(permission)) return Result.Fail(DependentAccessErrors.Unavailable);
        await db.SaveChangesAsync(ct); return Result.Ok();
    }
    public async Task<Result> RecordAsync(FamilyRelationshipRequest request, Family family, CancellationToken ct)
    {
        var adult = family.Members.SingleOrDefault(m => m.IsActive && m.PatientId == request.RequesterPatientId && m.Role == request.RequesterClaimedRole);
        var child = family.Members.SingleOrDefault(m => m.IsActive && m.PatientId == request.TargetPatientId && m.Role == FamilyMemberRole.Child);
        if (adult is null || child is null || adult.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother or FamilyMemberRole.LegalGuardian)) return Result.Ok();
        var documents = await db.FamilyRelationshipDocuments.AsNoTracking().Where(d => d.FamilyRelationshipRequestId == request.Id &&
            d.RevisionNumber == request.CurrentRevisionNumber).ToArrayAsync(ct);
        if (documents.Length == 0 || documents.Length > options.Value.MaxEvidenceFiles || documents.Any(d => d.FileSize <= 0 || d.FileSize > options.Value.MaxEvidenceFileBytes))
            return Result.Fail(DependentAccessErrors.ProofRequired);
        foreach (var document in documents)
        {
            PrivateMedia? opened;
            try { opened = await media.OpenAsync(document.MediaKey, ct); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { opened = null; }
            if (opened is null) return Result.Fail(DependentAccessErrors.ProofRequired);
            await using var stream = opened.Content;
            if (!stream.CanSeek || stream.Length != document.FileSize) return Result.Fail(DependentAccessErrors.ProofRequired);
        }
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Family:OperationalPair", $"{family.Id:N}:{adult.PatientId:N}:{child.PatientId:N}", ct);
        var existing = await db.Set<ParentChildOperationalAuthority>().Where(p => p.FamilyId == family.Id && p.ParentPatientId == adult.PatientId && p.ChildPatientId == child.PatientId).ToArrayAsync(ct);
        if (existing.Any(p => p.ApprovedRelationshipRequestId == request.Id)) return Result.Ok(); // A revoked request is never resurrected.
        foreach (var old in existing.Where(p => p.IsActive))
        { old.Revoke(request.ReviewedBySuperAdminApplicationUserId!.Value, request.ReviewedOnUtc!.Value, "Superseded by reviewed proof"); db.Add(new OperationalAuthorityHistory(old.Id, request.ReviewedBySuperAdminApplicationUserId.Value, "Superseded", request.ReviewedOnUtc.Value)); }
        if (existing.Any(p => p.IsActive == false)) await db.SaveChangesAsync(ct);
        var proof = ParentChildOperationalAuthority.Approve(adult, child, request);
        db.Add(proof); db.Add(new OperationalAuthorityHistory(proof.Id, proof.ReviewedByApplicationUserId, "Approved", proof.ApprovedAtUtc));
        return Result.Ok();
    }
}
