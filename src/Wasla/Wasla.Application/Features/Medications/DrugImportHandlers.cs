using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using BuildingBlock.Application.Time;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Medications;

internal sealed class PreviewDrugImportHandler(MedicationAccess access, IDrugCatalogImportService importer,
    IMedicationReadService reader, IDateTimeProvider clock) : ICommandHandler<PreviewDrugImportCommand, DrugImportBatchResponse>
{
    public async Task<Result<DrugImportBatchResponse>> Handle(PreviewDrugImportCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogImport, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<DrugImportBatchResponse>.Fail(actor.Errors);
        if (r.SourceVersion?.Length > 100 || r.SourceCommitSha?.Length > 100) return Result<DrugImportBatchResponse>.Fail(MedicationErrors.Validation("DrugCatalogImport.InvalidSchema"));
        var batch = await importer.StageAsync(r.File, r.SourceVersion, r.SourceCommitSha, actor.Value.UserId, clock.UtcNow, ct);
        return batch.IsFailure ? Result<DrugImportBatchResponse>.Fail(batch.Errors) : Result<DrugImportBatchResponse>.Ok((await reader.BatchesAsync(batch.Value.Id, 1, 1, ct)).Items.Single());
    }
}
internal sealed class ApplyDrugImportHandler(MedicationAccess access, IDrugCatalogImportService importer,
    IMedicationReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<ApplyDrugImportCommand, DrugImportBatchResponse>
{
    public async Task<Result<DrugImportBatchResponse>> Handle(ApplyDrugImportCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogImport, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<DrugImportBatchResponse>.Fail(actor.Errors);
        await idem.LockResourceAsync("ImportApply", Guid.Empty, ct);
        return await idem.ExecuteAsync(actor.Value.UserId, "ApplyDrugImport", r.IdempotencyKey, r, async () =>
        {
            var applied = await importer.ApplyAsync(r.BatchId, actor.Value.UserId, clock.UtcNow, ct);
            return applied.IsFailure ? Result<DrugImportBatchResponse>.Fail(applied.Errors) : Result<DrugImportBatchResponse>.Ok((await reader.BatchesAsync(r.BatchId, 1, 1, ct)).Items.Single());
        }, ct);
    }
}
internal sealed class ListDrugImportsHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<ListDrugImportsQuery, ClinicalPage<DrugImportBatchResponse>>
{
    public async Task<Result<ClinicalPage<DrugImportBatchResponse>>> Handle(ListDrugImportsQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogImportHistory, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<DrugImportBatchResponse>>.Fail(actor.Errors);
        var page = await reader.BatchesAsync(r.BatchId, Math.Clamp(r.PageNumber, 1, 1000000), Math.Clamp(r.PageSize, 1, 100), ct);
        return r.BatchId.HasValue && page.Items.Count == 0 ? Result<ClinicalPage<DrugImportBatchResponse>>.Fail(MedicationErrors.NotFound("DrugCatalogImport.BatchNotFound")) : Result<ClinicalPage<DrugImportBatchResponse>>.Ok(page);
    }
}
internal sealed class ListDrugImportChangesHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<ListDrugImportChangesQuery, ClinicalPage<DrugImportRecordResponse>>
{
    public async Task<Result<ClinicalPage<DrugImportRecordResponse>>> Handle(ListDrugImportChangesQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogImportHistory, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<DrugImportRecordResponse>>.Fail(actor.Errors);
        if ((await reader.BatchesAsync(r.BatchId, 1, 1, ct)).Items.Count == 0) return Result<ClinicalPage<DrugImportRecordResponse>>.Fail(MedicationErrors.NotFound("DrugCatalogImport.BatchNotFound"));
        return Result<ClinicalPage<DrugImportRecordResponse>>.Ok(await reader.ImportChangesAsync(r.BatchId, r.ChangeType, Math.Clamp(r.PageNumber, 1, 1000000), Math.Clamp(r.PageSize, 1, 100), ct));
    }
}
