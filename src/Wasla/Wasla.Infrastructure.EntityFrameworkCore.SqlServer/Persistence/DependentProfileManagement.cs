using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Families;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DependentProfileManagement(WaslaDbContext db, IDependentAccessPolicy access, IPatientIdentityStore identities,
    IPrivateMediaReader reader, IMediaService media, IDateTimeProvider clock) : IDependentProfileManagement
{
    public async Task<bool> CanManageAsync(Guid child, string permission, CancellationToken ct)
        => permission is FamilyExtensionPermissionNames.ManageContacts or FamilyExtensionPermissionNames.ManagePhoto &&
            (await access.AuthorizeAsync(child, permission, ct)).IsSuccess && !await db.PatientAccountLinks.AnyAsync(l => l.PatientId == child, ct);
    public async Task<Result<PrivateMedia>> ImageAsync(Guid child, CancellationToken ct)
    {
        if ((await access.AuthorizeAsync(child, FamilySelfServicePermissionNames.Profile, ct)).IsFailure) return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable);
        var key = await db.Patients.Where(p => p.Id == child).Select(p => p.ProfileImageMediaKey).SingleOrDefaultAsync(ct);
        var file = key is null ? null : await reader.OpenAsync(key, ct);
        if (file is null) return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable);
        if ((await access.AuthorizeAsync(child, FamilySelfServicePermissionNames.Profile, ct)).IsFailure)
        { await file.Content.DisposeAsync(); return Result<PrivateMedia>.Fail(DependentAccessErrors.Unavailable); }
        return Result<PrivateMedia>.Ok(file);
    }
    public async Task<Result<DependentProfileChangeResponse>> ExecuteAsync(ManageDependentProfileCommand c, CancellationToken ct)
    {
        var permission = c.Operation == "photo" ? FamilyExtensionPermissionNames.ManagePhoto : FamilyExtensionPermissionNames.ManageContacts;
        var scope = await access.AuthorizeAsync(c.ChildPatientId, permission, ct);
        if (scope.IsFailure) return Result<DependentProfileChangeResponse>.Fail(scope.Errors);
        if (c.Operation is not ("photo" or "add" or "update" or "delete") || string.IsNullOrWhiteSpace(c.IdempotencyKey) || c.IdempotencyKey.Length > 100)
            return Result<DependentProfileChangeResponse>.Fail(PatientErrors.Invalid);
        if (c.Contact?.LinkedPatientId is not null) return Result<DependentProfileChangeResponse>.Fail(PatientErrors.InvalidContact);
        string? imageHash = null;
        if (c.Operation == "photo")
        {
            var photo = c.Photo;
            if (photo is null || !photo.Content.CanSeek || photo.Content.Length is <= 0 or > 10485760 ||
                Path.GetExtension(photo.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png")) return Result<DependentProfileChangeResponse>.Fail(PatientErrors.Invalid);
            photo.Content.Position = 0; imageHash = Convert.ToHexString(await SHA256.HashDataAsync(photo.Content, ct)); photo.Content.Position = 0;
            if (!await Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media.HistoricalDocumentSafety.IsPassiveAsync(photo.Content, Path.GetExtension(photo.FileName), ct)) return Result<DependentProfileChangeResponse>.Fail(PatientErrors.Invalid);
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { c.ChildPatientId, c.Operation, c.RowVersion, c.ContactId, c.Contact, imageHash }))));
        await identities.LockAsync(ct);
        var replay = await db.Set<DependentProfileChange>().SingleOrDefaultAsync(r => r.ActorUserId == scope.Value.ActorUserId && r.IdempotencyKey == c.IdempotencyKey && r.Operation == c.Operation, ct);
        if (replay is not null) return replay.ChildPatientId == c.ChildPatientId && replay.Fingerprint == fingerprint ?
            Result<DependentProfileChangeResponse>.Ok(new(replay.Id, replay.ContactId)) : Result<DependentProfileChangeResponse>.Fail(FamilyErrors.ConcurrencyConflict);
        var patient = await db.Patients.SingleAsync(p => p.Id == c.ChildPatientId, ct);
        if (string.IsNullOrWhiteSpace(c.RowVersion) || c.RowVersion.Length > 40) return Result<DependentProfileChangeResponse>.Fail(FamilyErrors.ConcurrencyConflict);
        byte[] version;
        try { version = Convert.FromBase64String(c.RowVersion); } catch (FormatException) { return Result<DependentProfileChangeResponse>.Fail(FamilyErrors.ConcurrencyConflict); }
        if (!version.AsSpan().SequenceEqual(patient.RowVersion)) return Result<DependentProfileChangeResponse>.Fail(FamilyErrors.ConcurrencyConflict);
        // A linked teenager controls their own operational profile. Never alter their account-bound data through a proxy.
        if (await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patient.Id, ct)) return Result<DependentProfileChangeResponse>.Fail(DependentAccessErrors.Unavailable);
        Guid? changedContact = null;
        if (c.Operation == "photo")
        {
            var saved = await media.SaveAsync(c.Photo!, new($"Patients/{patient.Id:N}/Profile"), ct);
            var updated = patient.UpdateProfile(patient.NameAr, patient.NameEn, patient.PhoneNumber, patient.Email, saved.Key, true);
            if (updated.IsFailure) return Result<DependentProfileChangeResponse>.Fail(updated.Errors);
        }
        else
        {
            var contacts = await db.PatientContacts.Where(p => p.PatientId == patient.Id && !p.IsDeleted).ToArrayAsync(ct);
            var existing = contacts.SingleOrDefault(p => p.Id == c.ContactId);
            if (c.Operation != "add" && existing is null) return Result<DependentProfileChangeResponse>.Fail(PatientErrors.ContactNotFound);
            if (c.Operation is "delete" or "update" && existing!.IsPrimary && (c.Operation == "delete" || c.Contact?.IsPrimary != true) &&
                string.IsNullOrWhiteSpace(patient.PhoneNumber) && !contacts.Any(p => p.Id != existing.Id && p.IsPrimary && !string.IsNullOrWhiteSpace(p.PhoneNumber)))
                return Result<DependentProfileChangeResponse>.Fail(PatientErrors.LastContactRequired);
            if (c.Operation == "delete") { existing!.IsDeleted = true; existing.DeletedOnUtc = clock.UtcNow; changedContact = existing.Id; }
            else
            {
                if (c.Contact is not { } i) return Result<DependentProfileChangeResponse>.Fail(PatientErrors.InvalidContact);
                if (c.Operation == "add")
                {
                    var made = PatientContact.Create(Guid.NewGuid(), patient.Id, i.NameAr, i.NameEn, i.PhoneNumber, i.RelationshipType, null, i.IsPrimary, i.ContactEmail);
                    if (made.IsFailure) return Result<DependentProfileChangeResponse>.Fail(made.Errors);
                    db.Add(made.Value); changedContact = made.Value.Id;
                }
                else
                {
                    var updated = existing!.Update(i.NameAr, i.NameEn, i.PhoneNumber, i.RelationshipType, null, i.IsPrimary, i.ContactEmail);
                    if (updated.IsFailure) return Result<DependentProfileChangeResponse>.Fail(updated.Errors); changedContact = existing.Id;
                }
            }
        }
        if ((await access.AuthorizeAsync(c.ChildPatientId, permission, ct)).IsFailure) return Result<DependentProfileChangeResponse>.Fail(DependentAccessErrors.Unavailable);
        patient.ModifiedOnUtc = clock.UtcNow; db.Entry(patient).Property(p => p.ModifiedOnUtc).IsModified = true;
        db.Entry(patient).Property(p => p.RowVersion).OriginalValue = version;
        var change = new DependentProfileChange(scope.Value.ActorUserId, scope.Value.AdultPatientId, patient.Id, c.Operation, c.IdempotencyKey, fingerprint, changedContact, clock.UtcNow);
        db.Add(change); await db.SaveChangesAsync(ct);
        return Result<DependentProfileChangeResponse>.Ok(new(change.Id, changedContact));
    }
}
