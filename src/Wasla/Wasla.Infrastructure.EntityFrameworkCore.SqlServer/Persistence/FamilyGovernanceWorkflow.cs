using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyGovernanceWorkflow(WaslaDbContext db, IWaslaDataStore store, ICurrentUser current,
    IDependentAccessPolicy access, IOperationalAuthorityReader proofs, IPatientIdentityStore identities, IMediaService media,
    IPrivateMediaReader reader, IDateTimeProvider clock, IOptions<DependentManagementOptions> options,
    IOptions<FamilyMedicalPolicyOptions> medicalPolicy) : IFamilyGovernanceWorkflow
{
    private static Error Missing => DependentAccessErrors.Unavailable;
    private static Error Invalid => Error.Validation("FamilyGovernance.Invalid", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyGovernance_Invalid"));
    private static Error Conflict => Error.Conflict("FamilyGovernance.Conflict", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyGovernance_Conflict"));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Key(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100;
    private static bool Version(FamilyGovernanceCase row, string? value)
    { try { return value is { Length: > 0 and <= 40 } && row.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value)); } catch (FormatException) { return false; } }
    private async Task<UserAccessSnapshot?> ActorAsync(string permission, bool admin, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } user || !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return null;
        var a = await store.GetAccessSnapshotAsync(user, ct);
        if (a is not { User.IsActive: true, User.IsFirstLogin: false } || !a.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
            a.User.UserType != (admin ? UserType.SuperAdmin : UserType.Patient) || admin && a.SuperAdminId is null) return null;
        if (!admin)
        {
            var link = await store.FindPatientAccountLinkAsync(user, ct); var p = link is null ? null : await store.FindPatientByIdAsync(link.PatientId, ct);
            if (p is null || a.PatientId != p.Id || p.GetAge(options.Value.Today(clock.UtcNow)) < 18) return null;
        }
        return a;
    }
    private static FamilyGovernanceReceipt Map(FamilyGovernanceCase c) => new(c.Id, c.Kind, c.Status, c.Revision, Convert.ToBase64String(c.RowVersion), c.Status switch {
        FamilyGovernanceCaseStatus.PendingReview => "WaitForReview", FamilyGovernanceCaseStatus.ModificationRequested => "ReviseSubmittedEvidence",
        FamilyGovernanceCaseStatus.Approved => "RefreshDependentProfile", FamilyGovernanceCaseStatus.PendingGovernanceDecision => "PendingGovernanceDecision", _ => "ContactAuthorizedReviewTeam" });
    private async Task<Result<FamilyGovernanceReceipt>?> ReplayAsync(Guid actor, string op, string key, string hash, CancellationToken ct)
    {
        var prior = await db.Set<FamilyGovernanceOperation>().AsNoTracking().SingleOrDefaultAsync(o => o.ActorUserId == actor && o.Operation == op && o.IdempotencyKey == key, ct);
        return prior is null ? null : prior.Fingerprint != hash ? Result<FamilyGovernanceReceipt>.Fail(Conflict) :
            Result<FamilyGovernanceReceipt>.Ok(JsonSerializer.Deserialize<FamilyGovernanceReceipt>(prior.ResponseJson)!);
    }
    private async Task<Result<FamilyGovernanceReceipt>> SaveAsync(FamilyGovernanceCase row, Guid actor, string op, string key, string hash, string? reason, CancellationToken ct)
    {
        db.Add(new FamilyGovernanceHistory(row.Id, row.Revision, actor, op, reason, clock.UtcNow)); await db.SaveChangesAsync(ct);
        var receipt = Map(row); db.Add(new FamilyGovernanceOperation(actor, row.Id, op, key, hash, JsonSerializer.Serialize(receipt), clock.UtcNow));
        await db.SaveChangesAsync(ct); return Result<FamilyGovernanceReceipt>.Ok(receipt);
    }
    private async Task<string?> FingerprintAsync(object payload, IReadOnlyList<FamilyEvidenceUpload> files, CancellationToken ct)
    {
        if (files.Count < 1 || files.Count > options.Value.MaxEvidenceFiles) return null;
        var pieces = new List<string> { JsonSerializer.Serialize(payload) };
        foreach (var f in files)
        {
            if (!Enum.IsDefined(f.DocumentType) || !f.File.Content.CanSeek || f.File.Length <= 0 || f.File.Length > options.Value.MaxEvidenceFileBytes || f.File.Content.Length != f.File.Length ||
                Path.GetExtension(f.File.FileName).ToLowerInvariant() is not (".pdf" or ".jpg" or ".jpeg" or ".png")) return null;
            f.File.Content.Position = 0; pieces.Add($"{f.DocumentType}:{Path.GetFileName(f.File.FileName)}:{Convert.ToHexString(await SHA256.HashDataAsync(f.File.Content, ct))}"); f.File.Content.Position = 0;
        }
        return Hash(string.Join('|', pieces));
    }
    private async Task EvidenceAsync(FamilyGovernanceCase row, IReadOnlyList<FamilyEvidenceUpload> files, CancellationToken ct)
    {
        foreach (var f in files)
        {
            var saved = await media.SaveAsync(f.File, new($"FamilyGovernance/{row.Id:N}/{row.Revision}"), ct);
            db.Add(new FamilyGovernanceEvidence(row.Id, row.Revision, f.DocumentType, saved.Key, Path.GetFileName(f.File.FileName), saved.ContentType, saved.Length, clock.UtcNow));
        }
        // Preserve staged private bytes on uncertain commit. Referenced evidence is immutable.
    }
    private async Task<bool> EvidenceCurrentAsync(FamilyGovernanceCase row, CancellationToken ct)
    {
        var docs = await db.Set<FamilyGovernanceEvidence>().AsNoTracking().Where(e => e.CaseId == row.Id && e.Revision == row.Revision).ToArrayAsync(ct);
        if (docs.Length < 1 || docs.Length > options.Value.MaxEvidenceFiles) return false;
        foreach (var doc in docs)
        {
            var opened = await reader.OpenAsync(doc.MediaKey, ct); if (opened is null) return false;
            await using var bytes = opened.Content; if (!bytes.CanSeek || bytes.Length != doc.FileSize) return false;
        }
        return true;
    }
    private static DependentIdentityCorrectionInput Snapshot(Patient p) => new(p.NameAr, p.NameEn, p.DateOfBirth, p.Gender);
    public async Task<Result<FamilyGovernanceReceipt>> SubmitIdentityAsync(SubmitDependentIdentityCorrectionCommand c, CancellationToken ct)
    {
        var scope = await access.AuthorizeAsync(c.ChildPatientId, FamilyGovernancePermissionNames.IdentityCorrectionRequest, ct);
        if (scope.IsFailure || scope.Value.ParentRole is not (FamilyMemberRole.Father or FamilyMemberRole.Mother)) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        var p = c.ProposedIdentity; var today = options.Value.Today(clock.UtcNow);
        if (!Key(c.IdempotencyKey) || p is null || string.IsNullOrWhiteSpace(p.NameAr) || p.NameAr.Length > 200 || p.NameEn?.Length > 200 ||
            p.DateOfBirth == default || p.DateOfBirth > today || p.Gender is not (Gender.Male or Gender.Female) || string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Length > 2000)
            return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        var op = c.CaseId.HasValue ? "IdentityResubmit" : "IdentitySubmit";
        var hash = await FingerprintAsync(new { c.ChildPatientId, c.CaseId, c.RowVersion, c.ProposedIdentity, c.Reason }, c.Evidence, ct);
        if (hash is null) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        await identities.LockAsync(ct);
        if (await ReplayAsync(scope.Value.ActorUserId, op, c.IdempotencyKey, hash, ct) is { } replay) return replay;
        var patient = await db.Patients.AsNoTracking().SingleAsync(p => p.Id == c.ChildPatientId, ct);
        FamilyGovernanceCase row;
        if (c.CaseId is { } id)
        {
            row = (await db.Set<FamilyGovernanceCase>().SingleOrDefaultAsync(r => r.Id == id && r.ActorUserId == scope.Value.ActorUserId && r.ChildPatientId == c.ChildPatientId && r.Kind == FamilyGovernanceCaseKind.IdentityCorrection, ct))!;
            if (row is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
            if (row.Status != FamilyGovernanceCaseStatus.ModificationRequested || !Version(row, c.RowVersion)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
            row.Resubmit(scope.Value.AuthorityId, clock.UtcNow);
        }
        else
        {
            if (await db.Set<FamilyGovernanceCase>().AnyAsync(r => r.ParentPatientId == scope.Value.AdultPatientId && r.ChildPatientId == c.ChildPatientId && r.Kind == FamilyGovernanceCaseKind.IdentityCorrection &&
                (r.Status == FamilyGovernanceCaseStatus.PendingReview || r.Status == FamilyGovernanceCaseStatus.ModificationRequested || r.Status == FamilyGovernanceCaseStatus.PendingGovernanceDecision), ct)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
            if (await db.Set<FamilyGovernanceCase>().CountAsync(r => r.ActorUserId == scope.Value.ActorUserId && r.CreatedAtUtc >= clock.UtcNow.Date, ct) >= 20) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
            row = new(FamilyGovernanceCaseKind.IdentityCorrection, scope.Value.ActorUserId, scope.Value.AdultPatientId, c.ChildPatientId, scope.Value.AuthorityId, clock.UtcNow); db.Add(row);
        }
        db.Add(new FamilyGovernanceRevision(row.Id, row.Revision, JsonSerializer.Serialize(Snapshot(patient)), JsonSerializer.Serialize(p), c.Reason.Trim(), scope.Value.Version, clock.UtcNow));
        await EvidenceAsync(row, c.Evidence, ct);
        var fresh = await access.AuthorizeAsync(c.ChildPatientId, FamilyGovernancePermissionNames.IdentityCorrectionRequest, ct);
        if (fresh.IsFailure || fresh.Value != scope.Value) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        return await SaveAsync(row, scope.Value.ActorUserId, op, c.IdempotencyKey, hash, null, ct);
    }
    public async Task<Result<FamilyGovernanceReceipt>> OpenConflictAsync(Guid actorUserId, Guid parentPatientId, Guid childPatientId,
        FamilyMemberRole intendedRole, IReadOnlyList<FamilyEvidenceUpload> evidence, string key, string fingerprint, CancellationToken ct,
        Guid? onboardingRequestId = null)
    {
        var actor = onboardingRequestId.HasValue ? await store.GetAccessSnapshotAsync(actorUserId, ct) : await ActorAsync(FamilySelfServicePermissionNames.Submit, false, ct);
        if (onboardingRequestId is { } source)
        {
            // The review operates in the administrator's context, on behalf of the
            // original applicant. Validate both actors and the preserved intake.
            var admin = await ActorAsync(FamilySelfServicePermissionNames.OnboardingReview, true, ct);
            var intake = await db.Set<ChildOnboardingRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == source &&
                r.ActorApplicationUserId == actorUserId && r.ParentPatientId == parentPatientId && r.Status == ChildOnboardingStatus.PendingReview, ct);
            var link = await store.FindPatientAccountLinkAsync(actorUserId, ct);
            var parentPatient = await store.FindPatientByIdAsync(parentPatientId, ct);
            if (admin is null || intake is null || actor is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient } ||
                !actor.Permissions.Contains(FamilySelfServicePermissionNames.Onboard) || link?.PatientId != parentPatientId ||
                parentPatient is null || parentPatient.GetAge(options.Value.Today(clock.UtcNow)) < 18 || intake.ParentRole != intendedRole ||
                !await FamilyIdentitySearch.Exact(db.Patients.Where(p => p.Id == childPatientId && p.DateOfBirth == intake.DateOfBirth && p.Gender == intake.Gender), intake.NameAr).AnyAsync(ct))
                return Result<FamilyGovernanceReceipt>.Fail(Missing);
        }
        if (!options.Value.Enabled || actor is null || actor.PatientId != parentPatientId || actor.User.Id != actorUserId) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        if (!Key(key) || intendedRole is not (FamilyMemberRole.Father or FamilyMemberRole.Mother)) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        var hash = await FingerprintAsync(new { parentPatientId, childPatientId, intendedRole, fingerprint }, evidence, ct); if (hash is null) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        await identities.LockAsync(ct);
        if (await ReplayAsync(actorUserId, "MembershipConflict", key, hash, ct) is { } replay) return replay;
        if (await db.Set<FamilyGovernanceCase>().CountAsync(r => r.ActorUserId == actorUserId && r.CreatedAtUtc >= clock.UtcNow.Date, ct) >= 20) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        var parent = await store.FindActiveFamilyMemberByPatientIdAsync(parentPatientId, ct); var child = await store.FindActiveFamilyMemberByPatientIdAsync(childPatientId, ct);
        if (parent is null || child is null || parent.FamilyId == child.FamilyId || child.Role != FamilyMemberRole.Child) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        var today = options.Value.Today(clock.UtcNow);
        if (!await db.Patients.AnyAsync(p => p.Id == childPatientId && p.DateOfBirth > today.AddYears(-18) && p.DateOfBirth <= today, ct)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        if (await db.Set<FamilyGovernanceCase>().AnyAsync(r => r.ParentPatientId == parentPatientId && r.ChildPatientId == childPatientId && r.Kind == FamilyGovernanceCaseKind.MembershipConflict &&
            (r.Status == FamilyGovernanceCaseStatus.PendingReview || r.Status == FamilyGovernanceCaseStatus.ModificationRequested || r.Status == FamilyGovernanceCaseStatus.PendingGovernanceDecision), ct)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        var row = new FamilyGovernanceCase(FamilyGovernanceCaseKind.MembershipConflict, actorUserId, parentPatientId, childPatientId, null, clock.UtcNow); db.Add(row);
        db.Add(new FamilyGovernanceRevision(row.Id, 1, JsonSerializer.Serialize(new { ParentFamilyId = parent.FamilyId, ChildFamilyId = child.FamilyId }), JsonSerializer.Serialize(new { intendedRole }), "Confidential membership review", null, clock.UtcNow));
        await EvidenceAsync(row, evidence, ct); return await SaveAsync(row, actorUserId, "MembershipConflict", key, hash, null, ct);
    }
    public async Task<Result<FamilyGovernanceReceipt>> ResubmitConflictAsync(ResubmitFamilyConflictCommand c, CancellationToken ct)
    {
        var actor = await ActorAsync(FamilySelfServicePermissionNames.Submit, false, ct);
        if (actor is null || !options.Value.Enabled) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        if (!Key(c.IdempotencyKey)) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        var hash = await FingerprintAsync(new { c.CaseId, c.RowVersion }, c.Evidence, ct); if (hash is null) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        await identities.LockAsync(ct);
        if (await ReplayAsync(actor.User.Id, "ConflictResubmit", c.IdempotencyKey, hash, ct) is { } replay) return replay;
        var row = await db.Set<FamilyGovernanceCase>().SingleOrDefaultAsync(r => r.Id == c.CaseId && r.ActorUserId == actor.User.Id && r.Kind == FamilyGovernanceCaseKind.MembershipConflict, ct);
        if (row is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        if (row.Status != FamilyGovernanceCaseStatus.ModificationRequested || !Version(row, c.RowVersion)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        var original = await db.Set<FamilyGovernanceRevision>().AsNoTracking().SingleAsync(v => v.CaseId == row.Id && v.Revision == row.Revision, ct);
        row.Resubmit(null, clock.UtcNow);
        db.Add(new FamilyGovernanceRevision(row.Id, row.Revision, original.BeforeSnapshot, original.AfterSnapshot, "Revised membership evidence", null, clock.UtcNow));
        await EvidenceAsync(row, c.Evidence, ct);
        if (await ActorAsync(FamilySelfServicePermissionNames.Submit, false, ct) is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        return await SaveAsync(row, actor.User.Id, "ConflictResubmit", c.IdempotencyKey, hash, null, ct);
    }
    public async Task<Result<FamilyGovernanceReceipt>> DecideAsync(DecideFamilyGovernanceCaseCommand c, CancellationToken ct)
    {
        var permission = c.Decision switch { FamilyGovernanceCaseStatus.Rejected => PermissionNames.FamilyRelationshipRequestsReject,
            FamilyGovernanceCaseStatus.ModificationRequested => PermissionNames.FamilyRelationshipRequestsRequestModification, _ => PermissionNames.FamilyRelationshipRequestsApprove };
        var actor = await ActorAsync(permission, true, ct); if (actor is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        if (!Key(c.IdempotencyKey) || c.Decision is not (FamilyGovernanceCaseStatus.Approved or FamilyGovernanceCaseStatus.Rejected or FamilyGovernanceCaseStatus.ModificationRequested or FamilyGovernanceCaseStatus.PendingGovernanceDecision) ||
            string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Length > 2000) return Result<FamilyGovernanceReceipt>.Fail(Invalid);
        var op = "Review:" + c.Decision; var hash = Hash(JsonSerializer.Serialize(c)); await identities.LockAsync(ct);
        if (await ReplayAsync(actor.User.Id, op, c.IdempotencyKey, hash, ct) is { } replay) return replay;
        var row = await db.Set<FamilyGovernanceCase>().SingleOrDefaultAsync(r => r.Id == c.CaseId, ct); if (row is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        if (!(row.Status == FamilyGovernanceCaseStatus.PendingReview || row.Status == FamilyGovernanceCaseStatus.PendingGovernanceDecision && c.Decision == FamilyGovernanceCaseStatus.Rejected) || !Version(row, c.RowVersion)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
        var decision = c.Decision;
        if (decision == FamilyGovernanceCaseStatus.Approved)
        {
            if (!options.Value.Enabled || !await EvidenceCurrentAsync(row, ct)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
            if (row.Kind == FamilyGovernanceCaseKind.MembershipConflict) decision = FamilyGovernanceCaseStatus.PendingGovernanceDecision;
            else
            {
                var version = await db.Set<FamilyGovernanceRevision>().SingleAsync(v => v.CaseId == row.Id && v.Revision == row.Revision, ct);
                var proof = await proofs.FindAsync(row.ParentPatientId, row.ChildPatientId, ct);
                var requester = await store.GetAccessSnapshotAsync(row.ActorUserId, ct); var link = await store.FindPatientAccountLinkAsync(row.ActorUserId, ct);
                if (proof is null || proof.Id != row.OperationalAuthorityId || proof.ParentRole is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) ||
                    $"{proof.Id:N}:{Convert.ToBase64String(proof.RowVersion)}:{proof.ApprovedRevisionNumber}" != version.AuthorityVersion ||
                    requester is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient } || requester.PatientId != row.ParentPatientId || link?.PatientId != row.ParentPatientId ||
                    !requester.Permissions.Contains(FamilyGovernancePermissionNames.IdentityCorrectionRequest)) return Result<FamilyGovernanceReceipt>.Fail(Conflict);
                var patient = await db.Patients.SingleAsync(p => p.Id == row.ChildPatientId, ct); var proposed = JsonSerializer.Deserialize<DependentIdentityCorrectionInput>(version.AfterSnapshot)!;
                var today = options.Value.Today(clock.UtcNow); var medicalToday = medicalPolicy.Value.IsConfigured ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(medicalPolicy.Value.TimeZoneId!))) : today;
                var crossesMedicalAge = (patient.DateOfBirth.AddYears(16) > medicalToday) != (proposed.DateOfBirth.AddYears(16) > medicalToday);
                var conflict = JsonSerializer.Serialize(Snapshot(patient)) != version.BeforeSnapshot || proposed.DateOfBirth <= today.AddYears(-18) || crossesMedicalAge || patient.Email is not null ||
                    await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patient.Id, ct) || await db.PatientClaimRequests.AnyAsync(r =>
                        (r.TargetPatientId == patient.Id || r.ApprovedTargetPatientId == patient.Id || r.ApplicantDateOfBirth == patient.DateOfBirth && r.ApplicantGender == patient.Gender) &&
                        (r.IsUnderLegalHold || r.Status == PatientClaimStatus.PendingReview || r.Status == PatientClaimStatus.ModificationRequested || r.Status == PatientClaimStatus.ApprovedPendingActivation), ct) ||
                    await FamilyIdentitySearch.Exact(db.Patients.Where(p => p.Id != patient.Id && p.DateOfBirth == proposed.DateOfBirth && p.Gender == proposed.Gender), proposed.NameAr).AnyAsync(ct);
                if (conflict) decision = FamilyGovernanceCaseStatus.PendingGovernanceDecision;
                else
                {
                    var corrected = patient.CorrectIdentity(proposed.NameAr, proposed.NameEn, proposed.DateOfBirth, proposed.Gender, today);
                    if (corrected.IsFailure) return Result<FamilyGovernanceReceipt>.Fail(corrected.Errors);
                }
            }
        }
        if (await ActorAsync(permission, true, ct) is null) return Result<FamilyGovernanceReceipt>.Fail(Missing);
        row.Decide(decision, actor.User.Id, clock.UtcNow); return await SaveAsync(row, actor.User.Id, op, c.IdempotencyKey, hash, c.Reason.Trim(), ct);
    }
    public async Task<Result<FamilyGovernancePage>> ReadAsync(GetFamilyGovernanceCasesQuery q, CancellationToken ct)
    {
        var permission = q.Admin ? q.CaseId.HasValue ? PermissionNames.FamilyRelationshipRequestsViewDetails : PermissionNames.FamilyRelationshipRequestsViewAll : FamilySelfServicePermissionNames.Workspace;
        var actor = await ActorAsync(permission, q.Admin, ct); if (actor is null) return Result<FamilyGovernancePage>.Fail(Missing);
        if (q.PageNumber is < 1 or > 10000 || q.PageSize is < 1 or > 100 || q.Kind.HasValue && !Enum.IsDefined(q.Kind.Value)) return Result<FamilyGovernancePage>.Fail(Invalid);
        var query = db.Set<FamilyGovernanceCase>().AsNoTracking().Where(r => (q.Admin || r.ActorUserId == actor.User.Id) && (!q.CaseId.HasValue || r.Id == q.CaseId) && (!q.Kind.HasValue || r.Kind == q.Kind));
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        if (q.CaseId.HasValue && rows.Length == 0) return Result<FamilyGovernancePage>.Fail(Missing);
        var output = new List<FamilyGovernanceDetails>();
        foreach (var row in rows)
        {
            var revisions = q.Admin && q.CaseId.HasValue ? await db.Set<FamilyGovernanceRevision>().AsNoTracking().Where(r => r.CaseId == row.Id).OrderBy(r => r.Revision)
                .Select(r => new FamilyGovernanceRevisionResponse(r.Revision, r.BeforeSnapshot, r.AfterSnapshot, r.Reason, r.CreatedAtUtc)).Take(100).ToArrayAsync(ct) : [];
            var docs = q.Admin && q.CaseId.HasValue ? await db.Set<FamilyGovernanceEvidence>().AsNoTracking().Where(e => e.CaseId == row.Id).OrderBy(e => e.Revision).ThenBy(e => e.Id)
                .Select(e => new FamilyRelationshipDocumentResponse(e.Id, e.Revision, e.DocumentType, e.FileName, e.ContentType, e.FileSize, e.CreatedAtUtc)).Take(500).ToArrayAsync(ct) : [];
            var history = q.Admin && q.CaseId.HasValue ? await db.Set<FamilyGovernanceHistory>().AsNoTracking().Where(h => h.CaseId == row.Id).OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id)
                .Select(h => new FamilyGovernanceHistoryResponse(h.Revision, h.ActorUserId, h.Action, h.Reason, h.OccurredAtUtc)).Take(500).ToArrayAsync(ct) : [];
            output.Add(new(Map(row), row.CreatedAtUtc, row.Status == FamilyGovernanceCaseStatus.ModificationRequested ? Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyRelationship_ReviewSubmittedEvidence") : null, revisions, docs, history));
        }
        if (await ActorAsync(permission, q.Admin, ct) is null) return Result<FamilyGovernancePage>.Fail(Missing);
        return Result<FamilyGovernancePage>.Ok(new(output, q.PageNumber, q.PageSize, await query.CountAsync(ct)));
    }
    public async Task<Result<PrivateMedia>> EvidenceAsync(FamilyGovernanceEvidenceQuery q, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.FamilyRelationshipRequestsViewDetails, true, ct) is null) return Result<PrivateMedia>.Fail(Missing);
        var doc = await db.Set<FamilyGovernanceEvidence>().AsNoTracking().SingleOrDefaultAsync(e => e.CaseId == q.CaseId && e.Id == q.EvidenceId, ct);
        if (doc is null) return Result<PrivateMedia>.Fail(Missing);
        var opened = await reader.OpenAsync(doc.MediaKey, ct); if (opened is null) return Result<PrivateMedia>.Fail(Missing);
        try
        {
            var actor = await ActorAsync(PermissionNames.FamilyRelationshipRequestsViewDetails, true, ct);
            if (actor is null) { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(Missing); }
            db.Add(new FamilyGovernanceHistory(doc.CaseId, doc.Revision, actor.User.Id, "EvidenceViewed", null, clock.UtcNow)); await db.SaveChangesAsync(ct);
            if (await ActorAsync(PermissionNames.FamilyRelationshipRequestsViewDetails, true, ct) is null) { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(Missing); }
            return Result<PrivateMedia>.Ok(opened with { FileName = doc.FileName, ContentType = doc.ContentType });
        }
        catch { await opened.Content.DisposeAsync(); throw; }
    }
}
