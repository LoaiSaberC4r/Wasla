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

namespace Wasla.Application.Features.Tickets.ConfirmNoResponse;

public sealed record ConfirmNoResponseCommand(
    Guid PracticeId,
    Guid TicketId,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class ConfirmNoResponseCommandValidator
    : AbstractValidator<ConfirmNoResponseCommand>
{
    public ConfirmNoResponseCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.TicketId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class ConfirmNoResponseCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    ITicketQueueReader queueReader,
    IDateTimeProvider clock)
    : ICommandHandler<ConfirmNoResponseCommand, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        ConfirmNoResponseCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticeTicketsConfirmNoResponseOwn,
            PermissionNames.PracticeTicketsCall,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var key = TicketIdempotency.ValidateKey(request.IdempotencyKey);
        if (key.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(key.Errors);
        }

        var ticket = await unitOfWork.WriteRepository<Ticket>().FirstOrDefaultAsync(
            new TicketByIdForUpdateSpecification(request.TicketId), cancellationToken);
        if (ticket is null || ticket.DoctorPracticeId != request.PracticeId)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }

        var configuration = await unitOfWork.WriteRepository<DoctorPracticeConfiguration>()
            .GetByPropertyAsync(item => item.DoctorPracticeId == request.PracticeId, cancellationToken);
        if (configuration is null)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.PracticeUnavailable);
        }

        await queueLock.AcquirePracticeDayAsync(
            request.PracticeId, ticket.BusinessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "ConfirmNoResponse", key.Value, cancellationToken);
        var nowUtc = EnsureUtc(clock.UtcNow);
        var idempotency = await TicketIdempotency.BeginAsync(
            unitOfWork.WriteRepository<TicketIdempotencyRecord>(),
            actor.Value.ApplicationUserId,
            "ConfirmNoResponse",
            key.Value,
            TicketIdempotency.Fingerprint(request.PracticeId, request.TicketId, request.RowVersion),
            nowUtc,
            cancellationToken);
        if (idempotency.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(idempotency.Errors);
        }

        if (idempotency.Value.ExistingTicketId is { } replayId)
        {
            return await DetailsAsync(replayId, cancellationToken);
        }

        if (!TicketRowVersion.Matches(ticket.RowVersion, request.RowVersion))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.ConcurrencyConflict);
        }

        var confirmed = ticket.ConfirmNoResponse(
            actor.Value.ApplicationUserId, configuration.MaximumTicketCallAttempts, nowUtc);
        if (confirmed.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(confirmed.Errors);
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
