using System.Text.Json;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using Wasla.Application.Features.Auth;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Medications;

internal sealed record MedicationActor(Guid UserId, Guid? DoctorId, IReadOnlyList<string> Permissions);
internal sealed class MedicationAccess(IWaslaDataStore store, ICurrentUser current)
{
    public async Task<Result<MedicationActor>> ActorAsync(string permission, UserType type, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } id) return Result<MedicationActor>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        var snapshot = await store.GetAccessSnapshotAsync(id, ct);
        if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false } || snapshot.User.UserType != type ||
            !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
            !LoginCommandHandler.EffectivePermissions(snapshot).Contains(permission, StringComparer.OrdinalIgnoreCase) ||
            type == UserType.Doctor && (snapshot.DoctorId is null || snapshot.DoctorStatus != DoctorApprovalStatus.Approved) ||
            type == UserType.DrugCatalogManager && !snapshot.Roles.Contains(SystemRoleNames.DrugCatalogManager))
            return Result<MedicationActor>.Fail(MedicationErrors.AccessDenied(type == UserType.DrugCatalogManager ? "DrugCatalog.AccessDenied" : "Prescription.AccessDenied"));
        return Result<MedicationActor>.Ok(new(id, snapshot.DoctorId, snapshot.Permissions.Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray()));
    }
    public async Task<bool> OwnPracticeAsync(Guid doctorId, Guid practiceId, CancellationToken ct)
        => await store.FindDoctorPracticeAsync(practiceId, ct) is { IsActive: true } practice && practice.DoctorId == doctorId;
    public async Task<Result<Guid>> OwnPatientAsync(CancellationToken ct)
    {
        var actor = await ActorAsync(PermissionNames.PrescriptionsViewOwnCompleted, UserType.Patient, ct);
        if (actor.IsFailure) return Result<Guid>.Fail(actor.Errors);
        var link = await store.FindPatientAccountLinkAsync(actor.Value.UserId, ct);
        return link is null ? Result<Guid>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied")) : Result<Guid>.Ok(link.PatientId);
    }
}

internal sealed class MedicationIdempotency(IUnitOfWork<WaslaWritePersistence> work, ITicketQueueLock locks, IDateTimeProvider clock)
{
    public Task LockResourceAsync(string kind, Guid id, CancellationToken ct)
        => locks.AcquireIdempotencyAsync(Guid.Empty, "Phase14:" + kind, id.ToString("N"), ct);
    public async Task<Result<T>> ExecuteAsync<T>(Guid actor, string operation, string? key, object payload,
        Func<Task<Result<T>>> action, CancellationToken ct, Func<T, Task<Result<T>>>? replay = null)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 200) return Result<T>.Fail(MedicationErrors.Validation("Medication.IdempotencyKeyRequired"));
        key = key.Trim(); var fingerprint = TicketIdempotency.Fingerprint(JsonSerializer.Serialize(payload));
        await locks.AcquireIdempotencyAsync(actor, "Phase14:" + operation, key, ct);
        var repository = work.WriteRepository<MedicationIdempotencyRecord>();
        var existing = await repository.GetByPropertyAsync(r => r.ActorApplicationUserId == actor && r.Operation == operation && r.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint || existing.ResponseJson is null)
                return Result<T>.Fail(MedicationErrors.Conflict("Medication.IdempotencyKeyReused"));
            var original = JsonSerializer.Deserialize<T>(existing.ResponseJson)!;
            return replay is null ? Result<T>.Ok(original) : await replay(original);
        }
        var record = MedicationIdempotencyRecord.Create(actor, operation, key, fingerprint, clock.UtcNow);
        var result = await action(); if (result.IsFailure) return result;
        record.Complete(JsonSerializer.Serialize(result.Value));
        await repository.AddAsync(record, ct); await work.SaveChangesAsync(ct); return result;
    }
}
internal sealed class PrescriptionForUpdate : Specification<Prescription>
{
    public PrescriptionForUpdate(Guid id, bool byEncounter = false)
    {
        AddCriteria(p => byEncounter ? p.MedicalEncounterId == id : p.Id == id);
        AddInclude(p => p.Versions); UseTracking();
    }
}
internal sealed class DrugForUpdate : Specification<DrugCatalog>
{
    public DrugForUpdate(Guid id) { AddCriteria(d => d.Id == id); UseTracking(); }
}
internal sealed class DrugRequestForUpdate : Specification<DrugCatalogRequest>
{
    public DrugRequestForUpdate(Guid id) { AddCriteria(d => d.Id == id); UseTracking(); }
}

internal static class PrescriptionCapabilities
{
    public static PrescriptionStateResponse For(PrescriptionStateResponse state, MedicationActor actor, bool encounterInProgress)
    {
        bool Has(string p) => actor.Permissions.Contains(p, StringComparer.OrdinalIgnoreCase);
        var draft = state.Draft; var manage = Has(PermissionNames.PrescriptionsManageOwnDraft) &&
            (encounterInProgress || draft?.PreviousVersionId is not null && Has(PermissionNames.PrescriptionsCorrectOwn));
        var finalized = state.Current?.Status == PrescriptionVersionStatus.Finalized;
        return state with { Capabilities = new(manage, manage && Has(PermissionNames.DrugCatalogRequestsCreateOwn),
            !encounterInProgress && finalized && Has(PermissionNames.PrescriptionsCorrectOwn),
            draft?.PreviousVersionId is not null && Has(PermissionNames.PrescriptionsCorrectOwn) && state.CompletionBlockers.Count == 0,
            draft?.PreviousVersionId is not null && Has(PermissionNames.PrescriptionsCorrectOwn),
            finalized && draft is null && Has(PermissionNames.PrescriptionsVoidOwn)) };
    }
}
