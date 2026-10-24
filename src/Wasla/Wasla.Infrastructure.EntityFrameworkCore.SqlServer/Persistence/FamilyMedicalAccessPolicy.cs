using System.Globalization;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyMedicalAccessPolicy(WaslaDbContext db, IWaslaDataStore store, ICurrentUser current,
    IDateTimeProvider clock, IOptions<MedicalRecordOptions> sharing, IOptions<FamilyMedicalPolicyOptions> policy) : IFamilyMedicalAccessPolicy
{
    internal FamilyMedicalPolicyOptions Policy => policy.Value;
    internal bool Configured => Policy.IsConfigured && EffectiveDate() is not null;
    internal bool SharingEnabled => sharing.Value.GuardianSharingEnabled && Configured;
    internal DateOnly? EffectiveDate()
    {
        if (string.IsNullOrWhiteSpace(Policy.TimeZoneId)) return null;
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(Policy.TimeZoneId))); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }
    internal async Task<UserAccessSnapshot?> ActorAsync(string permission, bool admin, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } actor || !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return null;
        var snapshot = await store.GetAccessSnapshotAsync(actor, ct);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false } && snapshot.User.UserType == (admin ? UserType.SuperAdmin : UserType.Patient) && (!admin || snapshot.SuperAdminId is not null) &&
            snapshot.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ? snapshot : null;
    }
    internal async Task<IReadOnlyList<string>> PermissionsAsync(CancellationToken ct)
    {
        if (current.UserId is not { } user) return [];
        var snapshot = await store.GetAccessSnapshotAsync(user, ct);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false } ? snapshot.Permissions.Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray() : [];
    }
    internal async Task<Guid?> PatientAsync(string permission, CancellationToken ct)
    {
        var actor = await ActorAsync(permission, false, ct);
        return actor is null ? null : (await store.FindPatientAccountLinkAsync(actor.User.Id, ct))?.PatientId;
    }
    internal async Task<FamilyMember?> MemberAsync(Guid patient, CancellationToken ct)
        => await db.FamilyMembers.AsNoTracking().Where(m => m.PatientId == patient && m.IsActive &&
            db.Families.Any(f => f.Id == m.FamilyId && f.Status == FamilyStatus.Active) &&
            db.FamilyRelationshipRequests.Any(r => r.Id == m.AddedThroughRequestId && r.Status == FamilyRelationshipRequestStatus.Approved && r.FamilyId == m.FamilyId &&
                (r.RequesterPatientId == m.PatientId && r.RequesterClaimedRole == m.Role || r.TargetPatientId == m.PatientId && r.TargetClaimedRole == m.Role)))
            .SingleOrDefaultAsync(ct);
    internal async Task<bool> PairAsync(Guid family, Guid adult, Guid child, CancellationToken ct)
    {
        var a = await MemberAsync(adult, ct); var c = await MemberAsync(child, ct);
        if (a is null || c is null || !a.CanManageFamily || c.Role != FamilyMemberRole.Child || a.FamilyId != family || c.FamilyId != family || EffectiveDate() is not { } today) return false;
        var birthday = await db.Patients.Where(p => p.Id == child).Select(p => (DateOnly?)p.DateOfBirth).SingleOrDefaultAsync(ct);
        return birthday is { } dob && dob <= today && dob.AddYears(16) > today;
    }
    internal async Task<string?> AuthorityVersionAsync(Guid family, FamilyMember adult, FamilyMember child, Guid? proofId, CancellationToken ct)
    {
        if (!Configured || proofId is not { } id) return null;
        var proof = await db.FamilyRelationshipRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        var docs = await db.FamilyRelationshipDocuments.AsNoTracking().Where(d => d.FamilyRelationshipRequestId == id &&
            db.FamilyRelationshipRequests.Any(r => r.Id == id && r.CurrentRevisionNumber == d.RevisionNumber)).OrderBy(d => d.Id).ToArrayAsync(ct);
        var reviewerVerified = proof?.ReviewedBySuperAdminApplicationUserId is { } reviewer && await db.SuperAdmins.AsNoTracking().AnyAsync(s =>
            s.ApplicationUserId == reviewer && db.ApplicationUsers.Any(u => u.Id == s.ApplicationUserId && u.UserType == UserType.SuperAdmin), ct);
        return AuthorityVersion(family, adult, child, proof, docs, reviewerVerified);
    }
    private string? AuthorityVersion(Guid family, FamilyMember adult, FamilyMember child, FamilyRelationshipRequest? proof, FamilyRelationshipDocument[] docs, bool reviewerVerified)
    {
        if (!Configured || !reviewerVerified || !adult.CanManageFamily || !child.IsActive || adult.FamilyId != family || child.FamilyId != family ||
            child.Role != FamilyMemberRole.Child || adult.PatientId == child.PatientId || proof is null || proof.FamilyId != family || proof.Status != FamilyRelationshipRequestStatus.Approved ||
            proof.RequesterPatientId != adult.PatientId || proof.TargetPatientId != child.PatientId || proof.RequesterClaimedRole != adult.Role ||
            proof.TargetClaimedRole != FamilyMemberRole.Child || proof.ReviewedBySuperAdminApplicationUserId is null || proof.ReviewedOnUtc is null ||
            proof.RequestType is not (FamilyRelationshipRequestType.CreateFamily or FamilyRelationshipRequestType.AddFamilyMember or
                FamilyRelationshipRequestType.VerifyParentChild or FamilyRelationshipRequestType.VerifyGuardianChild)) return null;
        if (proof.RequestType == FamilyRelationshipRequestType.VerifyParentChild && adult.Role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) ||
            proof.RequestType == FamilyRelationshipRequestType.VerifyGuardianChild && adult.Role is not (FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian)) return null;
        if (docs.Length == 0 || docs.Length > Policy.MaximumFiles || docs.Any(d => !Policy.AllowedDocumentTypes.Contains(d.DocumentType) ||
            d.FileSize <= 0 || d.FileSize > Policy.MaximumFileBytes || string.IsNullOrWhiteSpace(d.MediaKey))) return null;
        return string.Join(':', proof.Id, proof.CurrentRevisionNumber, Convert.ToBase64String(proof.RowVersion),
            string.Join(',', docs.Select(d => d.Id)));
    }
    public async Task<Result<MedicalRecordScope>> ScopeAsync(Guid? child, CancellationToken ct)
    {
        if (!SharingEnabled || child is not { } id || current.UserId is not { } user) return Result<MedicalRecordScope>.Fail(Wasla.Domain.Clinical.MedicalRecordErrors.Denied);
        var statuses = await LoadAsync(PermissionNames.MedicalRecordsViewDependent, ct);
        if (statuses is null || !statuses.Value.Statuses.TryGetValue(id, out var status) || !status.CanView)
            return Result<MedicalRecordScope>.Fail(Wasla.Domain.Clinical.MedicalRecordErrors.Denied);
        return Result<MedicalRecordScope>.Ok(new(user, id, MedicalRecordMode.Dependent, AccessVersion: statuses.Value.Versions[id]));
    }
    public async Task<Result<FamilyMedicalAccessStatusResponse>> StatusAsync(Guid child, CancellationToken ct)
    {
        var data = await LoadAsync(PermissionNames.FamiliesViewOwn, ct);
        return data is not null && data.Value.Statuses.TryGetValue(child, out var status)
            ? Result<FamilyMedicalAccessStatusResponse>.Ok(status) : Result<FamilyMedicalAccessStatusResponse>.Fail(FamilyMedicalErrors.Unavailable);
    }
    public async Task<IReadOnlyDictionary<Guid, FamilyMedicalAccessStatusResponse>> StatusesAsync(Guid family, CancellationToken ct)
    {
        var data = await LoadAsync(PermissionNames.FamiliesViewOwn, ct);
        return data is not null && data.Value.Family == family ? data.Value.Statuses : new Dictionary<Guid, FamilyMedicalAccessStatusResponse>();
    }
    public async Task<Result<RecordPage<FamilyMedicalDependent>>> DependentsAsync(CancellationToken ct)
    {
        if (!SharingEnabled) return Result<RecordPage<FamilyMedicalDependent>>.Fail(Wasla.Domain.Clinical.MedicalRecordErrors.Denied);
        var data = await LoadAsync(PermissionNames.MedicalRecordsViewDependent, ct);
        if (data is null) return Result<RecordPage<FamilyMedicalDependent>>.Fail(Wasla.Domain.Clinical.MedicalRecordErrors.Denied);
        var ids = data.Value.Statuses.Where(p => p.Value.CanView).Select(p => p.Key).ToArray();
        var patients = await db.Patients.AsNoTracking().Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.NameAr, p.NameEn }).Take(100).ToArrayAsync(ct);
        var confirmed = await LoadAsync(PermissionNames.MedicalRecordsViewDependent, ct);
        if (!SharingEnabled || confirmed is null) return Result<RecordPage<FamilyMedicalDependent>>.Fail(Wasla.Domain.Clinical.MedicalRecordErrors.Denied);
        return Result<RecordPage<FamilyMedicalDependent>>.Ok(new(patients.Where(p => confirmed.Value.Statuses.TryGetValue(p.Id, out var s) && s.CanView)
            .Select(p => new FamilyMedicalDependent(p.Id, p.NameAr, p.NameEn, confirmed.Value.Statuses[p.Id])).ToArray(), null, false));
    }
    private async Task<(Guid Family, Dictionary<Guid, FamilyMedicalAccessStatusResponse> Statuses, Dictionary<Guid, string> Versions)?> LoadAsync(string permission, CancellationToken ct)
    {
        var patient = await PatientAsync(permission, ct); if (patient is not { } actorPatient) return null;
        // Normal roster metadata remains visible when the family is inactive; clinical membership is checked separately.
        var actor = await db.FamilyMembers.AsNoTracking().SingleOrDefaultAsync(m => m.PatientId == actorPatient && m.IsActive, ct);
        if (actor is null) return (Guid.Empty, [], []);
        var family = await db.Families.AsNoTracking().SingleAsync(f => f.Id == actor.FamilyId, ct);
        var children = await (from m in db.FamilyMembers.AsNoTracking() join p in db.Patients on m.PatientId equals p.Id
            where m.FamilyId == family.Id && m.IsActive && m.Role == FamilyMemberRole.Child
            select new { Member = m, p.DateOfBirth }).ToArrayAsync(ct);
        var reviews = await db.Set<FamilyMedicalEligibilityReview>().AsNoTracking().Where(r => r.FamilyId == family.Id && r.AdultPatientId == actorPatient).ToArrayAsync(ct);
        var parentProofs = await db.Set<VerifiedParentChildRelationship>().AsNoTracking().Where(r => r.FamilyId == family.Id && r.ParentPatientId == actorPatient &&
            r.Status == ParentChildVerificationStatus.Active).ToArrayAsync(ct);
        var proofIds = parentProofs.Select(p => p.ApprovedRelationshipRequestId).Concat(reviews.Where(r => r.AuthorityRelationshipRequestId.HasValue)
            .Select(r => r.AuthorityRelationshipRequestId!.Value)).Distinct().ToArray();
        var proofs = await db.FamilyRelationshipRequests.AsNoTracking().Where(r => proofIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var reviewerIds = proofs.Values.Where(p => p.ReviewedBySuperAdminApplicationUserId.HasValue).Select(p => p.ReviewedBySuperAdminApplicationUserId!.Value).Distinct().ToArray();
        var verifiedReviewers = (await db.SuperAdmins.AsNoTracking().Where(s => reviewerIds.Contains(s.ApplicationUserId) &&
            db.ApplicationUsers.Any(u => u.Id == s.ApplicationUserId && u.UserType == UserType.SuperAdmin)).Select(s => s.ApplicationUserId).ToArrayAsync(ct)).ToHashSet();
        var proofDocuments = (await db.FamilyRelationshipDocuments.AsNoTracking().Where(d => proofIds.Contains(d.FamilyRelationshipRequestId) &&
            db.FamilyRelationshipRequests.Any(r => r.Id == d.FamilyRelationshipRequestId && r.CurrentRevisionNumber == d.RevisionNumber))
            .OrderBy(d => d.Id).ToArrayAsync(ct)).ToLookup(d => d.FamilyRelationshipRequestId);
        var restrictions = await db.Set<FamilyMedicalAccessRestriction>().AsNoTracking().Where(r => r.FamilyId == family.Id && r.AffectedAdultPatientId == actorPatient).ToArrayAsync(ct);
        var own = await db.Set<FamilyMedicalAccessRequest>().AsNoTracking().Where(r => r.FamilyId == family.Id && r.SubmittedByPatientId == actorPatient &&
            (r.Status == FamilyMedicalRequestStatus.Pending || r.Status == FamilyMedicalRequestStatus.ModificationRequested)).ToArrayAsync(ct);
        var verifiedIds = await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == family.Id && db.FamilyRelationshipRequests.Any(r => r.Id == m.AddedThroughRequestId && r.FamilyId == m.FamilyId && r.Status == FamilyRelationshipRequestStatus.Approved &&
            (r.RequesterPatientId == m.PatientId && r.RequesterClaimedRole == m.Role || r.TargetPatientId == m.PatientId && r.TargetClaimedRole == m.Role))).Select(m => m.Id).ToArrayAsync(ct);
        var verified = verifiedIds.ToHashSet();
        var snapshot = await store.GetAccessSnapshotAsync(current.UserId!.Value, ct);
        if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false }) return null;
        bool Has(string p) => current.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase) && snapshot!.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase);
        var statuses = new Dictionary<Guid, FamilyMedicalAccessStatusResponse>(); var versions = new Dictionary<Guid, string>();
        var today = EffectiveDate();
        foreach (var c in children)
        {
            var baseEligible = family.Status == FamilyStatus.Active && actor.CanManageFamily && verified.Contains(actor.Id) && verified.Contains(c.Member.Id);
            var ageEligible = today is { } date && c.DateOfBirth <= date && c.DateOfBirth.AddYears(16) > date;
            var review = reviews.SingleOrDefault(r => r.ChildPatientId == c.Member.PatientId && r.AdultFamilyMemberId == actor.Id && r.ChildFamilyMemberId == c.Member.Id);
            var restriction = restrictions.SingleOrDefault(r => r.ChildPatientId == c.Member.PatientId && r.Status == FamilyMedicalRestrictionStatus.Active);
            var parent = actor.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother;
            var parentProof = parentProofs.SingleOrDefault(r => r.ChildPatientId == c.Member.PatientId && r.ParentFamilyMemberId == actor.Id &&
                r.ChildFamilyMemberId == c.Member.Id && r.ParentRole == actor.Role);
            // The recorded policy version is provenance, not a separate per-parent medical approval veto.
            // Reevaluate current policy and allowed proof below; only Guardians need per-pair medical policy approval.
            var proofId = parent ? parentProof?.ApprovedRelationshipRequestId : review?.AuthorityRelationshipRequestId;
            var proof = proofId is { } source && proofs.TryGetValue(source, out var found) ? found : null;
            var proofVersion = baseEligible && proof is not null ? AuthorityVersion(family.Id, actor, c.Member, proof, proofDocuments[proof.Id].ToArray(),
                proof.ReviewedBySuperAdminApplicationUserId is { } reviewer && verifiedReviewers.Contains(reviewer)) : null;
            if (parentProof is not null && (proof is null || proof.CurrentRevisionNumber != parentProof.ApprovedRevisionNumber ||
                proof.ReviewedBySuperAdminApplicationUserId != parentProof.VerifiedBySuperAdminApplicationUserId || proof.ReviewedOnUtc != parentProof.VerifiedAtUtc)) proofVersion = null;
            var entitlement = proofVersion is not null && (parent || actor.Role is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian &&
                review is { Status: FamilyMedicalEligibilityStatus.Approved } && review.PolicyVersion == Policy.PolicyVersion &&
                review.BasisReference is not null && Policy.BasisReferences.Contains(review.BasisReference, StringComparer.Ordinal));
            var open = own.FirstOrDefault(r => r.ChildPatientId == c.Member.PatientId);
            var status = !SharingEnabled ? "Unavailable" : !baseEligible ? "RelationshipNotEligible" : !ageEligible ? "AgeNotEligible" :
                restriction is not null ? "Restricted" : !entitlement ? "RelationshipNotEligible" : !Has(PermissionNames.MedicalRecordsViewDependent) ? "Unavailable" : "Allowed";
            var reason = status switch { "Unavailable" => SharingEnabled ? "PermissionUnavailable" : "PolicyUnavailable", "Restricted" => "RestrictionActive",
                "AgeNotEligible" => "AgeNotEligible", "RelationshipNotEligible" => !baseEligible ? "RelationshipNotEligible" : parent ? "ParentChildVerificationRequired" : review is null or { Status: FamilyMedicalEligibilityStatus.PendingReview } ? "EligibilityReviewPending" : "EligibilityUnavailable", _ => null };
            statuses[c.Member.PatientId] = new(status, status == "Allowed", Configured && baseEligible && ageEligible && Has(PermissionNames.FamilyMedicalAccessRequestsCreateOwn),
                Configured && baseEligible && ageEligible && restriction is not null && Has(PermissionNames.FamilyMedicalAccessRequestsCreateOwn), open?.Id, open?.RequestType, reason);
            versions[c.Member.PatientId] = string.Join(':', family.Id, Convert.ToBase64String(family.RowVersion), actor.Id, actor.Role, actor.VerifiedOnUtc.Ticks,
                c.Member.Id, c.Member.VerifiedOnUtc.Ticks, parentProof?.Id, Convert.ToBase64String(parentProof?.RowVersion ?? []), proofVersion,
                parent ? null : review?.Id, parent ? null : Convert.ToBase64String(review?.RowVersion ?? []),
                restrictions.Where(r => r.ChildPatientId == c.Member.PatientId).OrderBy(r => r.Id).Aggregate("", (a, r) => a + Convert.ToBase64String(r.RowVersion)),
                Policy.PolicyVersion, Policy.ApprovalReference, Policy.TimeZoneId, string.Join(',', Policy.BasisReferences), string.Join(',', Policy.AllowedDocumentTypes),
                today?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        return (family.Id, statuses, versions);
    }
}
