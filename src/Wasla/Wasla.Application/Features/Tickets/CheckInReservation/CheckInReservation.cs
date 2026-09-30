using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Payments;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Tickets.CheckInReservation;

public sealed record CheckInReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    decimal PaidAmount,
    string IdempotencyKey,
    PaymentMethod PaymentMethod = PaymentMethod.LegacyUnspecified,
    string? ReferenceNumber = null,
    string? Notes = null)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CheckInReservationCommandValidator
    : AbstractValidator<CheckInReservationCommand>
{
    public CheckInReservationCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.ReservationId).NotEmpty();
        RuleFor(command => command.PaidAmount).GreaterThan(0);
        RuleFor(command => command.PaymentMethod)
            .Must(FinancialPolicy.IsSelectableMethod)
            .WithMessage(_ => ErrorMessage.GetString("PaymentUnsupportedMethod"));
        RuleFor(command => command.ReferenceNumber).MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
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
            cancellationToken,
            request.PaymentMethod,
            request.ReferenceNumber,
            request.Notes);
}
