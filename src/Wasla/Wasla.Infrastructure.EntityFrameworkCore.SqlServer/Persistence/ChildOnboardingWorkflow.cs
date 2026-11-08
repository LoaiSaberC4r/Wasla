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

internal sealed class ChildOnboardingWorkflow(WaslaDbContext db, IWaslaDataStore store, IPatientIdentityStore identity,
    ICurrentUser current, IDateTimeProvider clock, IOptions<DependentManagementOptions> options, IMediaService media,
    IPrivateMediaReader privateMedia, FamilyRelationshipWorkflowService familyWorkflow) : IChildOnboardingWorkflow
{
    private static Error Conflict => Error.Conflict("ChildOnboarding.StaleRevision", Wasla.Domain.Resources.FamilySelfServiceText.Get("ChildOnboarding_StaleRevision"));
    private static Error Invalid => Error.Validation("ChildOnboarding.Invalid", Wasla.Domain.Resources.FamilySelfServiceText.Get("ChildOnboarding_Invalid"));
    private async Task<UserAccessSnapshot?> ActorAsync(string permission, bool admin, CancellationToken ct)
    {
        if (!options.Value.Enabled || !current.IsAuthenticated || current.UserId is not { } actor || !current.Permissions.Contains(permission)) return null;
        var access = await store.GetAccessSnapshotAsync(actor, ct);
        if (access is not { User.IsActive: true, User.IsFirstLogin: false } || !access.Permissions.Contains(permission) ||
            access.User.UserType != (admin ? UserType.SuperAdmin : UserType.Patient) || (admin && access.SuperAdminId is null)) return null;
        if (!admin)
        {
            var link = await store.FindPatientAccountLinkAsync(actor, ct);
            var patient = await store.FindPatientByUserIdAsync(actor, ct);
            if (link is null || patient is null || access.PatientId != patient.Id || link.PatientId != patient.Id || patient.GetAge(options.Value.Today(clock.UtcNow)) < 18) return null;
        }
        return access;
    }
    private static ChildOnboardingResponse Map(ChildOnboardingRequest r, bool admin = false) => new(r.Id, r.Status, r.Revision, r.CreatedAtUtc,
        Convert.ToBase64String(r.RowVersion), admin ? r.ReviewMessage : r.Status switch {
            ChildOnboardingStatus.ModificationRequested => Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyRelationship_ReviewSubmittedEvidence"),
            ChildOnboardingStatus.Rejected => Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyRelationship_RequestRejected"),
            _ => null }, r.Status == ChildOnboardingStatus.ModificationRequested, r.ApprovedPatientId, r.FamilyRelationshipRequestId);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Key(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100;
    private static bool Version(ChildOnboardingRequest r, string value)
    { try { return r.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value)); } catch (FormatException) { return false; } }
    private async Task<string?> EvidenceFingerprintAsync(string metadata, IReadOnlyList<FamilyEvidenceUpload> files, CancellationToken ct)
    {
        if (files.Count == 0 || files.Count > options.Value.MaxEvidenceFiles) return null;
        var parts = new List<string> { metadata };
        foreach (var f in files)
        {
            if (!Enum.IsDefined(f.DocumentType) || f.File.Length <= 0 || f.File.Length > options.Value.MaxEvidenceFileBytes || !f.File.Content.CanSeek) return null;
            f.File.Content.Position = 0;
            parts.Add($"{f.DocumentType}:{Path.GetFileName(f.File.FileName)}:{Convert.ToHexString(await SHA256.HashDataAsync(f.File.Content, ct))}");
            f.File.Content.Position = 0;
        }
        return Hash(string.Join('|', parts));
    }
    private async Task SaveEvidenceAsync(ChildOnboardingRequest r, IReadOnlyList<FamilyEvidenceUpload> files, CancellationToken ct)
    {
        foreach (var file in files)
        {
            var saved = await media.SaveAsync(file.File, new($"ChildOnboarding/{r.Id:N}/{r.Revision}"), ct);
            db.Add(new ChildOnboardingEvidence(r.Id, r.Revision, file.DocumentType, saved.Key, Path.GetFileName(file.File.FileName), saved.ContentType, saved.Length, clock.UtcNow));
        }
        // On an uncertain commit retain staged files. Reconciliation may remove only
        // proven unreferenced files, never compensate by deleting a committed proof.
    }
    public async Task<Result<ChildOnboardingResponse>> SubmitAsync(SubmitChildOnboardingCommand request, CancellationToken ct)
    {
        var actor = await ActorAsync(FamilySelfServicePermissionNames.Onboard, false, ct);
        if (actor?.PatientId is not { } parent) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        var i = request.Intake; var today = options.Value.Today(clock.UtcNow);
        if (i is not null)
        {
            var parentPatient = await store.FindPatientByIdAsync(parent, ct);
            i = i with { PrimaryContactNameAr = i.PrimaryContactNameAr ?? parentPatient?.NameAr,
                PrimaryContactPhone = i.PrimaryContactPhone ?? parentPatient?.PhoneNumber };
        }
        if (!Key(request.IdempotencyKey) || i is null || string.IsNullOrWhiteSpace(i.NameAr) || i.NameAr.Length > 200 || i.NameEn?.Length > 200 ||
            i.DateOfBirth == default || i.DateOfBirth > today || i.DateOfBirth <= today.AddYears(-18) || i.Gender is not (Gender.Male or Gender.Female) ||
            i.RelationToChild is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) || i.PhoneNumber?.Length > 30 || i.PrimaryContactPhone?.Length > 30 ||
            (string.IsNullOrWhiteSpace(i.PhoneNumber) && string.IsNullOrWhiteSpace(i.PrimaryContactPhone)) || i.PrimaryContactNameAr?.Length > 200)
            return Result<ChildOnboardingResponse>.Fail(Invalid);
        var fingerprint = await EvidenceFingerprintAsync(JsonSerializer.Serialize(i), request.Evidence, ct);
        if (fingerprint is null) return Result<ChildOnboardingResponse>.Fail(Invalid);
        await identity.LockAsync(ct);
        var replay = await db.Set<ChildOnboardingRequest>().SingleOrDefaultAsync(r => r.ActorApplicationUserId == actor.User.Id && r.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null) return replay.Fingerprint == fingerprint ? Result<ChildOnboardingResponse>.Ok(Map(replay)) : Result<ChildOnboardingResponse>.Fail(Conflict);
        var intent = Hash($"{FamilyIdentitySearch.Normalize(i.NameAr)}:{i.DateOfBirth:yyyy-MM-dd}:{i.Gender}");
        if (await db.Set<ChildOnboardingRequest>().AnyAsync(r => r.ParentPatientId == parent && r.IntentHash == intent &&
            (r.Status == ChildOnboardingStatus.PendingReview || r.Status == ChildOnboardingStatus.ModificationRequested), ct)) return Result<ChildOnboardingResponse>.Fail(Conflict);
        var row = new ChildOnboardingRequest(actor.User.Id, parent, i.RelationToChild, i.NameAr, i.NameEn, i.DateOfBirth, i.Gender, i.PhoneNumber,
            i.PrimaryContactNameAr ?? string.Empty, i.PrimaryContactPhone ?? string.Empty, request.IdempotencyKey, fingerprint, intent, clock.UtcNow);
        await SaveEvidenceAsync(row, request.Evidence, ct); db.Add(row);
        db.Add(new ChildOnboardingHistory(row.Id, row.Revision, actor.User.Id, "Submitted", clock.UtcNow));
        if (await ActorAsync(FamilySelfServicePermissionNames.Onboard, false, ct) is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        await db.SaveChangesAsync(ct); return Result<ChildOnboardingResponse>.Ok(Map(row));
    }
    public async Task<Result<ChildOnboardingResponse>> ReviewAsync(ReviewChildOnboardingCommand request, CancellationToken ct)
    {
        var actor = await ActorAsync(FamilySelfServicePermissionNames.OnboardingReview, true, ct);
        if (actor is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        if (!Key(request.IdempotencyKey) || request.Decision is not (ChildOnboardingStatus.Approved or ChildOnboardingStatus.Rejected or ChildOnboardingStatus.ModificationRequested) ||
            request.Reason?.Length > 2000 || (request.Decision != ChildOnboardingStatus.Approved && string.IsNullOrWhiteSpace(request.Reason))) return Result<ChildOnboardingResponse>.Fail(Invalid);
        await identity.LockAsync(ct);
        var row = await db.Set<ChildOnboardingRequest>().SingleOrDefaultAsync(r => r.Id == request.RequestId, ct);
        if (row is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        var fingerprint = Hash(JsonSerializer.Serialize(new { request.Decision, request.Reason, request.ConfirmedExistingPatientId }));
        if (row.ReviewIdempotencyKey == request.IdempotencyKey) return row.ReviewFingerprint == fingerprint && row.ReviewedByApplicationUserId == actor.User.Id ?
            Result<ChildOnboardingResponse>.Ok(Map(row, true)) : Result<ChildOnboardingResponse>.Fail(Conflict);
        if (row.Status != ChildOnboardingStatus.PendingReview || !Version(row, request.RowVersion)) return Result<ChildOnboardingResponse>.Fail(Conflict);
        Guid? patientId = null; Guid? relationshipId = null;
        if (request.Decision == ChildOnboardingStatus.Approved)
        {
            var parent = await store.GetAccessSnapshotAsync(row.ActorApplicationUserId, ct);
            var link = await store.FindPatientAccountLinkAsync(row.ActorApplicationUserId, ct);
            if (parent is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient } || link?.PatientId != row.ParentPatientId)
                return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
            var today = options.Value.Today(clock.UtcNow);
            var parentPatient = await store.FindPatientByIdAsync(row.ParentPatientId, ct);
            if (parentPatient is null || parentPatient.GetAge(today) < 18 || !parent.Permissions.Contains(FamilySelfServicePermissionNames.Onboard))
                return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
            if (row.DateOfBirth > today || row.DateOfBirth <= today.AddYears(-18)) return Result<ChildOnboardingResponse>.Fail(Invalid);
            var matches = await FamilyIdentitySearch.Exact(db.Patients.Where(p => p.DateOfBirth == row.DateOfBirth && p.Gender == row.Gender), row.NameAr).Take(6).ToArrayAsync(ct);
            Patient patient;
            if (matches.Length > 0)
            {
                var existing = matches.SingleOrDefault(p => p.Id == request.ConfirmedExistingPatientId);
                if (existing is null) return Result<ChildOnboardingResponse>.Fail(Error.Conflict("ChildOnboarding.PossibleDuplicateReview", Wasla.Domain.Resources.FamilySelfServiceText.Get("ChildOnboarding_PossibleDuplicateReview")));
                patient = existing;
            }
            else
            {
                if (request.ConfirmedExistingPatientId.HasValue) return Result<ChildOnboardingResponse>.Fail(Conflict);
                var created = Patient.Create(Guid.NewGuid(), row.NameAr, row.NameEn, row.DateOfBirth, row.Gender, row.PhoneNumber, null, null, null, null, today);
                if (created.IsFailure) return Result<ChildOnboardingResponse>.Fail(created.Errors);
                patient = created.Value; db.Add(patient);
                if (!string.IsNullOrWhiteSpace(row.ContactPhone))
                {
                    var contact = PatientContact.Create(Guid.NewGuid(), patient.Id, row.ContactNameAr, null, row.ContactPhone,
                        row.ParentRole == FamilyMemberRole.Father ? PatientContactRelationshipType.Father : PatientContactRelationshipType.Mother, row.ParentPatientId, true);
                    if (contact.IsFailure) return Result<ChildOnboardingResponse>.Fail(contact.Errors);
                    db.Add(contact.Value);
                }
                await db.SaveChangesAsync(ct); // Still inside the command's SQL transaction.
            }
            if (string.IsNullOrWhiteSpace(patient.PhoneNumber) && !await db.PatientContacts.AnyAsync(c => c.PatientId == patient.Id && c.IsPrimary && c.PhoneNumber != "", ct))
            {
                var guardianContact = PatientContact.Create(Guid.NewGuid(), patient.Id, row.ContactNameAr, null, row.ContactPhone,
                    row.ParentRole == FamilyMemberRole.Father ? PatientContactRelationshipType.Father : PatientContactRelationshipType.Mother, row.ParentPatientId, true);
                if (guardianContact.IsFailure) return Result<ChildOnboardingResponse>.Fail(guardianContact.Errors);
                db.Add(guardianContact.Value); await db.SaveChangesAsync(ct);
            }
            var adultMember = await store.FindActiveFamilyMemberByPatientIdAsync(row.ParentPatientId, ct);
            var childMember = await store.FindActiveFamilyMemberByPatientIdAsync(patient.Id, ct);
            if (adultMember is not null && childMember is not null && adultMember.FamilyId != childMember.FamilyId) return Result<ChildOnboardingResponse>.Fail(Conflict);
            var type = childMember is not null ? adultMember is null ? FamilyRelationshipRequestType.JoinFamilyAsParent : FamilyRelationshipRequestType.VerifyOperationalParentChild :
                adultMember is null ? FamilyRelationshipRequestType.CreateFamily : FamilyRelationshipRequestType.AddFamilyMember;
            var documents = await db.Set<ChildOnboardingEvidence>().Where(e => e.RequestId == row.Id && e.Revision == row.Revision).ToArrayAsync(ct);
            var streams = new List<Stream>();
            try
            {
                var evidence = new List<FamilyEvidenceUpload>();
                foreach (var d in documents)
                {
                    var file = await privateMedia.OpenAsync(d.MediaKey, ct);
                    if (file is null || !file.Content.CanSeek || file.Content.Length != d.FileSize) return Result<ChildOnboardingResponse>.Fail(Invalid);
                    streams.Add(file.Content); evidence.Add(new(d.DocumentType, new(file.Content, d.FileName, d.ContentType, d.FileSize)));
                }
                var submitted = await familyWorkflow.SubmitAsync(type, childMember?.FamilyId ?? adultMember?.FamilyId, row.ParentPatientId, patient.Id,
                    row.ParentRole, FamilyMemberRole.Child, evidence, row.ActorApplicationUserId, ct);
                if (submitted.IsFailure) return Result<ChildOnboardingResponse>.Fail(submitted.Errors);
                var approved = await familyWorkflow.ApproveAsync(submitted.Value.RequestId, submitted.Value.RowVersion, ct);
                if (approved.IsFailure) return Result<ChildOnboardingResponse>.Fail(approved.Errors);
                relationshipId = approved.Value.RequestId; patientId = patient.Id;
            }
            finally { foreach (var stream in streams) await stream.DisposeAsync(); }
        }
        row.Decide(request.Decision, actor.User.Id, request.Reason, clock.UtcNow, patientId, relationshipId, request.IdempotencyKey, fingerprint);
        db.Add(new ChildOnboardingHistory(row.Id, row.Revision, actor.User.Id, request.Decision.ToString(), clock.UtcNow, request.Reason));
        if (await ActorAsync(FamilySelfServicePermissionNames.OnboardingReview, true, ct) is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        await db.SaveChangesAsync(ct); return Result<ChildOnboardingResponse>.Ok(Map(row, true));
    }
    public async Task<Result<ChildOnboardingResponse>> ResubmitAsync(ResubmitChildOnboardingCommand request, CancellationToken ct)
    {
        var actor = await ActorAsync(FamilySelfServicePermissionNames.OnboardingResubmit, false, ct);
        if (actor is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        var fingerprint = await EvidenceFingerprintAsync(JsonSerializer.Serialize(new { request.RequestId, request.Intake }), request.Evidence, ct);
        if (!Key(request.IdempotencyKey) || fingerprint is null) return Result<ChildOnboardingResponse>.Fail(Invalid);
        await identity.LockAsync(ct);
        var row = await db.Set<ChildOnboardingRequest>().SingleOrDefaultAsync(r => r.Id == request.RequestId && r.ActorApplicationUserId == actor.User.Id, ct);
        if (row is null) return Result<ChildOnboardingResponse>.Fail(DependentAccessErrors.Unavailable);
        var savedRevision = await db.Set<ChildOnboardingRevision>().AsNoTracking().SingleOrDefaultAsync(v => v.RequestId == row.Id && v.ResubmitIdempotencyKey == request.IdempotencyKey, ct);
        if (savedRevision is not null) return savedRevision.ResubmitFingerprint == fingerprint ? Result<ChildOnboardingResponse>.Ok(
            new(row.Id, ChildOnboardingStatus.PendingReview, savedRevision.Revision, row.CreatedAtUtc, savedRevision.ResultRowVersion, null, false)) : Result<ChildOnboardingResponse>.Fail(Conflict);
        if (row.ResubmitIdempotencyKey == request.IdempotencyKey) return row.ResubmitFingerprint == fingerprint ? Result<ChildOnboardingResponse>.Ok(Map(row)) : Result<ChildOnboardingResponse>.Fail(Conflict);
        if (row.Status != ChildOnboardingStatus.ModificationRequested || !Version(row, request.RowVersion)) return Result<ChildOnboardingResponse>.Fail(Conflict);
        if (!await db.Set<ChildOnboardingRevision>().AnyAsync(v => v.RequestId == row.Id && v.Revision == row.Revision, ct)) db.Add(new ChildOnboardingRevision(row, clock.UtcNow));
        if (request.Intake is { } revised)
        {
            var today = options.Value.Today(clock.UtcNow);
            if (string.IsNullOrWhiteSpace(revised.NameAr) || revised.NameAr.Length > 200 || revised.NameEn?.Length > 200 || revised.DateOfBirth == default ||
                revised.DateOfBirth > today || revised.DateOfBirth <= today.AddYears(-18) || revised.Gender is not (Gender.Male or Gender.Female) ||
                revised.RelationToChild != row.ParentRole || revised.PhoneNumber?.Length > 30 || string.IsNullOrWhiteSpace(revised.PrimaryContactNameAr) ||
                revised.PrimaryContactNameAr.Length > 200 || string.IsNullOrWhiteSpace(revised.PrimaryContactPhone) || revised.PrimaryContactPhone.Length > 30)
                return Result<ChildOnboardingResponse>.Fail(Invalid);
            var intent = Hash($"{FamilyIdentitySearch.Normalize(revised.NameAr)}:{revised.DateOfBirth:yyyy-MM-dd}:{revised.Gender}");
            if (await db.Set<ChildOnboardingRequest>().AnyAsync(r => r.Id != row.Id && r.ParentPatientId == row.ParentPatientId && r.IntentHash == intent &&
                (r.Status == ChildOnboardingStatus.PendingReview || r.Status == ChildOnboardingStatus.ModificationRequested), ct)) return Result<ChildOnboardingResponse>.Fail(Conflict);
            row.CorrectPendingIntake(revised.NameAr, revised.NameEn, revised.DateOfBirth, revised.Gender, revised.PhoneNumber, revised.PrimaryContactNameAr, revised.PrimaryContactPhone, intent);
        }
        row.Resubmit(request.IdempotencyKey, fingerprint); await SaveEvidenceAsync(row, request.Evidence, ct);
        db.Add(new ChildOnboardingHistory(row.Id, row.Revision, actor.User.Id, "Resubmitted", clock.UtcNow));
        await db.SaveChangesAsync(ct);
        db.Add(new ChildOnboardingRevision(row, clock.UtcNow)); await db.SaveChangesAsync(ct);
        return Result<ChildOnboardingResponse>.Ok(Map(row));
    }
    public async Task<Result<IReadOnlyList<ChildOnboardingReview>>> ReadAsync(GetChildOnboardingRequestsQuery request, CancellationToken ct)
    {
        var permission = request.Admin ? request.RequestId.HasValue ? FamilySelfServicePermissionNames.OnboardingDetails : FamilySelfServicePermissionNames.OnboardingInbox : FamilySelfServicePermissionNames.OnboardingOwn;
        var actor = await ActorAsync(permission, request.Admin, ct);
        if (actor is null) return Result<IReadOnlyList<ChildOnboardingReview>>.Fail(DependentAccessErrors.Unavailable);
        if (request.PageNumber < 1 || request.PageNumber > 10000 || request.PageSize is < 1 or > 100) return Result<IReadOnlyList<ChildOnboardingReview>>.Fail(Invalid);
        var rows = await db.Set<ChildOnboardingRequest>().AsNoTracking().Where(r => (!request.RequestId.HasValue || r.Id == request.RequestId) &&
            (request.Admin || r.ActorApplicationUserId == actor.User.Id)).OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(ct);
        var result = new List<ChildOnboardingReview>();
        foreach (var row in rows)
        {
            var docs = await db.Set<ChildOnboardingEvidence>().AsNoTracking().Where(e => e.RequestId == row.Id).OrderBy(e => e.Revision).ThenBy(e => e.Id).ToArrayAsync(ct);
            var matches = request.Admin && request.RequestId.HasValue ? await FamilyIdentitySearch.Exact(db.Patients.AsNoTracking().Where(p =>
                    p.DateOfBirth == row.DateOfBirth && p.Gender == row.Gender), row.NameAr).OrderBy(p => p.Id).Take(5)
                    .Select(p => new ChildOnboardingDuplicateCandidate(p.Id, p.NameAr, p.NameEn, p.DateOfBirth, p.Gender)).ToArrayAsync(ct) : [];
            result.Add(new(Map(row, request.Admin), new(row.NameAr, row.NameEn, row.DateOfBirth, row.Gender, row.PhoneNumber, row.ContactNameAr, row.ContactPhone, row.ParentRole),
                docs.Select(d => new FamilyRelationshipDocumentResponse(d.Id, d.Revision, d.DocumentType, d.FileName, d.ContentType, d.FileSize, d.CreatedAtUtc)).ToArray(), matches));
        }
        return Result<IReadOnlyList<ChildOnboardingReview>>.Ok(result);
    }
    public async Task<Result<PrivateMedia>> EvidenceAsync(GetChildOnboardingEvidenceQuery request, CancellationToken ct)
    {
        if (await ActorAsync(FamilySelfServicePermissionNames.OnboardingEvidence, true, ct) is null) return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable);
        var document = await db.Set<ChildOnboardingEvidence>().AsNoTracking().SingleOrDefaultAsync(e => e.RequestId == request.RequestId && e.Id == request.EvidenceId, ct);
        if (document is null) return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable);
        var opened = await privateMedia.OpenAsync(document.MediaKey, ct);
        if (opened is null) return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable);
        if (await ActorAsync(FamilySelfServicePermissionNames.OnboardingEvidence, true, ct) is null) { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable); }
        return Result<PrivateMedia>.Ok(opened with { FileName = document.FileName, ContentType = document.ContentType });
    }
}
