using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Medications;

internal sealed class MutatePrescriptionHandler(MedicationAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IMedicationReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<MutatePrescriptionCommand, PrescriptionStateResponse>
{
    public async Task<Result<PrescriptionStateResponse>> Handle(MutatePrescriptionCommand r, CancellationToken ct)
    {
        var permission = r.Mutation switch { PrescriptionMutation.StartCorrection or PrescriptionMutation.FinalizeCorrection or PrescriptionMutation.DiscardCorrection => PermissionNames.PrescriptionsCorrectOwn,
            PrescriptionMutation.Void => PermissionNames.PrescriptionsVoidOwn, _ => PermissionNames.PrescriptionsManageOwnDraft };
        var actor = await access.ActorAsync(permission, UserType.Doctor, ct);
        if (actor.IsFailure) return Result<PrescriptionStateResponse>.Fail(actor.Errors);
        if (!actor.Value.Permissions.Contains(PermissionNames.PrescriptionsViewOwn) ||
            r.NewMedication is not null && !actor.Value.Permissions.Contains(PermissionNames.DrugCatalogRequestsCreateOwn) ||
            r.Correction && !actor.Value.Permissions.Contains(PermissionNames.PrescriptionsCorrectOwn))
            return Result<PrescriptionStateResponse>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        await idem.LockResourceAsync(r.PrescriptionId.HasValue ? "Prescription" : "EncounterPrescription", r.PrescriptionId ?? r.EncounterId.GetValueOrDefault(), ct);
        var sensitive = r.Mutation is PrescriptionMutation.AddItem or PrescriptionMutation.StartCorrection or PrescriptionMutation.FinalizeCorrection or PrescriptionMutation.Void;
        return sensitive ? await idem.ExecuteAsync(actor.Value.UserId, "Prescription:" + r.Mutation, r.IdempotencyKey, r,
            () => ExecuteAsync(r, actor.Value, ct), ct, async original =>
            {
                var encounter = await work.WriteRepository<MedicalEncounter>().GetByIdAsync(original.MedicalEncounterId, ct);
                if (encounter is null || encounter.DoctorId != actor.Value.DoctorId || !await access.OwnPracticeAsync(actor.Value.DoctorId!.Value, encounter.DoctorPracticeId, ct))
                    return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
                var latest = await reader.PrescriptionAsync(null, encounter.Id, actor.Value.DoctorId.Value, ct);
                return await StateAsync(latest?.PrescriptionId ?? Guid.Empty, encounter, actor.Value, ct);
            }) : await ExecuteAsync(r, actor.Value, ct);
    }
    private async Task<Result<PrescriptionStateResponse>> ExecuteAsync(MutatePrescriptionCommand r, MedicationActor actor, CancellationToken ct)
    {
        var repo = work.WriteRepository<Prescription>();
        var prescription = await repo.FirstOrDefaultAsync(new PrescriptionForUpdate(r.PrescriptionId ?? r.EncounterId.GetValueOrDefault(), !r.PrescriptionId.HasValue), ct);
        var encounter = await work.WriteRepository<MedicalEncounter>().GetByIdAsync(prescription?.MedicalEncounterId ?? r.EncounterId.GetValueOrDefault(), ct);
        if (encounter is null || encounter.DoctorId != actor.DoctorId || r.PracticeId.HasValue && encounter.DoctorPracticeId != r.PracticeId)
            return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        if (!await access.OwnPracticeAsync(actor.DoctorId!.Value, encounter.DoctorPracticeId, ct))
            return Result<PrescriptionStateResponse>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        if (r.PrescriptionId.HasValue && prescription is null) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        if (r.Mutation == PrescriptionMutation.StartCorrection && prescription?.Draft?.PreviousVersionId is not null)
            return await StateAsync(prescription.Id, encounter, actor, ct);
        if (prescription is not null && !TicketRowVersion.Matches(prescription.RowVersion, r.RowVersion ?? string.Empty) ||
            prescription is null && !string.IsNullOrWhiteSpace(r.RowVersion))
            return Result<PrescriptionStateResponse>.Fail(MedicationErrors.Conflict("Prescription.ConcurrencyConflict"));
        var itemOperation = r.Mutation is PrescriptionMutation.AddItem or PrescriptionMutation.UpdateItem or PrescriptionMutation.RemoveItem;
        if (itemOperation && (r.Correction ? encounter.Status != EncounterStatus.Completed || prescription?.Draft?.PreviousVersionId is null
                : encounter.Status != EncounterStatus.InProgress || prescription?.Draft?.PreviousVersionId is not null))
            return Result<PrescriptionStateResponse>.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        if (r.Mutation == PrescriptionMutation.AddItem && prescription is null)
        {
            var created = Prescription.CreateDraft(encounter, actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<PrescriptionStateResponse>.Fail(created.Errors);
            prescription = created.Value; await repo.AddAsync(prescription, ct);
        }
        if (prescription is null) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        Result changed;
        switch (r.Mutation)
        {
            case PrescriptionMutation.AddItem:
                if (r.DrugCatalogId.HasValue == (r.NewMedication is not null)) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.Validation("PrescriptionItem.MedicationRequired"));
                if (r.DrugCatalogId.HasValue)
                {
                    await idem.LockResourceAsync("Catalog", r.DrugCatalogId.Value, ct);
                    var drug = await work.WriteRepository<DrugCatalog>().GetByIdAsync(r.DrugCatalogId.Value, ct);
                    if (drug is null) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("DrugCatalog.NotFound"));
                    changed = prescription.AddCatalogItem(drug, r.Data ?? new(), actor.UserId, clock.UtcNow);
                }
                else
                {
                    var submitted = DrugCatalogRequest.Submit(r.NewMedication!, actor.DoctorId.Value, actor.UserId, clock.UtcNow);
                    if (submitted.IsFailure) return Result<PrescriptionStateResponse>.Fail(submitted.Errors);
                    await work.WriteRepository<DrugCatalogRequest>().AddAsync(submitted.Value, ct);
                    changed = prescription.AddSubmittedItem(submitted.Value, r.Data ?? new(), actor.UserId, clock.UtcNow);
                }
                break;
            case PrescriptionMutation.UpdateItem: changed = prescription.UpdateItem(r.ItemId.GetValueOrDefault(), r.Data ?? new(), actor.UserId, clock.UtcNow); break;
            case PrescriptionMutation.RemoveItem: changed = prescription.RemoveItem(r.ItemId.GetValueOrDefault(), actor.UserId, clock.UtcNow); break;
            case PrescriptionMutation.StartCorrection: changed = prescription.StartCorrection(encounter, r.Reason ?? string.Empty, actor.UserId, clock.UtcNow); break;
            case PrescriptionMutation.FinalizeCorrection: changed = prescription.FinalizeCorrection(actor.UserId, clock.UtcNow); break;
            case PrescriptionMutation.DiscardCorrection: changed = prescription.DiscardCorrection(actor.UserId, clock.UtcNow); break;
            case PrescriptionMutation.Void: changed = prescription.Void(r.Reason ?? string.Empty, actor.UserId, clock.UtcNow); break;
            default: changed = Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState")); break;
        }
        if (changed.IsFailure) return Result<PrescriptionStateResponse>.Fail(changed.Errors);
        var id = prescription.Id;
        if (prescription.IsEmptyInitialDraft) { repo.Delete(prescription); id = Guid.Empty; }
        await work.SaveChangesAsync(ct);
        return await StateAsync(id, encounter, actor, ct);
    }
    private async Task<Result<PrescriptionStateResponse>> StateAsync(Guid id, MedicalEncounter encounter, MedicationActor actor, CancellationToken ct)
    {
        var state = id == Guid.Empty ? new PrescriptionStateResponse(null, encounter.Id, encounter.DoctorPracticeId, null, null, null,
            new(false, false, false, false, false, false), []) : (await reader.PrescriptionAsync(id, null, actor.DoctorId!.Value, ct))!;
        return Result<PrescriptionStateResponse>.Ok(PrescriptionCapabilities.For(state, actor, encounter.Status == EncounterStatus.InProgress));
    }
}
internal sealed class GetDoctorPrescriptionHandler(MedicationAccess access, IMedicationReadService reader,
    IUnitOfWork<WaslaWritePersistence> work) : IQueryHandler<GetDoctorPrescriptionQuery, PrescriptionStateResponse>
{
    public async Task<Result<PrescriptionStateResponse>> Handle(GetDoctorPrescriptionQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.PrescriptionsViewOwn, UserType.Doctor, ct);
        if (actor.IsFailure) return Result<PrescriptionStateResponse>.Fail(actor.Errors);
        var state = await reader.PrescriptionAsync(r.PrescriptionId, null, actor.Value.DoctorId!.Value, ct);
        if (state is null || r.CorrectionOnly && state.Draft?.PreviousVersionId is null) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        if (!await access.OwnPracticeAsync(actor.Value.DoctorId.Value, state.PracticeId, ct)) return Result<PrescriptionStateResponse>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        var encounter = await work.WriteRepository<MedicalEncounter>().GetByIdAsync(state.MedicalEncounterId, ct);
        return Result<PrescriptionStateResponse>.Ok(PrescriptionCapabilities.For(state, actor.Value, encounter!.Status == EncounterStatus.InProgress));
    }
}
internal sealed class ListPrescriptionVersionsHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<ListPrescriptionVersionsQuery, IReadOnlyList<PrescriptionVersionResponse>>
{
    public async Task<Result<IReadOnlyList<PrescriptionVersionResponse>>> Handle(ListPrescriptionVersionsQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.PrescriptionsViewOwn, UserType.Doctor, ct);
        if (actor.IsFailure) return Result<IReadOnlyList<PrescriptionVersionResponse>>.Fail(actor.Errors);
        var state = await reader.PrescriptionAsync(r.PrescriptionId, null, actor.Value.DoctorId!.Value, ct);
        if (state is null) return Result<IReadOnlyList<PrescriptionVersionResponse>>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        if (!await access.OwnPracticeAsync(actor.Value.DoctorId.Value, state.PracticeId, ct)) return Result<IReadOnlyList<PrescriptionVersionResponse>>.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        var versions = await reader.VersionsAsync(r.PrescriptionId, actor.Value.DoctorId.Value, r.VersionNumber, ct);
        return versions.Count == 0 ? Result<IReadOnlyList<PrescriptionVersionResponse>>.Fail(MedicationErrors.NotFound("Prescription.NotFound")) : Result<IReadOnlyList<PrescriptionVersionResponse>>.Ok(versions);
    }
}
internal sealed class ListMyPrescriptionsHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<ListMyPrescriptionsQuery, ClinicalPage<PatientPrescriptionSummaryResponse>>
{
    public async Task<Result<ClinicalPage<PatientPrescriptionSummaryResponse>>> Handle(ListMyPrescriptionsQuery r, CancellationToken ct)
    {
        var patient = await access.OwnPatientAsync(ct);
        return patient.IsFailure ? Result<ClinicalPage<PatientPrescriptionSummaryResponse>>.Fail(patient.Errors) :
            Result<ClinicalPage<PatientPrescriptionSummaryResponse>>.Ok(await reader.PatientListAsync(patient.Value, Math.Clamp(r.PageNumber, 1, 1000000), Math.Clamp(r.PageSize, 1, 100), ct));
    }
}
internal sealed class GetMyPrescriptionHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<GetMyPrescriptionQuery, PatientPrescriptionResponse>
{
    public async Task<Result<PatientPrescriptionResponse>> Handle(GetMyPrescriptionQuery r, CancellationToken ct)
    {
        var patient = await access.OwnPatientAsync(ct);
        if (patient.IsFailure) return Result<PatientPrescriptionResponse>.Fail(patient.Errors);
        var prescription = await reader.PatientPrescriptionAsync(patient.Value, r.PrescriptionId, ct);
        return prescription is null ? Result<PatientPrescriptionResponse>.Fail(MedicationErrors.NotFound("Prescription.NotFound")) : Result<PatientPrescriptionResponse>.Ok(prescription);
    }
}
