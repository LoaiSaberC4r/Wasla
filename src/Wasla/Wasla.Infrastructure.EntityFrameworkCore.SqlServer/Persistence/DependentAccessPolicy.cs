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
        => (await FindManyAsync(adultPatientId, [childPatientId], ct)).GetValueOrDefault(childPatientId);

    public async Task<IReadOnlyDictionary<Guid, ParentChildOperationalAuthority>> FindManyAsync(Guid adultPatientId,
        IReadOnlyCollection<Guid> children, CancellationToken ct)
    {
        if (!options.Value.Enabled || children.Count == 0) return new Dictionary<Guid, ParentChildOperationalAuthority>();
        var today = options.Value.Today(clock.UtcNow); var cutoff = today.AddYears(-18);
        // One statement evaluates every live pair and its immutable reviewed source.
        return await db.Set<ParentChildOperationalAuthority>().AsNoTracking().Where(p => p.IsActive && p.ParentPatientId == adultPatientId && children.Contains(p.ChildPatientId) &&
            (p.ParentRole == FamilyMemberRole.Father || p.ParentRole == FamilyMemberRole.Mother || p.ParentRole == FamilyMemberRole.LegalGuardian) &&
            db.Families.Any(f => f.Id == p.FamilyId && f.Status == FamilyStatus.Active) &&
            db.FamilyMembers.Any(m => m.Id == p.ParentFamilyMemberId && m.FamilyId == p.FamilyId && m.IsActive && m.PatientId == adultPatientId && m.Role == p.ParentRole) &&
            db.FamilyMembers.Any(m => m.Id == p.ChildFamilyMemberId && m.FamilyId == p.FamilyId && m.IsActive && m.PatientId == p.ChildPatientId && m.Role == FamilyMemberRole.Child) &&
            db.Patients.Any(c => c.Id == p.ChildPatientId && c.DateOfBirth > cutoff && c.DateOfBirth <= today) &&
            db.Patients.Any(a => a.Id == adultPatientId && a.DateOfBirth <= cutoff) &&
            db.FamilyRelationshipRequests.Any(r => r.Id == p.ApprovedRelationshipRequestId &&
                (r.RequestType == FamilyRelationshipRequestType.CreateFamily || r.RequestType == FamilyRelationshipRequestType.AddFamilyMember ||
                 r.RequestType == FamilyRelationshipRequestType.VerifyParentChild || r.RequestType == FamilyRelationshipRequestType.VerifyGuardianChild ||
                 r.RequestType == FamilyRelationshipRequestType.VerifyOperationalParentChild || r.RequestType == FamilyRelationshipRequestType.JoinFamilyAsParent) &&
                r.Status == FamilyRelationshipRequestStatus.Approved && r.CurrentRevisionNumber == p.ApprovedRevisionNumber && r.ReviewedOnUtc == p.ApprovedAtUtc &&
                r.FamilyId == p.FamilyId && r.RequesterPatientId == adultPatientId && r.TargetPatientId == p.ChildPatientId &&
                r.RequesterClaimedRole == p.ParentRole && r.TargetClaimedRole == FamilyMemberRole.Child && r.ReviewedBySuperAdminApplicationUserId == p.ReviewedByApplicationUserId) &&
            db.SuperAdmins.Any(s => s.ApplicationUserId == p.ReviewedByApplicationUserId &&
                db.ApplicationUsers.Any(u => u.Id == s.ApplicationUserId && u.UserType == UserType.SuperAdmin)) &&
            db.FamilyRelationshipDocuments.Any(d => d.FamilyRelationshipRequestId == p.ApprovedRelationshipRequestId && d.RevisionNumber == p.ApprovedRevisionNumber && d.FileSize > 0))
            .ToDictionaryAsync(p => p.ChildPatientId, ct);
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
    public async Task<DependentAccessSnapshot> SnapshotAsync(IReadOnlyCollection<Guid> children, CancellationToken ct)
    {
        DependentAccessSnapshot empty = new(new Dictionary<Guid, DependentOperationalScope>(), new HashSet<string>());
        if (!current.IsAuthenticated || current.UserId is not { } actor) return empty;
        var account = await store.GetAccessSnapshotAsync(actor, ct);
        if (account is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient, PatientId: not null }) return empty;
        var link = await store.FindPatientAccountLinkAsync(actor, ct);
        if (link?.PatientId != account.PatientId) return empty;
        var pairs = await proofs.FindManyAsync(link!.PatientId, children, ct);
        return new(pairs.ToDictionary(p => p.Key, p => new DependentOperationalScope(actor, link.PatientId, p.Key, p.Value.FamilyId,
            p.Value.Id, p.Value.ParentRole, $"{p.Value.Id:N}:{Convert.ToBase64String(p.Value.RowVersion)}:{p.Value.ApprovedRevisionNumber}")),
            account.Permissions.Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase));
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
