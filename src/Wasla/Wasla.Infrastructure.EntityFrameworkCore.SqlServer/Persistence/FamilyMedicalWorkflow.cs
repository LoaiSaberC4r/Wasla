using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Media;
using Wasla.Domain.Families;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class FamilyMedicalWorkflow(WaslaDbContext db, FamilyMedicalAccessPolicy access, ICurrentUser current,
    IDateTimeProvider clock, ITicketQueueLock locks, IMediaService media, IMediaUploadValidator validator,
    IPrivateMediaReader privateMedia, IFamilyMedicalMediaCompensation compensation) : IFamilyMedicalWorkflow
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = MakeJsonOptions();
    private static JsonSerializerOptions MakeJsonOptions()
    { var o = new JsonSerializerOptions(JsonSerializerDefaults.Web); o.Converters.Add(new JsonStringEnumConverter()); return o; }
    private static string CommandPermission(string op) => op switch
    {
        "create" => PermissionNames.FamilyMedicalAccessRequestsCreateOwn, "withdraw" => PermissionNames.FamilyMedicalAccessRequestsWithdrawOwn,
        "resubmit" => PermissionNames.FamilyMedicalAccessRequestsResubmitOwn, "request-modification" => PermissionNames.FamilyMedicalAccessRequestsRequestModification,
        "eligibility-revoke" => PermissionNames.FamilyMedicalAccessEligibilityRevoke,
        "eligibility-approve" or "eligibility-reject" => PermissionNames.FamilyMedicalAccessEligibilityDecide,
        _ => PermissionNames.FamilyMedicalAccessRequestsDecide
    };
    private static bool AdminOperation(string op) => op is not ("create" or "withdraw" or "resubmit");
    private Task PairLockAsync(Guid family, Guid adult, Guid child, CancellationToken ct)
        => locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:FamilyPair", $"{family:N}:{adult:N}:{child:N}", ct);
    public async Task<Result<object>> ExecuteAsync(FamilyMedicalCommand c, CancellationToken ct)
    {
        var actor = await access.ActorAsync(CommandPermission(c.Operation), AdminOperation(c.Operation), ct);
        if (actor is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
        var user = actor.User.Id;
        var applicant = AdminOperation(c.Operation) ? null : await access.PatientAsync(CommandPermission(c.Operation), ct);
        var member = applicant is { } p ? await access.MemberAsync(p, ct) : null;
        if (!AdminOperation(c.Operation) && (member is null || !member.CanManageFamily)) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
        if (c.Operation.StartsWith("eligibility-", StringComparison.Ordinal)) return await EligibilityAsync(c, user, ct);
        FamilyMedicalAccessRequest? request = null;
        Guid family, adult, child;
        if (c.Operation == "create") { family = member!.FamilyId; adult = c.AffectedAdultPatientId!.Value; child = c.ChildPatientId!.Value; }
        else
        {
            var route = await db.Set<FamilyMedicalAccessRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == c.RequestId, ct);
            if (route is null || !AdminOperation(c.Operation) && (route.SubmittedByPatientId != applicant || route.SubmittedByApplicationUserId != user || route.FamilyId != member!.FamilyId))
                return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
            family = route.FamilyId; adult = route.AffectedAdultPatientId; child = route.ChildPatientId;
        }
        await PairLockAsync(family, adult, child, ct);
        if (c.Operation != "create") request = await db.Set<FamilyMedicalAccessRequest>().SingleAsync(r => r.Id == c.RequestId, ct);
        var uploads = await PrepareEvidenceAsync(c, ct);
        if (uploads.IsFailure) return Result<object>.Fail(uploads.Errors);
        try
        {
            async Task<Result<object>> Change()
            {
                if (c.Operation is "create" or "resubmit" or "approve" && !access.Configured) return Result<object>.Fail(FamilyMedicalErrors.PolicyUnavailable);
                if (c.Operation is "create" or "resubmit" or "approve")
                {
                    if (!await access.PairAsync(family, adult, child, ct) || c.Operation == "create" && !await access.PairAsync(family, applicant!.Value, child, ct))
                        return Result<object>.Fail(c.Operation == "approve" ? FamilyMedicalErrors.Conflict : FamilyMedicalErrors.Unavailable);
                }
                var active = await db.Set<FamilyMedicalAccessRestriction>().SingleOrDefaultAsync(r => r.FamilyId == family && r.ChildPatientId == child && r.AffectedAdultPatientId == adult && r.Status == FamilyMedicalRestrictionStatus.Active, ct);
                if (c.Operation == "create")
                {
                    if (c.RequestType == FamilyMedicalRequestType.Restriction ? active is not null : active is null) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                    if (await db.Set<FamilyMedicalAccessRequest>().AnyAsync(r => r.FamilyId == family && r.ChildPatientId == child && r.AffectedAdultPatientId == adult && r.RequestType == c.RequestType &&
                        (r.Status == FamilyMedicalRequestStatus.Pending || r.Status == FamilyMedicalRequestStatus.ModificationRequested), ct)) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                    var created = FamilyMedicalAccessRequest.Create(family, child, adult, applicant!.Value, user, c.RequestType!.Value, c.Reason!, clock.UtcNow, active?.Id);
                    if (created.IsFailure) return Result<object>.Fail(created.Errors);
                    request = created.Value; db.Add(request);
                    // Persist the principal before linking evidence/history; all saves belong to the command transaction.
                    await db.SaveChangesAsync(ct);
                    db.Add(FamilyMedicalAccessRequestHistory.Create(request.Id, "Submitted", null, (int)request.Status, 1, user, clock.UtcNow, request.Reason));
                }
                else
                {
                    if (!Matches(request!.RowVersion, c.RowVersion)) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                    var old = request.Status;
                    Guid? restrictionId = null;
                    if (c.Operation == "approve")
                    {
                        if (request.Status != FamilyMedicalRequestStatus.Pending) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                        if (request.RequestType == FamilyMedicalRequestType.Restriction)
                        {
                            if (string.IsNullOrWhiteSpace(c.BasisReference) || c.BasisReference.Length > 500 ||
                                !access.Policy.RestrictionGroundsReferences.Contains(c.BasisReference, StringComparer.Ordinal) ||
                                !await RestrictionEvidenceCurrentAsync(request, ct))
                                return Result<object>.Fail(FamilyMedicalErrors.PolicyUnavailable);
                            if (active is not null) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                            db.Add(FamilyMedicalAccessRequestHistory.Create(request.Id, "RestrictionGroundReviewed", (int)old, (int)old,
                                request.CurrentRevisionNumber, user, clock.UtcNow, c.BasisReference));
                            var restriction = FamilyMedicalAccessRestriction.Activate(request, user, clock.UtcNow); db.Add(restriction); restrictionId = restriction.Id;
                            db.Add(FamilyMedicalAccessRestrictionHistory.Create(restriction.Id, "Restricted", null, (int)restriction.Status, user, clock.UtcNow));
                        }
                        else
                        {
                            if (active is null || active.Id != request.AffectedRestrictionId) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
                            var lifted = active.Lift(request, user, clock.UtcNow); if (lifted.IsFailure) return Result<object>.Fail(lifted.Errors);
                            restrictionId = active.Id; db.Add(FamilyMedicalAccessRestrictionHistory.Create(active.Id, "Lifted", 1, 2, user, clock.UtcNow));
                        }
                    }
                    var transitioned = request.Transition(c.Operation, user, clock.UtcNow, c.Reason, restrictionId);
                    if (transitioned.IsFailure) return Result<object>.Fail(transitioned.Errors);
                    db.Add(FamilyMedicalAccessRequestHistory.Create(request.Id, c.Operation, (int)old, (int)request.Status, request.CurrentRevisionNumber, user, clock.UtcNow, c.Reason));
                }
                if (c.Operation is "create" or "resubmit") await SaveEvidenceAsync(request!, uploads.Value, user, ct);
                if (await access.ActorAsync(CommandPermission(c.Operation), AdminOperation(c.Operation), ct) is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
                await db.SaveChangesAsync(ct);
                return Result<object>.Ok(await CaseAsync(request!, AdminOperation(c.Operation), ct));
            }
            return await IdempotentAsync(c, user, family, adult, child, uploads.Value, Change, ct);
        }
        finally { foreach (var item in uploads.Value) await item.Upload.DisposeAsync(); }
    }
    private async Task<Result<object>> EligibilityAsync(FamilyMedicalCommand c, Guid user, CancellationToken ct)
    {
        var route = await db.Set<FamilyMedicalEligibilityReview>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == c.RequestId, ct);
        if (route is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
        await PairLockAsync(route.FamilyId, route.AdultPatientId, route.ChildPatientId, ct);
        var r = await db.Set<FamilyMedicalEligibilityReview>().SingleAsync(r => r.Id == route.Id, ct);
        async Task<Result<object>> Change()
        {
            if (!Matches(r.RowVersion, c.RowVersion)) return Result<object>.Fail(FamilyMedicalErrors.Conflict);
            var operation = c.Operation[12..];
            if (operation == "approve")
            {
                if (!access.Configured || c.PolicyVersion != access.Policy.PolicyVersion || !access.Policy.BasisReferences.Contains(c.BasisReference!, StringComparer.Ordinal))
                    return Result<object>.Fail(FamilyMedicalErrors.PolicyUnavailable);
                if (!await access.PairAsync(r.FamilyId, r.AdultPatientId, r.ChildPatientId, ct)) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
                var members = await db.FamilyMembers.AsNoTracking().Where(m => m.Id == r.AdultFamilyMemberId || m.Id == r.ChildFamilyMemberId).ToArrayAsync(ct);
                if (members.Length != 2 || members.Any(m => !m.IsActive)) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
                var adult = members.Single(m => m.Id == r.AdultFamilyMemberId);
                var child = members.Single(m => m.Id == r.ChildFamilyMemberId);
                if (adult.PatientId != r.AdultPatientId || child.PatientId != r.ChildPatientId ||
                    adult.Role is not (FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian) ||
                    await access.AuthorityVersionAsync(r.FamilyId, adult, child, c.AuthorityRelationshipRequestId, ct) is null ||
                    !await AuthorityMediaCurrentAsync(c.AuthorityRelationshipRequestId!.Value, ct))
                    return Result<object>.Fail(FamilyMedicalErrors.PolicyUnavailable);
            }
            var old = r.Status; var decided = r.Decide(operation, user, clock.UtcNow, c.PolicyVersion, c.BasisReference, c.Reason, c.AuthorityRelationshipRequestId);
            if (decided.IsFailure) return Result<object>.Fail(decided.Errors);
            // Keep each approval's provenance after later re-verification replaces the current reference.
            var decisionMetadata = operation == "approve"
                ? $"PolicyVersion={r.PolicyVersion}; BasisReference={r.BasisReference}; AuthorityRelationshipRequestId={r.AuthorityRelationshipRequestId:D}"
                : c.Reason;
            db.Add(FamilyMedicalEligibilityReviewHistory.Create(r.Id, c.Operation, (int)old, (int)r.Status, user, clock.UtcNow, decisionMetadata));
            if (await access.ActorAsync(CommandPermission(c.Operation), true, ct) is null) return Result<object>.Fail(FamilyMedicalErrors.Unavailable);
            await db.SaveChangesAsync(ct); return Result<object>.Ok(await EligibilityResponseAsync(r, ct));
        }
        return await IdempotentAsync(c, user, r.FamilyId, r.AdultPatientId, r.ChildPatientId, [], Change, ct);
    }
    private static bool Matches(byte[] actual, string? supplied)
    { try { return supplied is not null && actual.AsSpan().SequenceEqual(Convert.FromBase64String(supplied)); } catch (FormatException) { return false; } }
    private async Task<bool> RestrictionEvidenceCurrentAsync(FamilyMedicalAccessRequest request, CancellationToken ct)
    {
        var docs = await db.Set<FamilyMedicalAccessRequestDocument>().AsNoTracking().Where(d => d.RequestId == request.Id &&
            d.RevisionNumber == request.CurrentRevisionNumber).ToArrayAsync(ct);
        if (docs.Length == 0 || docs.Length > access.Policy.MaximumFiles || docs.Any(d => !access.Policy.AllowedDocumentTypes.Contains(d.DocumentType) ||
            d.SizeBytes <= 0 || d.SizeBytes > access.Policy.MaximumFileBytes)) return false;
        foreach (var doc in docs)
        {
            PrivateMedia? opened;
            try { opened = await privateMedia.OpenAsync(doc.MediaKey, ct); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
            if (opened is null) return false;
            await using var stream = opened.Content;
            if (!stream.CanSeek || stream.Length != doc.SizeBytes) return false;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            if (!string.Equals(hash, doc.Sha256, StringComparison.Ordinal)) return false;
        }
        return true;
    }
    private async Task<bool> AuthorityMediaCurrentAsync(Guid requestId, CancellationToken ct)
    {
        var docs = await db.FamilyRelationshipDocuments.AsNoTracking().Where(d => d.FamilyRelationshipRequestId == requestId &&
            db.FamilyRelationshipRequests.Any(r => r.Id == requestId && r.CurrentRevisionNumber == d.RevisionNumber)).ToArrayAsync(ct);
        if (docs.Length == 0) return false;
        foreach (var doc in docs)
        {
            PrivateMedia? opened;
            try { opened = await privateMedia.OpenAsync(doc.MediaKey, ct); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
            if (opened is null) return false;
            await using var stream = opened.Content;
            if (!stream.CanSeek || stream.Length != doc.FileSize) return false;
        }
        return true;
    }
    private sealed record PreparedEvidence(FamilyRelationshipDocumentType Type, ValidatedMediaUpload Upload, string Hash);
    private async Task<Result<IReadOnlyList<PreparedEvidence>>> PrepareEvidenceAsync(FamilyMedicalCommand c, CancellationToken ct)
    {
        var files = c.Evidence ?? [];
        if (c.Operation is not ("create" or "resubmit")) return Result<IReadOnlyList<PreparedEvidence>>.Ok([]);
        if (!access.Configured) return Result<IReadOnlyList<PreparedEvidence>>.Fail(FamilyMedicalErrors.PolicyUnavailable);
        var type = c.RequestType;
        if (type is null && c.RequestId is { } id) type = await db.Set<FamilyMedicalAccessRequest>().Where(r => r.Id == id).Select(r => r.RequestType).SingleAsync(ct);
        var required = type == FamilyMedicalRequestType.Restriction ? access.Policy.RestrictionEvidenceRequired : access.Policy.RestorationEvidenceRequired;
        if (files.Count > access.Policy.MaximumFiles || required && files.Count == 0 || files.Any(f => !access.Policy.AllowedDocumentTypes.Contains(f.DocumentType)))
            return Result<IReadOnlyList<PreparedEvidence>>.Fail(FamilyMedicalErrors.Invalid);
        var prepared = new List<PreparedEvidence>();
        try
        {
            foreach (var file in files)
            {
                if (file.File.Length is > 0 && file.File.Length > access.Policy.MaximumFileBytes) return await FailAsync();
                var upload = await validator.ValidateAsync(file.File, ct);
                if (upload.Length > access.Policy.MaximumFileBytes || upload.ContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
                { await upload.DisposeAsync(); return await FailAsync(); }
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(upload.Content, ct)); upload.Content.Position = 0;
                prepared.Add(new(file.DocumentType, upload, hash));
            }
            return Result<IReadOnlyList<PreparedEvidence>>.Ok(prepared);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or MediaServiceException)
        { return await FailAsync(); }
        async Task<Result<IReadOnlyList<PreparedEvidence>>> FailAsync()
        { foreach (var item in prepared) await item.Upload.DisposeAsync(); return Result<IReadOnlyList<PreparedEvidence>>.Fail(FamilyMedicalErrors.Invalid); }
    }
    private async Task SaveEvidenceAsync(FamilyMedicalAccessRequest request, IReadOnlyList<PreparedEvidence> files, Guid actor, CancellationToken ct)
    {
        foreach (var f in files)
        {
            var stored = await media.SaveAsync(new MediaUpload(f.Upload.Content, f.Upload.SafeFileName, f.Upload.ContentType, f.Upload.Length), new($"FamilyMedicalRequests/{request.Id:N}/Revision-{request.CurrentRevisionNumber}"), ct);
            compensation.Track(stored.Key);
            db.Add(FamilyMedicalAccessRequestDocument.Create(request.Id, request.CurrentRevisionNumber, f.Type, stored.Key,
                Path.GetFileName(f.Upload.SafeFileName), stored.ContentType, stored.Length, f.Hash, actor, clock.UtcNow));
        }
    }
    private async Task<Result<object>> IdempotentAsync(FamilyMedicalCommand c, Guid actor, Guid family, Guid adult, Guid child,
        IReadOnlyList<PreparedEvidence> files, Func<Task<Result<object>>> change, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.IdempotencyKey)) return await change();
        var operation = "Phase16Family:" + c.Operation; var key = c.IdempotencyKey.Trim();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { actor, family, adult, child,
            c.RequestId, c.RequestType, Reason = c.Reason?.Trim(), c.RowVersion, c.PolicyVersion, c.BasisReference, c.AuthorityRelationshipRequestId,
            Evidence = files.Select(f => new { f.Type, Name = f.Upload.SafeFileName, f.Upload.ContentType, f.Upload.Length, f.Hash }).ToArray() }))));
        await locks.AcquireIdempotencyAsync(actor, operation, key, ct);
        var existing = await db.MedicationIdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.ActorApplicationUserId == actor && r.Operation == operation && r.IdempotencyKey == key, ct);
        if (existing is not null) return existing.RequestFingerprint != fingerprint || existing.ResponseJson is null ? Result<object>.Fail(FamilyMedicalErrors.Conflict)
            : Result<object>.Ok(JsonSerializer.Deserialize<JsonElement>(existing.ResponseJson));
        var result = await change(); if (result.IsFailure) return result;
        var record = MedicationIdempotencyRecord.Create(actor, operation, key, fingerprint, clock.UtcNow); record.Complete(JsonSerializer.Serialize(result.Value, ResponseJsonOptions));
        db.Add(record); await db.SaveChangesAsync(ct); return result;
    }
}
