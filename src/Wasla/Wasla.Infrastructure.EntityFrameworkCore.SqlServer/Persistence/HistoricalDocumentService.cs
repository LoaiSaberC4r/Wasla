using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class HistoricalDocumentService(WaslaDbContext db, IWaslaDataStore store, ICurrentUser current,
    IFamilyMedicalOperationPolicy medical, IPatientIdentityStore identities, IMediaService media, IPrivateMediaReader reader,
    IDateTimeProvider clock, MedicalRecordAuditWriter audit, IOptions<HistoricalDocumentOptions> options,
    IOptions<DependentManagementOptions> calendar, IEnumerable<IMalwareScanner> scanners) : IHistoricalDocumentService
{
    private static Error Missing => Error.NotFound("HistoricalDocument.NotFound", Wasla.Domain.Resources.FamilySelfServiceText.Get("HistoricalDocument_NotFound"));
    private static Error Invalid => Error.Validation("HistoricalDocument.Invalid", Wasla.Domain.Resources.FamilySelfServiceText.Get("HistoricalDocument_Invalid"));
    private static Error Conflict => Error.Conflict("HistoricalDocument.VersionConflict", Wasla.Domain.Resources.FamilySelfServiceText.Get("HistoricalDocument_VersionConflict"));
    internal async Task<MedicalRecordScope?> ScopeAsync(Guid? child, string ownPermission, string dependentPermission, bool adult, CancellationToken ct)
    {
        if (!options.Value.Enabled || !current.IsAuthenticated || current.UserId is not { } user) return null;
        var permission = child.HasValue ? dependentPermission : ownPermission;
        var snapshot = await store.GetAccessSnapshotAsync(user, ct);
        if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient, PatientId: not null } ||
            !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) || !snapshot.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return null;
        var link = await store.FindPatientAccountLinkAsync(user, ct);
        if (link is null || link.PatientId != snapshot.PatientId) return null;
        if (child is { } id)
        { var result = await medical.ScopeAsync(id, dependentPermission, ct); return result.IsSuccess ? result.Value : null; }
        var patient = await store.FindPatientByIdAsync(link.PatientId, ct);
        if (patient is null || adult && patient.GetAge(calendar.Value.Today(clock.UtcNow)) < 18) return null;
        return new(user, patient.Id, MedicalRecordMode.OwnPatient);
    }
    private static bool Version(PatientHistoricalDocument d, string? value)
    { try { return value is not null && d.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value)); } catch (FormatException) { return false; } }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task<string?> FingerprintAsync(HistoricalDocumentMutation c, CancellationToken ct)
    {
        var parts = new List<string> { JsonSerializer.Serialize(new { c.Operation, c.ChildPatientId, c.DocumentId, c.RowVersion, c.Metadata, c.Reason }) };
        var files = c.Files ?? [];
        if (c.Operation != "withdraw" && (files.Count == 0 || files.Count > options.Value.MaxFiles)) return null;
        foreach (var f in files)
        {
            if (!f.Content.CanSeek || f.Content.Length <= 0 || f.Content.Length > options.Value.MaxFileBytes || f.Length != f.Content.Length ||
                Path.GetExtension(f.FileName).ToLowerInvariant() is not (".pdf" or ".jpg" or ".jpeg" or ".png")) return null;
            if (!await HistoricalDocumentSafety.IsPassiveAsync(f.Content, Path.GetExtension(f.FileName), ct)) return null;
            f.Content.Position = 0; var digest = Convert.ToHexString(await SHA256.HashDataAsync(f.Content, ct)); f.Content.Position = 0;
            parts.Add($"{Path.GetFileName(f.FileName)}:{digest}");
        }
        return Hash(string.Join('|', parts));
    }
    public async Task<Result<HistoricalDocumentMutationResponse>> MutateAsync(HistoricalDocumentMutation c, CancellationToken ct)
    {
        var (own, dependent) = c.Operation switch {
            "create" => (MedicalDocumentPermissionNames.CreateOwn, MedicalDocumentPermissionNames.CreateDependent),
            "revise" => (MedicalDocumentPermissionNames.ReviseOwn, MedicalDocumentPermissionNames.ReviseDependent),
            "withdraw" => (MedicalDocumentPermissionNames.WithdrawOwn, MedicalDocumentPermissionNames.WithdrawDependent), _ => ("", "") };
        var scope = await ScopeAsync(c.ChildPatientId, own, dependent, c.Operation != "withdraw", ct);
        if (scope is null) return Result<HistoricalDocumentMutationResponse>.Fail(Missing);
        if (own.Length == 0 || string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 100 || c.Reason?.Length > 1000 ||
            c.Operation != "create" && c.DocumentId is null) return Result<HistoricalDocumentMutationResponse>.Fail(Invalid);
        var m = c.Metadata;
        if (c.Operation != "withdraw" && (m is null || !Enum.IsDefined(m.Category) || m.DateUnknown == m.DocumentDate.HasValue ||
            m.DocumentDate == default(DateOnly) || m.DocumentDate > calendar.Value.Today(clock.UtcNow) || m.ProviderName?.Length > 200 || m.Title?.Length > 200 || m.PatientNote?.Length > 2000))
            return Result<HistoricalDocumentMutationResponse>.Fail(Invalid);
        if (c.Operation is "revise" or "withdraw" && string.IsNullOrWhiteSpace(c.Reason)) return Result<HistoricalDocumentMutationResponse>.Fail(Invalid);
        var fingerprint = await FingerprintAsync(c, ct); if (fingerprint is null) return Result<HistoricalDocumentMutationResponse>.Fail(Invalid);
        await identities.LockAsync(ct);
        var replay = await db.Set<HistoricalDocumentOperation>().SingleOrDefaultAsync(r => r.ActorUserId == scope.ActorId && r.Operation == c.Operation && r.IdempotencyKey == c.IdempotencyKey, ct);
        if (replay is not null) return replay.SubjectPatientId == scope.PatientId && replay.Fingerprint == fingerprint ?
            Result<HistoricalDocumentMutationResponse>.Ok(new(replay.DocumentId, replay.VersionNumber)) : Result<HistoricalDocumentMutationResponse>.Fail(Conflict);
        var doc = c.Operation == "create" ? null : await db.Set<PatientHistoricalDocument>().SingleOrDefaultAsync(d => d.Id == c.DocumentId && d.SubjectPatientId == scope.PatientId, ct);
        if (c.Operation != "create" && doc is null) return Result<HistoricalDocumentMutationResponse>.Fail(Missing);
        if (doc is not null && (!Version(doc, c.RowVersion) || doc.State != HistoricalDocumentState.AvailableUnverified)) return Result<HistoricalDocumentMutationResponse>.Fail(Conflict);
        // A proxy may revise/withdraw only their own upload. Subject ownership remains permanent.
        if (doc is not null && c.ChildPatientId.HasValue && doc.UploadedByApplicationUserId != scope.ActorId) return Result<HistoricalDocumentMutationResponse>.Fail(Missing);
        if (c.Operation != "withdraw")
        {
            if (options.Value.RequireRealScanner && !scanners.Any(s => s is ClamAvMalwareScanner))
                return Result<HistoricalDocumentMutationResponse>.Fail(Error.Conflict("HistoricalDocument.ScannerUnavailable", Wasla.Domain.Resources.FamilySelfServiceText.Get("HistoricalDocument_ScannerUnavailable")));
            var day = clock.UtcNow.Date;
            var writes = await db.Set<HistoricalDocumentOperation>().CountAsync(o => o.ActorUserId == scope.ActorId && o.CreatedAtUtc >= day, ct);
            var count = await db.Set<PatientHistoricalDocument>().CountAsync(d => d.SubjectPatientId == scope.PatientId, ct);
            var used = await (from a in db.Set<PatientHistoricalDocumentAttachment>() join v in db.Set<PatientHistoricalDocumentVersion>() on a.VersionId equals v.Id
                join d in db.Set<PatientHistoricalDocument>() on v.DocumentId equals d.Id where d.SubjectPatientId == scope.PatientId select (long?)a.Length).SumAsync(ct) ?? 0;
            if (writes >= options.Value.MaxWritesPerDay || c.Operation == "create" && count >= options.Value.MaxDocumentsPerSubject || used + c.Files!.Sum(f => f.Content.Length) > options.Value.MaxBytesPerSubject)
                return Result<HistoricalDocumentMutationResponse>.Fail(Error.Conflict("HistoricalDocument.QuotaExceeded", Wasla.Domain.Resources.FamilySelfServiceText.Get("HistoricalDocument_QuotaExceeded")));
        }
        if (doc is null)
        {
            var link = await store.FindPatientAccountLinkAsync(scope.ActorId, ct);
            doc = new(scope.PatientId, scope.ActorId, link!.PatientId, clock.UtcNow); db.Add(doc);
        }
        if (c.Operation == "withdraw") doc.Withdraw(clock.UtcNow);
        else
        {
            var version = new PatientHistoricalDocumentVersion(doc.Id, doc.CurrentVersionNumber + 1, m!.Category, m.DocumentDate, m.DateUnknown,
                Trim(m.ProviderName), Trim(m.Title), Trim(m.PatientNote), scope.ActorId, clock.UtcNow, Trim(c.Reason), doc.UploadedAtUtc);
            foreach (var file in c.Files!)
            {
                // Private media is validated and scanned before any available metadata is committed.
                var saved = await media.SaveAsync(file, new($"HistoricalDocuments/{doc.Id:N}/{version.Id:N}"), ct);
                file.Content.Position = 0; var hash = Convert.ToHexString(await SHA256.HashDataAsync(file.Content, ct)); file.Content.Position = 0;
                db.Add(new PatientHistoricalDocumentAttachment(version.Id, saved.Key, Path.GetFileName(file.FileName), saved.ContentType, saved.Length, hash, clock.UtcNow));
            }
            db.Add(version); doc.PublishValidatedVersion(version.VersionNumber, clock.UtcNow);
        }
        if (await ScopeAsync(c.ChildPatientId, own, dependent, c.Operation != "withdraw", ct) != scope) return Result<HistoricalDocumentMutationResponse>.Fail(Missing);
        db.Add(new PatientHistoricalDocumentHistory(doc.Id, doc.CurrentVersionNumber, scope.ActorId, c.Operation, clock.UtcNow));
        db.Add(new HistoricalDocumentOperation(scope.ActorId, scope.PatientId, c.Operation, c.IdempotencyKey, fingerprint, doc.Id, doc.CurrentVersionNumber, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result<HistoricalDocumentMutationResponse>.Ok(new(doc.Id, doc.CurrentVersionNumber));
    }
    private async Task<HistoricalVersionResponse> VersionAsync(PatientHistoricalDocumentVersion v, CancellationToken ct)
    {
        var attachments = await db.Set<PatientHistoricalDocumentAttachment>().AsNoTracking().Where(a => a.VersionId == v.Id).OrderBy(a => a.Id)
            .Select(a => new HistoricalAttachmentResponse(a.Id, a.FileName, a.ContentType, a.Length)).ToArrayAsync(ct);
        return new(v.VersionNumber, new(v.Category, v.DocumentDate, v.DateUnknown, v.ProviderName, v.Title, v.PatientNote), v.CreatedAtUtc, attachments);
    }
    public async Task<Result<object>> ReadAsync(HistoricalDocumentQuery q, CancellationToken ct)
    {
        var scope = await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ViewOwn, MedicalDocumentPermissionNames.ViewDependent, false, ct);
        if (scope is null) return Result<object>.Fail(Missing);
        if (q.PageNumber is < 1 or > 10000 || q.PageSize is < 1 or > 100 || q.Versions && q.DocumentId is null) return Result<object>.Fail(Invalid);
        var query = db.Set<PatientHistoricalDocument>().AsNoTracking().Where(d => d.SubjectPatientId == scope.PatientId &&
            (d.State == HistoricalDocumentState.AvailableUnverified || q.IncludeWithdrawn && d.State == HistoricalDocumentState.Withdrawn));
        if (q.DocumentId is { } id) query = query.Where(d => d.Id == id);
        var ordered = query.OrderByDescending(d => d.UploadedAtUtc).ThenBy(d => d.Id);
        var docs = q.DocumentId.HasValue ? await ordered.Take(1).ToArrayAsync(ct) :
            await ordered.Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
        object result;
        if (q.Versions)
        {
            if (docs.Length != 1) return Result<object>.Fail(Missing);
            var versions = await db.Set<PatientHistoricalDocumentVersion>().AsNoTracking().Where(v => v.DocumentId == docs[0].Id).OrderByDescending(v => v.VersionNumber).Skip((q.PageNumber - 1) * q.PageSize).Take(q.PageSize).ToArrayAsync(ct);
            var output = new List<HistoricalVersionResponse>(); foreach (var v in versions) output.Add(await VersionAsync(v, ct)); result = output;
        }
        else
        {
            var output = new List<HistoricalDocumentResponse>();
            var revise = await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ReviseOwn, MedicalDocumentPermissionNames.ReviseDependent, true, ct) is not null;
            var withdraw = await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.WithdrawOwn, MedicalDocumentPermissionNames.WithdrawDependent, false, ct) is not null;
            foreach (var d in docs)
            {
                var v = await db.Set<PatientHistoricalDocumentVersion>().AsNoTracking().SingleAsync(v => v.DocumentId == d.Id && v.VersionNumber == d.CurrentVersionNumber, ct);
                var editable = d.State == HistoricalDocumentState.AvailableUnverified && (!q.ChildPatientId.HasValue || d.UploadedByApplicationUserId == scope.ActorId);
                output.Add(new(d.Id, d.SubjectPatientId, d.State, d.CurrentVersionNumber, d.UploadedAtUtc, Convert.ToBase64String(d.RowVersion), await VersionAsync(v, ct), editable && revise, editable && withdraw));
            }
            if (q.DocumentId.HasValue && output.Count == 0) return Result<object>.Fail(Missing);
            result = q.DocumentId.HasValue ? output[0] : new { Items = output, q.PageNumber, q.PageSize, TotalCount = await query.CountAsync(ct) };
        }
        if (await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ViewOwn, MedicalDocumentPermissionNames.ViewDependent, false, ct) != scope) return Result<object>.Fail(Missing);
        await audit.WriteAsync(scope, "ViewHistoricalDocument", "historical-document", q.DocumentId, null, ct);
        return await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ViewOwn, MedicalDocumentPermissionNames.ViewDependent, false, ct) == scope ? Result<object>.Ok(result) : Result<object>.Fail(Missing);
    }
    public async Task<Result<PrivateMedia>> DownloadAsync(HistoricalDocumentMediaQuery q, CancellationToken ct)
    {
        var scope = await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ViewOwn, MedicalDocumentPermissionNames.ViewDependent, false, ct);
        if (scope is null) return Result<PrivateMedia>.Fail(Missing);
        var item = await (from d in db.Set<PatientHistoricalDocument>().AsNoTracking() join v in db.Set<PatientHistoricalDocumentVersion>() on d.Id equals v.DocumentId
            join a in db.Set<PatientHistoricalDocumentAttachment>() on v.Id equals a.VersionId
            where d.Id == q.DocumentId && d.SubjectPatientId == scope.PatientId && d.State == HistoricalDocumentState.AvailableUnverified && a.Id == q.AttachmentId &&
                (!q.ChildPatientId.HasValue || v.VersionNumber == d.CurrentVersionNumber) select a).SingleOrDefaultAsync(ct);
        if (item is null) return Result<PrivateMedia>.Fail(Missing);
        var opened = await reader.OpenAsync(item.MediaKey, ct); if (opened is null) return Result<PrivateMedia>.Fail(Missing);
        try
        {
            if (!opened.Content.CanSeek || opened.Content.Length != item.Length) { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(Missing); }
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(opened.Content, ct)); opened.Content.Position = 0;
            if (hash != item.Sha256) { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(Missing); }
            await audit.WriteAsync(scope, "DownloadHistoricalDocument", "historical-document", q.DocumentId, null, ct);
            if (await ScopeAsync(q.ChildPatientId, MedicalDocumentPermissionNames.ViewOwn, MedicalDocumentPermissionNames.ViewDependent, false, ct) != scope ||
                !await db.Set<PatientHistoricalDocument>().AnyAsync(d => d.Id == q.DocumentId && d.State == HistoricalDocumentState.AvailableUnverified, ct))
            { await opened.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(Missing); }
            return Result<PrivateMedia>.Ok(new(opened.Content, item.ContentType, item.FileName));
        }
        catch { await opened.Content.DisposeAsync(); throw; }
    }
}
