using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Subscriptions;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.CallNext;

public sealed record CallNextCommand(Guid PracticeId, string IdempotencyKey)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CallNextCommandValidator : AbstractValidator<CallNextCommand>
{
    public CallNextCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CallNextCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    ITicketQueueReader queueReader,
    IDateTimeProvider clock,
    ArchiveTicketReplay archiveReplay)
    : ICommandHandler<CallNextCommand, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        CallNextCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticeTicketsCallOwn,
            PermissionNames.PracticeTicketsCall,
            cancellationToken, deferSubscriptionCheck: true);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var key = TicketIdempotency.ValidateKey(request.IdempotencyKey);
        if (key.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(key.Errors);
        }

        var configuration = await unitOfWork.WriteRepository<DoctorPracticeConfiguration>()
            .GetByPropertyAsync(item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.PracticeUnavailable);
        }

        var nowUtc = EnsureUtc(clock.UtcNow);
        var businessDate = TicketBusinessClock.CurrentBusinessDate(nowUtc, configuration.TimeZoneId);
        await queueLock.AcquirePracticeDayAsync(request.PracticeId, businessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "CallNext", key.Value, cancellationToken);
        var idempotency = await TicketIdempotency.BeginAsync(
            unitOfWork.WriteRepository<TicketIdempotencyRecord>(),
            actor.Value.ApplicationUserId,
            "CallNext",
            key.Value,
            TicketIdempotency.Fingerprint(request.PracticeId, businessDate),
            nowUtc,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingTicketId is { } replayId)
        {
            var replayAccess = await access.AuthorizeDoctorOrReceptionAsync(request.PracticeId,
                PermissionNames.DoctorPracticeTicketsCallOwn, PermissionNames.PracticeTicketsCall, cancellationToken, new(SubscriptionResourceKind.Ticket, replayId));
            if (replayAccess.IsFailure) return Result<TicketDetailsResponse>.Fail(replayAccess.Errors);
            return await DetailsAsync(replayId, cancellationToken);
        }

        if (await queueReader.HasCalledOrInProgressAsync(
                request.PracticeId, cancellationToken))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.QueueBusy);
        }

        var nextId = await queueReader.FindNextWaitingTicketIdAsync(
            request.PracticeId, businessDate, cancellationToken);
        if (!nextId.HasValue)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }

        var targetAccess = await access.AuthorizeDoctorOrReceptionAsync(request.PracticeId,
            PermissionNames.DoctorPracticeTicketsCallOwn, PermissionNames.PracticeTicketsCall, cancellationToken, new(SubscriptionResourceKind.Ticket, nextId.Value));
        if (targetAccess.IsFailure) return Result<TicketDetailsResponse>.Fail(targetAccess.Errors);
        var archivedReplay = await archiveReplay.TryAsync(actor.Value.ApplicationUserId, "CallNext", key.Value, TicketIdempotency.Fingerprint(request.PracticeId, businessDate), request.PracticeId, cancellationToken);
        if (archivedReplay != null) return archivedReplay;

        var ticket = await unitOfWork.WriteRepository<Ticket>().FirstOrDefaultAsync(
            new TicketByIdForUpdateSpecification(nextId.Value), cancellationToken);
        if (ticket is null || ticket.DoctorPracticeId != request.PracticeId ||
            ticket.BusinessDate != businessDate)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }

        var called = ticket.Call(actor.Value.ApplicationUserId, nowUtc);
        if (called.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(called.Errors);
        }

        idempotency.Value.Record!.Complete(ticket.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await DetailsAsync(ticket.Id, cancellationToken);
    }

    private async Task<Result<TicketDetailsResponse>> DetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        var details = await queueReader.GetDetailsAsync(ticketId, cancellationToken);
        return details is null
            ? Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound)
            : Result<TicketDetailsResponse>.Ok(details);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
