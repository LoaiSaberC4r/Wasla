
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;

namespace Wasla.Application.Features.Diagnostics;

internal sealed class MedicalCatalogHandlers(DiagnosticAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IDiagnosticReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : IQueryHandler<SearchMedicalCatalogQuery, ClinicalPage<MedicalCatalogResponse>>,
      ICommandHandler<MutateMedicalCatalogCommand, MedicalCatalogResponse>,
      IQueryHandler<ListMedicalCatalogRequestsQuery, ClinicalPage<MedicalCatalogRequestResponse>>,
      ICommandHandler<MutateMedicalCatalogRequestCommand, MedicalCatalogRequestResponse>
{
    public async Task<Result<ClinicalPage<MedicalCatalogResponse>>> Handle(SearchMedicalCatalogQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "Catalog." + (r.DoctorSearch ? "SearchActive" : "View"), r.DoctorSearch ? UserType.Doctor : UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<MedicalCatalogResponse>>.Fail(actor.Errors);
        var page = await reader.CatalogAsync(r, actor.Value.Permissions, ct);
        return r.Id.HasValue && page.Items.Count == 0 ? Result<ClinicalPage<MedicalCatalogResponse>>.Fail(DiagnosticErrors.NotFound(r.Kind + "Catalog.NotFound")) : Result<ClinicalPage<MedicalCatalogResponse>>.Ok(page);
    }
    public async Task<Result<MedicalCatalogResponse>> Handle(MutateMedicalCatalogCommand r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "Catalog." + r.Mutation, UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<MedicalCatalogResponse>.Fail(actor.Errors);
        await idem.LockResourceAsync("Phase15:" + r.Kind + "CatalogApply", Guid.Empty, ct);
        Task<Result<MedicalCatalogResponse>> Run() => r.Kind == DiagnosticKind.Lab ? MutateLabCatalogAsync(r, actor.Value, ct) : MutateRadiologyCatalogAsync(r, actor.Value, ct);
        return r.Mutation == CatalogMutation.Merge ? await idem.ExecuteAsync(actor.Value.UserId, "Phase15:" + r.Kind + "CatalogMerge", r.IdempotencyKey, r, Run, ct) : await Run();
    }
    public async Task<Result<ClinicalPage<MedicalCatalogRequestResponse>>> Handle(ListMedicalCatalogRequestsQuery r, CancellationToken ct)
    {
        var actor = await access.ActorAsync(r.Kind + "CatalogRequests." + (r.Own ? "ViewOwn" : "View"), r.Own ? UserType.Doctor : UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<ClinicalPage<MedicalCatalogRequestResponse>>.Fail(actor.Errors);
        var page = await reader.CatalogRequestsAsync(r, r.Own ? actor.Value.DoctorId : null, actor.Value.Permissions, ct);
        return r.Id.HasValue && page.Items.Count == 0 ? Result<ClinicalPage<MedicalCatalogRequestResponse>>.Fail(DiagnosticErrors.NotFound(r.Kind + "CatalogRequest.NotFound")) : Result<ClinicalPage<MedicalCatalogRequestResponse>>.Ok(page);
    }
    public async Task<Result<MedicalCatalogRequestResponse>> Handle(MutateMedicalCatalogRequestCommand r, CancellationToken ct)
    {
        var own = r.Mutation is CatalogRequestMutation.Create or CatalogRequestMutation.Update;
        var permission = r.Kind + "CatalogRequests." + (own ? r.Mutation == CatalogRequestMutation.Create ? "CreateOwn" : "UpdateOwn" : "Review");
        var actor = await access.ActorAsync(permission, own ? UserType.Doctor : UserType.MedicalCatalogManager, ct);
        if (actor.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(actor.Errors);
        await idem.LockResourceAsync("Phase15:" + r.Kind + "CatalogApply", Guid.Empty, ct);
        Task<Result<MedicalCatalogRequestResponse>> Run() => r.Kind == DiagnosticKind.Lab ? ReviewLabAsync(r, actor.Value, own, ct) : ReviewRadiologyAsync(r, actor.Value, own, ct);
        return r.Mutation == CatalogRequestMutation.Approve ? await idem.ExecuteAsync(actor.Value.UserId, "Phase15:" + r.Kind + "CatalogApprove", r.IdempotencyKey, r, Run, ct) : await Run();
    }

    private async Task<Result<MedicalCatalogResponse>> MutateLabCatalogAsync(MutateMedicalCatalogCommand r, DiagnosticActor actor, CancellationToken ct)
    {
        var repo = work.WriteRepository<LabTestCatalog>();
        LabTestCatalog? catalog;
        if (r.Mutation == CatalogMutation.Create)
        {
            var created = LabTestCatalog.CreateWasla(r.Data ?? new(), actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<MedicalCatalogResponse>.Fail(created.Errors);
            catalog = created.Value;
            if (await repo.GetByPropertyAsync(c => c.NormalizedName == catalog.NormalizedName, ct) is not null)
                return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("LabCatalog.PossibleDuplicate"));
            await repo.AddAsync(catalog, ct);
        }
        else
        {
            catalog = await repo.GetByIdAsync(r.Id.GetValueOrDefault(), ct);
            if (catalog is null) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.NotFound("LabCatalog.NotFound"));
            if (!TicketRowVersion.Matches(catalog.RowVersion, r.RowVersion ?? "")) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("LabCatalog.ConcurrencyConflict"));
            Result change;
            if (r.Mutation == CatalogMutation.Merge)
            {
                var target = await repo.GetByIdAsync(r.TargetId.GetValueOrDefault(), ct);
                if (target is null) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("LabCatalog.InvalidMergeTarget"));
                change = catalog.MergeInto(target, r.Reason, actor.UserId, clock.UtcNow);
                if (change.IsSuccess)
                {
                    // Flatten incoming links in the same transaction. All targets remain canonical and cycles are impossible.
                    var incoming = await work.WriteRepository<LabTestCatalog>().FirstOrDefaultAsync(new DiagnosticForUpdate<LabTestCatalog>(c => c.MergedIntoId == catalog.Id), ct);
                    while (incoming is not null)
                    {
                        incoming.RedirectMerge(target.Id, actor.UserId, clock.UtcNow);
                        await work.SaveChangesAsync(ct);
                        incoming = await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<LabTestCatalog>(c => c.MergedIntoId == catalog.Id), ct);
                    }
                }
            }
            else change = r.Mutation == CatalogMutation.Update ? catalog.Update(r.Data ?? new(), actor.UserId, clock.UtcNow) : catalog.SetActive(r.Mutation == CatalogMutation.Activate, r.Reason, actor.UserId, clock.UtcNow);
            if (change.IsFailure) return Result<MedicalCatalogResponse>.Fail(change.Errors);
        }
        await work.SaveChangesAsync(ct);
        return Result<MedicalCatalogResponse>.Ok((await reader.CatalogAsync(new(r.Kind, false, Id: catalog.Id), actor.Permissions, ct)).Items.Single());
    }
    private async Task<Result<MedicalCatalogRequestResponse>> ReviewLabAsync(MutateMedicalCatalogRequestCommand r, DiagnosticActor actor, bool own, CancellationToken ct)
    {
        var repo = work.WriteRepository<LabCatalogRequest>();
        LabCatalogRequest? request;
        if (r.Mutation == CatalogRequestMutation.Create)
        {
            var created = LabCatalogRequest.Submit(r.Data ?? new(""), actor.DoctorId!.Value, actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(created.Errors);
            request = created.Value; await repo.AddAsync(request, ct);
        }
        else
        {
            request = await repo.GetByIdAsync(r.Id.GetValueOrDefault(), ct);
            if (request is null || own && request.RequestedByDoctorId != actor.DoctorId) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.NotFound("LabCatalogRequest.NotFound"));
            if (!TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? "")) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("LabCatalogRequest.ConcurrencyConflict"));
            Guid? canonical = r.CanonicalCatalogId;
            if (r.Mutation == CatalogRequestMutation.Approve && canonical is null)
            {
                var created = LabTestCatalog.CreateWasla(r.ApprovedData ?? new(DisplayNameEn: request.Name), actor.UserId, clock.UtcNow);
                if (created.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(created.Errors);
                if (await work.WriteRepository<LabTestCatalog>().GetByPropertyAsync(c => c.NormalizedName == created.Value.NormalizedName, ct) is not null)
                    return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("LabCatalog.PossibleDuplicate"));
                canonical = created.Value.Id; await work.WriteRepository<LabTestCatalog>().AddAsync(created.Value, ct);
            }
            else if (canonical.HasValue)
            {
                var target = await work.WriteRepository<LabTestCatalog>().GetByIdAsync(canonical.Value, ct);
                if (target is null || !target.IsSelectable) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("LabCatalogRequest.InvalidDuplicateTarget"));
            }
            var change = r.Mutation == CatalogRequestMutation.Update ? request.Update(r.Data ?? new(""), actor.UserId, clock.UtcNow) : request.Review(
                r.Mutation == CatalogRequestMutation.MoreInfo ? MedicalCatalogRequestStatus.NeedsMoreInfo : r.Mutation == CatalogRequestMutation.Approve ? MedicalCatalogRequestStatus.Approved : MedicalCatalogRequestStatus.Rejected,
                r.Reason, canonical, actor.UserId, clock.UtcNow);
            if (change.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(change.Errors);
        }
        await work.SaveChangesAsync(ct);
        return Result<MedicalCatalogRequestResponse>.Ok((await reader.CatalogRequestsAsync(new(r.Kind, own, request.Id), own ? actor.DoctorId : null, actor.Permissions, ct)).Items.Single());
    }


    private async Task<Result<MedicalCatalogResponse>> MutateRadiologyCatalogAsync(MutateMedicalCatalogCommand r, DiagnosticActor actor, CancellationToken ct)
    {
        var repo = work.WriteRepository<RadiologyProcedureCatalog>();
        RadiologyProcedureCatalog? catalog;
        if (r.Mutation == CatalogMutation.Create)
        {
            var created = RadiologyProcedureCatalog.CreateWasla(r.Data ?? new(), actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<MedicalCatalogResponse>.Fail(created.Errors);
            catalog = created.Value;
            if (await repo.GetByPropertyAsync(c => c.NormalizedName == catalog.NormalizedName, ct) is not null)
                return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalog.PossibleDuplicate"));
            await repo.AddAsync(catalog, ct);
        }
        else
        {
            catalog = await repo.GetByIdAsync(r.Id.GetValueOrDefault(), ct);
            if (catalog is null) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.NotFound("RadiologyCatalog.NotFound"));
            if (!TicketRowVersion.Matches(catalog.RowVersion, r.RowVersion ?? "")) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalog.ConcurrencyConflict"));
            Result change;
            if (r.Mutation == CatalogMutation.Merge)
            {
                var target = await repo.GetByIdAsync(r.TargetId.GetValueOrDefault(), ct);
                if (target is null) return Result<MedicalCatalogResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalog.InvalidMergeTarget"));
                change = catalog.MergeInto(target, r.Reason, actor.UserId, clock.UtcNow);
                if (change.IsSuccess)
                {
                    // Flatten incoming links in the same transaction. All targets remain canonical and cycles are impossible.
                    var incoming = await work.WriteRepository<RadiologyProcedureCatalog>().FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyProcedureCatalog>(c => c.MergedIntoId == catalog.Id), ct);
                    while (incoming is not null)
                    {
                        incoming.RedirectMerge(target.Id, actor.UserId, clock.UtcNow);
                        await work.SaveChangesAsync(ct);
                        incoming = await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyProcedureCatalog>(c => c.MergedIntoId == catalog.Id), ct);
                    }
                }
            }
            else change = r.Mutation == CatalogMutation.Update ? catalog.Update(r.Data ?? new(), actor.UserId, clock.UtcNow) : catalog.SetActive(r.Mutation == CatalogMutation.Activate, r.Reason, actor.UserId, clock.UtcNow);
            if (change.IsFailure) return Result<MedicalCatalogResponse>.Fail(change.Errors);
        }
        await work.SaveChangesAsync(ct);
        return Result<MedicalCatalogResponse>.Ok((await reader.CatalogAsync(new(r.Kind, false, Id: catalog.Id), actor.Permissions, ct)).Items.Single());
    }
    private async Task<Result<MedicalCatalogRequestResponse>> ReviewRadiologyAsync(MutateMedicalCatalogRequestCommand r, DiagnosticActor actor, bool own, CancellationToken ct)
    {
        var repo = work.WriteRepository<RadiologyCatalogRequest>();
        RadiologyCatalogRequest? request;
        if (r.Mutation == CatalogRequestMutation.Create)
        {
            var created = RadiologyCatalogRequest.Submit(r.Data ?? new(""), actor.DoctorId!.Value, actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(created.Errors);
            request = created.Value; await repo.AddAsync(request, ct);
        }
        else
        {
            request = await repo.GetByIdAsync(r.Id.GetValueOrDefault(), ct);
            if (request is null || own && request.RequestedByDoctorId != actor.DoctorId) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.NotFound("RadiologyCatalogRequest.NotFound"));
            if (!TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? "")) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalogRequest.ConcurrencyConflict"));
            Guid? canonical = r.CanonicalCatalogId;
            if (r.Mutation == CatalogRequestMutation.Approve && canonical is null)
            {
                var created = RadiologyProcedureCatalog.CreateWasla(r.ApprovedData ?? new(DisplayNameEn: request.Name), actor.UserId, clock.UtcNow);
                if (created.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(created.Errors);
                if (await work.WriteRepository<RadiologyProcedureCatalog>().GetByPropertyAsync(c => c.NormalizedName == created.Value.NormalizedName, ct) is not null)
                    return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalog.PossibleDuplicate"));
                canonical = created.Value.Id; await work.WriteRepository<RadiologyProcedureCatalog>().AddAsync(created.Value, ct);
            }
            else if (canonical.HasValue)
            {
                var target = await work.WriteRepository<RadiologyProcedureCatalog>().GetByIdAsync(canonical.Value, ct);
                if (target is null || !target.IsSelectable) return Result<MedicalCatalogRequestResponse>.Fail(DiagnosticErrors.Conflict("RadiologyCatalogRequest.InvalidDuplicateTarget"));
            }
            var change = r.Mutation == CatalogRequestMutation.Update ? request.Update(r.Data ?? new(""), actor.UserId, clock.UtcNow) : request.Review(
                r.Mutation == CatalogRequestMutation.MoreInfo ? MedicalCatalogRequestStatus.NeedsMoreInfo : r.Mutation == CatalogRequestMutation.Approve ? MedicalCatalogRequestStatus.Approved : MedicalCatalogRequestStatus.Rejected,
                r.Reason, canonical, actor.UserId, clock.UtcNow);
            if (change.IsFailure) return Result<MedicalCatalogRequestResponse>.Fail(change.Errors);
        }
        await work.SaveChangesAsync(ct);
        return Result<MedicalCatalogRequestResponse>.Ok((await reader.CatalogRequestsAsync(new(r.Kind, own, request.Id), own ? actor.DoctorId : null, actor.Permissions, ct)).Items.Single());
    }

}
