using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.StartVisit;

public sealed record StartVisitCommand(
    Guid PracticeId,
    Guid TicketId,
    string RowVersion,
    string IdempotencyKey)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class StartVisitCommandValidator : AbstractValidator<StartVisitCommand>
{
    public StartVisitCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.TicketId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(TicketRowVersion.IsValid);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class StartVisitCommandHandler(
    TicketAccessService access,
    IUnitOfWork<WaslaWritePersistence> unitOfWork,
    ITicketQueueLock queueLock,
    ITicketQueueReader queueReader,
    IReservationNoShowRuntimeReader reservationNoShowReader,
    IReservationProjectionInvalidationOutbox projectionOutbox,
    IDateTimeProvider clock)
    : ICommandHandler<StartVisitCommand, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        StartVisitCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorAsync(
            request.PracticeId, PermissionNames.DoctorPracticeTicketsStartOwn, cancellationToken);
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

        var nowUtc = EnsureUtc(clock.UtcNow);
        if (ticket.BusinessDate != TicketBusinessClock.CurrentBusinessDate(
                nowUtc, configuration.TimeZoneId))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.BusinessDateMismatch);
        }

        await queueLock.AcquirePracticeDayAsync(
            request.PracticeId, ticket.BusinessDate, cancellationToken);
        await queueLock.AcquireIdempotencyAsync(
            actor.Value.ApplicationUserId, "StartVisit", key.Value, cancellationToken);
        var idempotency = await TicketIdempotency.BeginAsync(
            unitOfWork.WriteRepository<TicketIdempotencyRecord>(),
            actor.Value.ApplicationUserId,
            "StartVisit",
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

        if (await queueReader.HasInProgressAsync(
                request.PracticeId, ticket.Id, cancellationToken))
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.QueueBusy);
        }

        var started = ticket.StartVisit(actor.Value.ApplicationUserId, nowUtc);
        if (started.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(started.Errors);
        }

        idempotency.Value.Record!.Complete(ticket.Id, nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var eligibleReservationIds = await reservationNoShowReader.FindEligibleReservationIdsAsync(
            request.PracticeId,
            ticket.BusinessDate,
            nowUtc,
            configuration.CheckInGracePeriodMinutes,
            configuration.NoShowAfterPassedPatientsCount,
            cancellationToken);
        var reservationRepository = unitOfWork.WriteRepository<Reservation>();
        foreach (var reservationId in eligibleReservationIds)
        {
            var reservation = await reservationRepository.FirstOrDefaultAsync(
                new ReservationForTicketSpecification(reservationId), cancellationToken);
            if (reservation is null || reservation.Status != ReservationStatus.Active ||
                !reservation.IsLate(nowUtc, configuration.CheckInGracePeriodMinutes) ||
                reservation.MarkNoShow(null, nowUtc).IsFailure)
            {
                continue;
            }

            await projectionOutbox.QueueAsync(new QueueReservationProjectionInvalidation(
                $"reservation-no-show:{reservation.Id:N}:{reservation.History.Last().Id:N}",
                reservation.DoctorPracticeId,
                reservation.DoctorId,
                RefreshAvailability: true,
                RefreshPopularity: true), cancellationToken);
        }

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
