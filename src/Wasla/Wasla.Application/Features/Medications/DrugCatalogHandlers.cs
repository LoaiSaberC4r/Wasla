using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Medications;

internal sealed class SearchDrugCatalogHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<SearchDrugCatalogQuery, ClinicalPage<DrugCatalogResponse>>
{
    public async Task<Result<ClinicalPage<DrugCatalogResponse>>> Handle(SearchDrugCatalogQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.DoctorSearch ? PermissionNames.DrugCatalogSearchActive : PermissionNames.DrugCatalogView,
            r.DoctorSearch ? UserType.Doctor : UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<DrugCatalogResponse>>.Fail(actor.Errors);
        if (r.Search?.Length > 500 || r.Status is { } status && !Enum.IsDefined(status)) return Result<ClinicalPage<DrugCatalogResponse>>.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
        return Result<ClinicalPage<DrugCatalogResponse>>.Ok(await reader.CatalogAsync(r.Search,
            r.DoctorSearch ? DrugCatalogStatus.Active : r.Status, r.DoctorSearch, Math.Clamp(r.PageNumber, 1, 1000000), Math.Clamp(r.PageSize, 1, 100), ct));
    }
}
internal sealed class GetDrugCatalogHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<GetDrugCatalogQuery, DrugCatalogDetailsResponse>
{
    public async Task<Result<DrugCatalogDetailsResponse>> Handle(GetDrugCatalogQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogView, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<DrugCatalogDetailsResponse>.Fail(actor.Errors);
        var drug = await reader.DrugAsync(r.DrugId, ct);
        return drug is null ? Result<DrugCatalogDetailsResponse>.Fail(MedicationErrors.NotFound("DrugCatalog.NotFound")) : Result<DrugCatalogDetailsResponse>.Ok(drug);
    }
}
internal sealed class ManageDrugCatalogHandler(MedicationAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IMedicationReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<ManageDrugCatalogCommand, DrugCatalogDetailsResponse>
{
    public async Task<Result<DrugCatalogDetailsResponse>> Handle(ManageDrugCatalogCommand r, CancellationToken ct)
    {
        var permission = r.Mutation switch { DrugCatalogMutation.Create => PermissionNames.DrugCatalogCreate,
            DrugCatalogMutation.Update => PermissionNames.DrugCatalogUpdate, DrugCatalogMutation.Activate => PermissionNames.DrugCatalogActivate,
            DrugCatalogMutation.Deactivate => PermissionNames.DrugCatalogDeactivate, _ => PermissionNames.DrugCatalogMerge };
        var actor = await access.ActorAsync(permission, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<DrugCatalogDetailsResponse>.Fail(actor.Errors);
        var resourceIds = new[] { r.DrugId, r.Mutation == DrugCatalogMutation.Merge ? r.TargetId : null }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().Order().ToArray();
        foreach (var id in resourceIds) await idem.LockResourceAsync("Catalog", id, ct);
        return r.Mutation == DrugCatalogMutation.Merge
            ? await idem.ExecuteAsync(actor.Value.UserId, "MergeDrug", r.IdempotencyKey, r, () => ExecuteAsync(r, actor.Value.UserId, ct), ct)
            : await ExecuteAsync(r, actor.Value.UserId, ct);
    }
    private async Task<Result<DrugCatalogDetailsResponse>> ExecuteAsync(ManageDrugCatalogCommand r, Guid actor, CancellationToken ct)
    {
        var repository = work.WriteRepository<DrugCatalog>(); DrugCatalog drug;
        if (r.Mutation == DrugCatalogMutation.Create)
        {
            if (r.Data is null) return Result<DrugCatalogDetailsResponse>.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
            var created = DrugCatalog.Create(r.Data, DrugOriginType.Manual, actor, clock.UtcNow);
            if (created.IsFailure) return Result<DrugCatalogDetailsResponse>.Fail(created.Errors);
            drug = created.Value; await repository.AddAsync(drug, ct);
        }
        else
        {
            drug = (await repository.FirstOrDefaultAsync(new DrugForUpdate(r.DrugId.GetValueOrDefault()), ct))!;
            if (drug is null) return Result<DrugCatalogDetailsResponse>.Fail(MedicationErrors.NotFound("DrugCatalog.NotFound"));
            if (!TicketRowVersion.Matches(drug.RowVersion, r.RowVersion ?? string.Empty)) return Result<DrugCatalogDetailsResponse>.Fail(MedicationErrors.Conflict("DrugCatalog.ConcurrencyConflict"));
            Result changed;
            switch (r.Mutation)
            {
                case DrugCatalogMutation.Update:
                    changed = r.Data is null ? Result.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData")) : drug.Update(r.Data, r.Reason, actor, clock.UtcNow); break;
                case DrugCatalogMutation.Activate: changed = drug.SetActive(true, r.Reason ?? string.Empty, actor, clock.UtcNow); break;
                case DrugCatalogMutation.Deactivate: changed = drug.SetActive(false, r.Reason ?? string.Empty, actor, clock.UtcNow); break;
                case DrugCatalogMutation.Merge:
                    var target = await repository.GetByIdAsync(r.TargetId.GetValueOrDefault(), ct);
                    changed = target is null ? Result.Fail(MedicationErrors.Conflict("DrugCatalog.MergeTargetInvalid")) : drug.MergeInto(target, r.Reason ?? string.Empty, actor, clock.UtcNow); break;
                default: changed = Result.Fail(MedicationErrors.Conflict("DrugCatalog.InvalidState")); break;
            }
            if (changed.IsFailure) return Result<DrugCatalogDetailsResponse>.Fail(changed.Errors);
        }
        await work.SaveChangesAsync(ct);
        return Result<DrugCatalogDetailsResponse>.Ok((await reader.DrugAsync(drug.Id, ct))!);
    }
}
internal sealed class ListDrugRequestsHandler(MedicationAccess access, IMedicationReadService reader)
    : IQueryHandler<ListDrugRequestsQuery, ClinicalPage<DrugRequestResponse>>
{
    public async Task<Result<ClinicalPage<DrugRequestResponse>>> Handle(ListDrugRequestsQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Own ? PermissionNames.DrugCatalogRequestsViewOwn : PermissionNames.DrugCatalogRequestsView,
            r.Own ? UserType.Doctor : UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<DrugRequestResponse>>.Fail(actor.Errors);
        var result = await reader.RequestsAsync(r.Own ? actor.Value.DoctorId : r.DoctorId, r.Status, r.Search, r.RequestId,
            Math.Clamp(r.PageNumber, 1, 1000000), Math.Clamp(r.PageSize, 1, 100), ct);
        return r.RequestId.HasValue && result.Items.Count == 0 ? Result<ClinicalPage<DrugRequestResponse>>.Fail(MedicationErrors.NotFound("DrugCatalogRequest.NotFound"))
            : Result<ClinicalPage<DrugRequestResponse>>.Ok(result);
    }
}
internal sealed class SaveDrugRequestHandler(MedicationAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IMedicationReadService reader, IDateTimeProvider clock)
    : ICommandHandler<SaveDrugRequestCommand, DrugRequestResponse>
{
    public async Task<Result<DrugRequestResponse>> Handle(SaveDrugRequestCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.RequestId.HasValue ? PermissionNames.DrugCatalogRequestsUpdateOwn : PermissionNames.DrugCatalogRequestsCreateOwn, UserType.Doctor, ct);
        if (actor.IsFailure) return Result<DrugRequestResponse>.Fail(actor.Errors);
        var repository = work.WriteRepository<DrugCatalogRequest>(); DrugCatalogRequest request;
        if (r.RequestId.HasValue)
        {
            request = (await repository.FirstOrDefaultAsync(new DrugRequestForUpdate(r.RequestId.Value), ct))!;
            if (request is null || request.RequestedByDoctorId != actor.Value.DoctorId) return Result<DrugRequestResponse>.Fail(MedicationErrors.NotFound("DrugCatalogRequest.NotFound"));
            if (!TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? string.Empty)) return Result<DrugRequestResponse>.Fail(MedicationErrors.Conflict("DrugCatalogRequest.ConcurrencyConflict"));
            var updated = request.Update(r.Data, actor.Value.UserId, clock.UtcNow); if (updated.IsFailure) return Result<DrugRequestResponse>.Fail(updated.Errors);
        }
        else
        {
            var created = DrugCatalogRequest.Submit(r.Data, actor.Value.DoctorId!.Value, actor.Value.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<DrugRequestResponse>.Fail(created.Errors);
            request = created.Value; await repository.AddAsync(request, ct);
        }
        await work.SaveChangesAsync(ct);
        return Result<DrugRequestResponse>.Ok((await reader.RequestsAsync(actor.Value.DoctorId, null, null, request.Id, 1, 1, ct)).Items.Single());
    }
}
internal sealed class ReviewDrugRequestHandler(MedicationAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IMedicationReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<ReviewDrugRequestCommand, DrugRequestResponse>
{
    public async Task<Result<DrugRequestResponse>> Handle(ReviewDrugRequestCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(PermissionNames.DrugCatalogRequestsReview, UserType.DrugCatalogManager, ct);
        if (actor.IsFailure) return Result<DrugRequestResponse>.Fail(actor.Errors);
        await idem.LockResourceAsync("DrugRequest", r.RequestId, ct);
        return r.Status == DrugCatalogRequestStatus.Approved
            ? await idem.ExecuteAsync(actor.Value.UserId, "ApproveDrugRequest", r.IdempotencyKey, r, () => ExecuteAsync(r, actor.Value.UserId, ct), ct)
            : await ExecuteAsync(r, actor.Value.UserId, ct);
    }
    private async Task<Result<DrugRequestResponse>> ExecuteAsync(ReviewDrugRequestCommand r, Guid actor, CancellationToken ct)
    {
        var request = await work.WriteRepository<DrugCatalogRequest>().FirstOrDefaultAsync(new DrugRequestForUpdate(r.RequestId), ct);
        if (request is null) return Result<DrugRequestResponse>.Fail(MedicationErrors.NotFound("DrugCatalogRequest.NotFound"));
        if (!TicketRowVersion.Matches(request.RowVersion, r.RowVersion)) return Result<DrugRequestResponse>.Fail(MedicationErrors.Conflict("DrugCatalogRequest.ConcurrencyConflict"));
        Guid? drugId = r.DuplicateOfDrugCatalogId;
        if (drugId.HasValue)
        {
            var duplicate = await work.WriteRepository<DrugCatalog>().GetByIdAsync(drugId.Value, ct);
            if (duplicate?.Status != DrugCatalogStatus.Active || r.Status != DrugCatalogRequestStatus.Rejected) return Result<DrugRequestResponse>.Fail(MedicationErrors.Validation("DrugCatalogRequest.InvalidData"));
        }
        if (r.Status == DrugCatalogRequestStatus.Approved)
        {
            if (r.ApprovedData is null) return Result<DrugRequestResponse>.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
            var created = DrugCatalog.Create(r.ApprovedData, DrugOriginType.DoctorRequest, actor, clock.UtcNow, requestId: request.Id);
            if (created.IsFailure) return Result<DrugRequestResponse>.Fail(created.Errors);
            await work.WriteRepository<DrugCatalog>().AddAsync(created.Value, ct); drugId = created.Value.Id;
        }
        var changed = request.Review(r.Status, r.DuplicateOfDrugCatalogId.HasValue ? "duplicate" : r.Reason, drugId, actor, clock.UtcNow);
        if (changed.IsFailure) return Result<DrugRequestResponse>.Fail(changed.Errors);
        await work.SaveChangesAsync(ct);
        return Result<DrugRequestResponse>.Ok((await reader.RequestsAsync(null, null, null, request.Id, 1, 1, ct)).Items.Single());
    }
}
