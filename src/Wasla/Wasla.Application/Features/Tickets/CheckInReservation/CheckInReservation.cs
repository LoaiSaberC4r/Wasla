using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;

namespace Wasla.Application.Features.Tickets.CheckInReservation;

public sealed record CheckInReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    decimal PaidAmount,
    string IdempotencyKey)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CheckInReservationCommandValidator
    : AbstractValidator<CheckInReservationCommand>
{
    public CheckInReservationCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.ReservationId).NotEmpty();
        RuleFor(command => command.PaidAmount).GreaterThan(0);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CheckInReservationCommandHandler(ReservationCheckInWorkflow workflow)
    : ICommandHandler<CheckInReservationCommand, TicketDetailsResponse>
{
    public Task<Result<TicketDetailsResponse>> Handle(
        CheckInReservationCommand request,
        CancellationToken cancellationToken)
        => workflow.ExecuteAsync(
            request.PracticeId,
            request.ReservationId,
            request.PaidAmount,
            force: false,
            reason: null,
            request.IdempotencyKey,
            cancellationToken);
}
