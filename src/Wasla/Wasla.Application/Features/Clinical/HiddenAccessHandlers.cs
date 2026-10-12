using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Clinical;

internal sealed class HiddenRequestForUpdate : Specification<HiddenEncounterAccessRequest>
{
    public HiddenRequestForUpdate(Guid id) { AddCriteria(r => r.Id == id); UseTracking(); }
}
internal sealed class HiddenRequestsForTicket : Specification<HiddenEncounterAccessRequest>
{
    public HiddenRequestsForTicket(Guid ticket) { AddCriteria(r => r.RequestingTicketId == ticket && (r.Status == HiddenAccessRequestStatus.Pending || r.Status == HiddenAccessRequestStatus.Approved)); UseTracking(); }
}
internal sealed class RequestHiddenAccessValidator : AbstractValidator<RequestHiddenEncounterAccessCommand>
{
    public RequestHiddenAccessValidator()
    {
        RuleFor(r => r.PracticeId).NotEmpty(); RuleFor(r => r.TicketId).NotEmpty();
        RuleFor(r => r.HiddenEncounterToken).NotEmpty().MaximumLength(4096); RuleFor(r => r.Reason).NotEmpty().Must(s => !string.IsNullOrWhiteSpace(s)).MaximumLength(1000);
        RuleFor(r => r.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
internal sealed class DecideHiddenAccessValidator : AbstractValidator<DecideHiddenEncounterAccessCommand>
{
    public DecideHiddenAccessValidator()
    {
        RuleFor(r => r.RequestId).NotEmpty(); RuleFor(r => r.Decision).IsInEnum(); RuleFor(r => r.RowVersion).Must(TicketRowVersion.IsValid);
        When(r => r.Decision == HiddenAccessDecision.Approve, () => RuleFor(r => r.IdempotencyKey).NotEmpty().MaximumLength(200));
    }
}
internal sealed class HiddenAccessQueryValidator : AbstractValidator<HiddenAccessQuery>
{
    public HiddenAccessQueryValidator()
    {
        RuleFor(r => r.Read).IsInEnum(); RuleFor(r => r.PageSize).InclusiveBetween(1, 100); RuleFor(r => r.Cursor).MaximumLength(4096);
        RuleFor(r => r.Search).MaximumLength(200); RuleFor(r => r.Status).Must(s => s is null || Enum.IsDefined(s.Value));
    }
}
internal sealed class HiddenAccessQueryHandler(IMedicalRecordAccessPolicy policy, IHiddenAccessReader reader) : IQueryHandler<HiddenAccessQuery, object>
{
    public async Task<Result<object>> Handle(HiddenAccessQuery r, CancellationToken ct)
    {
        var permission = r.Read is HiddenAccessRead.Sent or HiddenAccessRead.SentDetail ? PermissionNames.HiddenEncounterAccessRequestsViewSent
            : r.Read == HiddenAccessRead.History ? PermissionNames.HiddenEncounterAccessRequestsViewHistoryOwn : PermissionNames.HiddenEncounterAccessRequestsViewIncoming;
        var actor = await policy.DoctorAsync(permission, ct);
        if (actor.IsFailure) return Result<object>.Fail(MedicalRecordErrors.Denied);
        try { var value = await reader.ReadAsync(r, actor.Value, ct); return value is null ? Result<object>.Fail(MedicalRecordErrors.Denied) : Result<object>.Ok(value); }
        catch (ArgumentException) { return Result<object>.Fail(MedicalRecordErrors.InvalidInput); }
    }
}
internal sealed class RequestHiddenAccessHandler(IMedicalRecordAccessPolicy policy, IMedicalRecordTokens tokens,
    IUnitOfWork<WaslaWritePersistence> uow, IHiddenAccessReader reader, ITicketQueueLock locks, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<RequestHiddenEncounterAccessCommand, HiddenAccessResponse>
{
    public async Task<Result<HiddenAccessResponse>> Handle(RequestHiddenEncounterAccessCommand r, CancellationToken ct)
    {
        var actor = await policy.DoctorAsync(PermissionNames.HiddenEncounterAccessRequestsCreate, ct);
        var token = tokens.Read<HiddenEncounterToken>("HiddenEncounter", r.HiddenEncounterToken);
        if (actor.IsFailure || token is null || token.ActorId != actor.Value.UserId || token.DoctorId != actor.Value.DoctorId ||
            token.TicketId != r.TicketId || token.IssuedAtUtc > clock.UtcNow || token.IssuedAtUtc < clock.UtcNow.AddMinutes(-15))
            return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:Treatment", r.TicketId.ToString("N"), ct);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:Source", token.EncounterId.ToString("N"), ct);
        var scope = await policy.ScopeAsync(MedicalRecordMode.TreatingDoctor, r.PracticeId, r.TicketId, null, PermissionNames.HiddenEncounterAccessRequestsCreate, ct);
        if (scope.IsFailure) return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        var source = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(token.EncounterId), ct);
        var ticket = await uow.WriteRepository<Ticket>().FirstOrDefaultAsync(new TicketByIdForUpdateSpecification(r.TicketId), ct);
        var treating = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(r.TicketId, true), ct);
        if (source is null || ticket is null || treating is null || source.VisibilityRevision != token.Revision)
            return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        var originalTicket = await uow.WriteRepository<Ticket>().FirstOrDefaultAsync(new TicketByIdForUpdateSpecification(source.TicketId), ct);
        if (originalTicket is null || originalTicket.Status != TicketStatus.Completed || originalTicket.PatientId != source.PatientId ||
            originalTicket.DoctorId != source.DoctorId || originalTicket.DoctorPracticeId != source.DoctorPracticeId)
            return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        return await idem.ExecuteAsync(actor.Value.UserId, "Phase16:RequestAccess", r.IdempotencyKey,
            new { r.PracticeId, r.TicketId, Source = source.Id, Reason = r.Reason.Trim() }, async () =>
            {
                var repo = uow.WriteRepository<HiddenEncounterAccessRequest>();
                if (await repo.GetByPropertyAsync(x => x.HiddenEncounterId == source.Id && x.RequestingTicketId == ticket.Id, ct) is not null)
                    return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Duplicate);
                var created = HiddenEncounterAccessRequest.Create(source, ticket, treating, actor.Value.DoctorId, r.PracticeId, r.Reason, actor.Value.UserId, clock.UtcNow);
                if (created.IsFailure) return Result<HiddenAccessResponse>.Fail(created.Errors);
                await repo.AddAsync(created.Value, ct); await uow.SaveChangesAsync(ct);
                MedicalRecordTelemetry.Request("Create", created.Value.Status.ToString());
                return Result<HiddenAccessResponse>.Ok((await reader.ResponseAsync(created.Value.Id, actor.Value, false, ct))!);
            }, ct, async original =>
            {
                var value = await reader.ResponseAsync(original.RequestId, actor.Value, false, ct);
                return value is null ? Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied) : Result<HiddenAccessResponse>.Ok(value);
            });
    }
}
internal sealed class DecideHiddenAccessHandler(IMedicalRecordAccessPolicy policy, IWaslaDataStore store,
    IUnitOfWork<WaslaWritePersistence> uow, IHiddenAccessReader reader, ITicketQueueLock locks, MedicationIdempotency idem, IDateTimeProvider clock)
    : ICommandHandler<DecideHiddenEncounterAccessCommand, HiddenAccessResponse>
{
    public async Task<Result<HiddenAccessResponse>> Handle(DecideHiddenEncounterAccessCommand r, CancellationToken ct)
    {
        var actor = await policy.DoctorAsync(r.Decision == HiddenAccessDecision.Revoke ? PermissionNames.HiddenEncounterAccessRequestsRevokeOwn : PermissionNames.HiddenEncounterAccessRequestsDecideOwn, ct);
        var route = actor.IsSuccess ? await reader.RoutingAsync(r.RequestId, ct) : null;
        if (route is null || route.OwningDoctorId != actor.Value.DoctorId) return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:Treatment", route.TicketId.ToString("N"), ct);
        await locks.AcquireIdempotencyAsync(Guid.Empty, "Phase16:Source", route.EncounterId.ToString("N"), ct);
        var source = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(route.EncounterId), ct);
        var request = await uow.WriteRepository<HiddenEncounterAccessRequest>().FirstOrDefaultAsync(new HiddenRequestForUpdate(r.RequestId), ct);
        if (source is null || request is null || await store.FindDoctorPracticeAsync(source.DoctorPracticeId, ct) is not { IsActive: true } practice || practice.DoctorId != actor.Value.DoctorId)
            return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Denied);
        async Task<Result<HiddenAccessResponse>> Change()
        {
            if (!TicketRowVersion.Matches(request.RowVersion, r.RowVersion)) return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Conflict);
            if ((await reader.ResponseAsync(request.Id, actor.Value, true, ct))?.EffectiveStatus == HiddenAccessRequestStatus.Expired) return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Expired);
            var ticket = await uow.WriteRepository<Ticket>().FirstOrDefaultAsync(new TicketByIdForUpdateSpecification(request.RequestingTicketId), ct);
            var treating = await uow.WriteRepository<MedicalEncounter>().FirstOrDefaultAsync(new EncounterForUpdateSpecification(request.RequestingEncounterId), ct);
            if (ticket is null || treating is null || !request.ContextLive(source, ticket, treating)) return Result<HiddenAccessResponse>.Fail(MedicalRecordErrors.Expired);
            var desired = r.Decision == HiddenAccessDecision.Reject ? HiddenAccessRequestStatus.Rejected : HiddenAccessRequestStatus.Revoked;
            var changed = r.Decision != HiddenAccessDecision.Approve && request.Status == desired ? Result.Ok()
                : r.Decision == HiddenAccessDecision.Revoke ? request.Revoke(actor.Value.DoctorId, actor.Value.UserId, clock.UtcNow)
                : request.Decide(r.Decision == HiddenAccessDecision.Approve, source, ticket, treating, actor.Value.DoctorId, actor.Value.UserId, clock.UtcNow);
            if (changed.IsFailure) return Result<HiddenAccessResponse>.Fail(changed.Errors);
            await uow.SaveChangesAsync(ct);
            MedicalRecordTelemetry.Request(r.Decision.ToString(), request.Status.ToString());
            return Result<HiddenAccessResponse>.Ok((await reader.ResponseAsync(request.Id, actor.Value, true, ct))!);
        }
        return r.Decision != HiddenAccessDecision.Approve ? await Change()
            : await idem.ExecuteAsync(actor.Value.UserId, "Phase16:ApproveAccess", r.IdempotencyKey, new { r.RequestId, r.RowVersion }, Change, ct,
                async _ => Result<HiddenAccessResponse>.Ok((await reader.ResponseAsync(request.Id, actor.Value, true, ct))!));
    }
}
