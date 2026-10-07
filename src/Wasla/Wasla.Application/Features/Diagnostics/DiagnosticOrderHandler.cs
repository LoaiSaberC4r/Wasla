
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

internal sealed class DiagnosticOrderHandler(DiagnosticAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IDiagnosticReadService reader, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<DiagnosticOrderCommand, DiagnosticRequestStateResponse>
{
    public async Task<Result<DiagnosticRequestStateResponse>> Handle(DiagnosticOrderCommand r, CancellationToken ct)
    {
        var permission = r.Kind + "Requests." + (r.Mutation == DiagnosticOrderMutation.PostVisit ? "CreatePostVisitOwn" : r.Mutation == DiagnosticOrderMutation.Cancel ? "CancelOwn" : "ManageOwnDraft");
        var actor = await access.ActorAsync(permission, UserType.Doctor, ct);
        if (actor.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(actor.Errors);
        if ((r.Item?.NewTest is not null || r.Items?.Any(i => i.NewTest is not null) == true) && !actor.Value.Permissions.Contains(r.Kind + "CatalogRequests.CreateOwn"))
            return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Denied());
        await idem.LockResourceAsync("Phase15:" + r.Kind + "Order", r.RequestId ?? r.EncounterId.GetValueOrDefault(), ct);
        Task<Result<DiagnosticRequestStateResponse>> Run() => r.Kind == DiagnosticKind.Lab ? MutateLabAsync(r, actor.Value, ct) : MutateRadiologyAsync(r, actor.Value, ct);
        return r.Mutation is DiagnosticOrderMutation.Add or DiagnosticOrderMutation.PostVisit ?
            await idem.ExecuteAsync(actor.Value.UserId, "Phase15:" + r.Kind + ":" + r.Mutation, r.IdempotencyKey, r, Run, ct, async original =>
            {
                if (original.RequestId is not { } id) return Result<DiagnosticRequestStateResponse>.Ok(original);
                var latest = await reader.RequestAsync(r.Kind, id, actor.Value.OwnerId, false, actor.Value.Permissions, ct);
                if (latest is not null) return Result<DiagnosticRequestStateResponse>.Ok(latest);
                var draft = await reader.ReadAsync(new(r.Kind, DiagnosticReadResource.Draft, PracticeId: r.PracticeId, EncounterId: r.EncounterId), actor.Value.OwnerId, actor.Value.Permissions, ct);
                return draft is DiagnosticRequestStateResponse state ? Result<DiagnosticRequestStateResponse>.Ok(state) : Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.NotFound(r.Kind + "Request.NotFound"));
            }) : await Run();
    }

    private async Task<Result<DiagnosticRequestStateResponse>> MutateLabAsync(DiagnosticOrderCommand r, DiagnosticActor actor, CancellationToken ct)
    {
        var repo = work.WriteRepository<LabRequest>();
        var request = r.RequestId.HasValue ? await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<LabRequest>(x => x.Id == r.RequestId, x => x.Items), ct) :
            await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<LabRequest>(x => x.MedicalEncounterId == r.EncounterId && x.Status == DiagnosticRequestStatus.Draft, x => x.Items), ct);
        var encounter = await work.WriteRepository<MedicalEncounter>().GetByIdAsync(request?.MedicalEncounterId ?? r.EncounterId.GetValueOrDefault(), ct);
        if (encounter is null || encounter.DoctorId != actor.DoctorId || r.PracticeId.HasValue && encounter.DoctorPracticeId != r.PracticeId ||
            !await access.OwnPracticeAsync(actor.DoctorId!.Value, encounter.DoctorPracticeId, ct) || r.RequestId.HasValue && request is null)
            return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.NotFound("LabRequest.NotFound"));
        if (r.Mutation == DiagnosticOrderMutation.PostVisit)
        {
            var created = LabRequest.CreatePostVisit(encounter, r.Reason ?? "", r.PatientInstructions, actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(created.Errors);
            request = created.Value; await repo.AddAsync(request, ct);
            if (r.Items is not { Count: > 0 }) return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Validation("LabRequest.Empty"));
            foreach (var item in r.Items)
            {
                var added = await AddLabAsync(request, item, actor, ct); if (added.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(added.Errors);
            }
            var published = request.Publish(actor.UserId, clock.UtcNow); if (published.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(published.Errors);
        }
        else
        {
            if (request is not null && !TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? "") || request is null && !string.IsNullOrWhiteSpace(r.RowVersion))
                return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Conflict("LabRequest.ConcurrencyConflict"));
            if (r.Mutation != DiagnosticOrderMutation.Cancel && encounter.Status != EncounterStatus.InProgress)
                return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Conflict("LabRequest.InvalidEncounterState"));
            if (request is null && r.Mutation == DiagnosticOrderMutation.Add)
            {
                var created = LabRequest.CreateDraft(encounter, actor.UserId, clock.UtcNow, r.PatientInstructions);
                if (created.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(created.Errors);
                request = created.Value; await repo.AddAsync(request, ct);
            }
            if (request is null) return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.NotFound("LabRequest.NotFound"));
            var changed = r.Mutation switch
            {
                DiagnosticOrderMutation.Add => await AddLabAsync(request, r.Item ?? new(), actor, ct),
                DiagnosticOrderMutation.Update => request.UpdateItem(r.ItemId.GetValueOrDefault(), r.Item?.DoctorInstructions, actor.UserId, clock.UtcNow),
                DiagnosticOrderMutation.Remove => request.RemoveItem(r.ItemId.GetValueOrDefault(), actor.UserId, clock.UtcNow),
                DiagnosticOrderMutation.Cancel => request.Cancel(r.ItemId, r.Reason ?? "", actor.UserId, clock.UtcNow),
                _ => Result.Fail(DiagnosticErrors.Conflict("LabRequest.InvalidState"))
            };
            if (changed.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(changed.Errors);
            if (r.Mutation == DiagnosticOrderMutation.Cancel && !request.Items.Any(i => i.Status == DiagnosticItemStatus.Requested))
            {
                // A submission is a document for the request. Cancellation of its remaining requestable items ends applicability.
                var pending = await work.WriteRepository<PatientLabResultSubmission>().FirstOrDefaultAsync(new DiagnosticForUpdate<PatientLabResultSubmission>(s => s.RequestId == request.Id && s.Status == PatientSubmissionStatus.PendingReview), ct);
                while (pending is not null)
                {
                    pending.NoLongerApplicable(actor.UserId, clock.UtcNow); await work.SaveChangesAsync(ct);
                    pending = await work.WriteRepository<PatientLabResultSubmission>().FirstOrDefaultAsync(new DiagnosticForUpdate<PatientLabResultSubmission>(s => s.RequestId == request.Id && s.Status == PatientSubmissionStatus.PendingReview), ct);
                }
            }
        }
        if (request.Status == DiagnosticRequestStatus.Draft && request.Items.Count == 0) repo.Delete(request);
        await work.SaveChangesAsync(ct);
        if (request.Status == DiagnosticRequestStatus.Draft && request.Items.Count == 0)
        {
            var empty = await reader.ReadAsync(new(r.Kind, DiagnosticReadResource.Draft, PracticeId: encounter.DoctorPracticeId, EncounterId: encounter.Id), actor.OwnerId, actor.Permissions, ct);
            return Result<DiagnosticRequestStateResponse>.Ok((DiagnosticRequestStateResponse)empty!);
        }
        return Result<DiagnosticRequestStateResponse>.Ok((await reader.RequestAsync(r.Kind, request.Id, actor.OwnerId, false, actor.Permissions, ct))!);
    }
    private async Task<Result> AddLabAsync(LabRequest request, DiagnosticOrderItemInput item, DiagnosticActor actor, CancellationToken ct)
    {
        if (item.CatalogId.HasValue == (item.NewTest is not null)) return Result.Fail(DiagnosticErrors.Validation(item.CatalogId.HasValue ? "LabRequest.MultipleSources" : "LabRequest.SourceRequired"));
        if (item.CatalogId is { } id)
        {
            await idem.LockResourceAsync("Phase15:LabCatalogApply", Guid.Empty, ct);
            var catalog = await work.WriteRepository<LabTestCatalog>().GetByIdAsync(id, ct);
            return catalog is null ? Result.Fail(DiagnosticErrors.NotFound("LabCatalog.NotFound")) : request.AddCatalogItem(catalog, item.DoctorInstructions, actor.UserId, clock.UtcNow);
        }
        var submitted = LabCatalogRequest.Submit(item.NewTest!, actor.DoctorId!.Value, actor.UserId, clock.UtcNow);
        if (submitted.IsFailure) return Result.Fail(submitted.Errors);
        await work.WriteRepository<LabCatalogRequest>().AddAsync(submitted.Value, ct);
        return request.AddSubmittedItem(submitted.Value, item.DoctorInstructions, actor.UserId, clock.UtcNow);
    }


    private async Task<Result<DiagnosticRequestStateResponse>> MutateRadiologyAsync(DiagnosticOrderCommand r, DiagnosticActor actor, CancellationToken ct)
    {
        var repo = work.WriteRepository<RadiologyRequest>();
        var request = r.RequestId.HasValue ? await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyRequest>(x => x.Id == r.RequestId, x => x.Items), ct) :
            await repo.FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyRequest>(x => x.MedicalEncounterId == r.EncounterId && x.Status == DiagnosticRequestStatus.Draft, x => x.Items), ct);
        var encounter = await work.WriteRepository<MedicalEncounter>().GetByIdAsync(request?.MedicalEncounterId ?? r.EncounterId.GetValueOrDefault(), ct);
        if (encounter is null || encounter.DoctorId != actor.DoctorId || r.PracticeId.HasValue && encounter.DoctorPracticeId != r.PracticeId ||
            !await access.OwnPracticeAsync(actor.DoctorId!.Value, encounter.DoctorPracticeId, ct) || r.RequestId.HasValue && request is null)
            return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.NotFound("RadiologyRequest.NotFound"));
        if (r.Mutation == DiagnosticOrderMutation.PostVisit)
        {
            var created = RadiologyRequest.CreatePostVisit(encounter, r.Reason ?? "", r.PatientInstructions, actor.UserId, clock.UtcNow);
            if (created.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(created.Errors);
            request = created.Value; await repo.AddAsync(request, ct);
            if (r.Items is not { Count: > 0 }) return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Validation("RadiologyRequest.Empty"));
            foreach (var item in r.Items)
            {
                var added = await AddRadiologyAsync(request, item, actor, ct); if (added.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(added.Errors);
            }
            var published = request.Publish(actor.UserId, clock.UtcNow); if (published.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(published.Errors);
        }
        else
        {
            if (request is not null && !TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? "") || request is null && !string.IsNullOrWhiteSpace(r.RowVersion))
                return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Conflict("RadiologyRequest.ConcurrencyConflict"));
            if (r.Mutation != DiagnosticOrderMutation.Cancel && encounter.Status != EncounterStatus.InProgress)
                return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidEncounterState"));
            if (request is null && r.Mutation == DiagnosticOrderMutation.Add)
            {
                var created = RadiologyRequest.CreateDraft(encounter, actor.UserId, clock.UtcNow, r.PatientInstructions);
                if (created.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(created.Errors);
                request = created.Value; await repo.AddAsync(request, ct);
            }
            if (request is null) return Result<DiagnosticRequestStateResponse>.Fail(DiagnosticErrors.NotFound("RadiologyRequest.NotFound"));
            var changed = r.Mutation switch
            {
                DiagnosticOrderMutation.Add => await AddRadiologyAsync(request, r.Item ?? new(), actor, ct),
                DiagnosticOrderMutation.Update => request.UpdateItem(r.ItemId.GetValueOrDefault(), r.Item?.DoctorInstructions, actor.UserId, clock.UtcNow),
                DiagnosticOrderMutation.Remove => request.RemoveItem(r.ItemId.GetValueOrDefault(), actor.UserId, clock.UtcNow),
                DiagnosticOrderMutation.Cancel => request.Cancel(r.ItemId, r.Reason ?? "", actor.UserId, clock.UtcNow),
                _ => Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"))
            };
            if (changed.IsFailure) return Result<DiagnosticRequestStateResponse>.Fail(changed.Errors);
            if (r.Mutation == DiagnosticOrderMutation.Cancel && !request.Items.Any(i => i.Status == DiagnosticItemStatus.Requested))
            {
                // A submission is a document for the request. Cancellation of its remaining requestable items ends applicability.
                var pending = await work.WriteRepository<PatientRadiologyResultSubmission>().FirstOrDefaultAsync(new DiagnosticForUpdate<PatientRadiologyResultSubmission>(s => s.RequestId == request.Id && s.Status == PatientSubmissionStatus.PendingReview), ct);
                while (pending is not null)
                {
                    pending.NoLongerApplicable(actor.UserId, clock.UtcNow); await work.SaveChangesAsync(ct);
                    pending = await work.WriteRepository<PatientRadiologyResultSubmission>().FirstOrDefaultAsync(new DiagnosticForUpdate<PatientRadiologyResultSubmission>(s => s.RequestId == request.Id && s.Status == PatientSubmissionStatus.PendingReview), ct);
                }
            }
        }
        if (request.Status == DiagnosticRequestStatus.Draft && request.Items.Count == 0) repo.Delete(request);
        await work.SaveChangesAsync(ct);
        if (request.Status == DiagnosticRequestStatus.Draft && request.Items.Count == 0)
        {
            var empty = await reader.ReadAsync(new(r.Kind, DiagnosticReadResource.Draft, PracticeId: encounter.DoctorPracticeId, EncounterId: encounter.Id), actor.OwnerId, actor.Permissions, ct);
            return Result<DiagnosticRequestStateResponse>.Ok((DiagnosticRequestStateResponse)empty!);
        }
        return Result<DiagnosticRequestStateResponse>.Ok((await reader.RequestAsync(r.Kind, request.Id, actor.OwnerId, false, actor.Permissions, ct))!);
    }
    private async Task<Result> AddRadiologyAsync(RadiologyRequest request, DiagnosticOrderItemInput item, DiagnosticActor actor, CancellationToken ct)
    {
        if (item.CatalogId.HasValue == (item.NewTest is not null)) return Result.Fail(DiagnosticErrors.Validation(item.CatalogId.HasValue ? "RadiologyRequest.MultipleSources" : "RadiologyRequest.SourceRequired"));
        if (item.CatalogId is { } id)
        {
            await idem.LockResourceAsync("Phase15:RadiologyCatalogApply", Guid.Empty, ct);
            var catalog = await work.WriteRepository<RadiologyProcedureCatalog>().GetByIdAsync(id, ct);
            return catalog is null ? Result.Fail(DiagnosticErrors.NotFound("RadiologyCatalog.NotFound")) : request.AddCatalogItem(catalog, item.DoctorInstructions, actor.UserId, clock.UtcNow);
        }
        var submitted = RadiologyCatalogRequest.Submit(item.NewTest!, actor.DoctorId!.Value, actor.UserId, clock.UtcNow);
        if (submitted.IsFailure) return Result.Fail(submitted.Errors);
        await work.WriteRepository<RadiologyCatalogRequest>().AddAsync(submitted.Value, ct);
        return request.AddSubmittedItem(submitted.Value, item.DoctorInstructions, actor.UserId, clock.UtcNow);
    }

}
