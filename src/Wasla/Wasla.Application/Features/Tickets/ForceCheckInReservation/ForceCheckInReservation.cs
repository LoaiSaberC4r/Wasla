using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Payments;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Tickets.ForceCheckInReservation;

public sealed record ForceCheckInReservationCommand(
    Guid PracticeId,
    Guid ReservationId,
    decimal PaidAmount,
    string Reason,
    string IdempotencyKey,
    PaymentMethod PaymentMethod = PaymentMethod.LegacyUnspecified,
    string? ReferenceNumber = null,
    string? Notes = null)
    : ICommand<TicketDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class ForceCheckInReservationCommandValidator
    : AbstractValidator<ForceCheckInReservationCommand>
{
    public ForceCheckInReservationCommandValidator()
    {
        RuleFor(command => command.PracticeId).NotEmpty();
        RuleFor(command => command.ReservationId).NotEmpty();
        RuleFor(command => command.PaidAmount).GreaterThan(0);
        RuleFor(command => command.PaymentMethod)
            .Must(FinancialPolicy.IsSelectableMethod)
            .WithMessage(_ => ErrorMessage.GetString("PaymentUnsupportedMethod"));
        RuleFor(command => command.ReferenceNumber).MaximumLength(200);
        RuleFor(command => command.Notes).MaximumLength(1000);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class ForceCheckInReservationCommandHandler(ReservationCheckInWorkflow workflow)
    : ICommandHandler<ForceCheckInReservationCommand, TicketDetailsResponse>
{
    public Task<Result<TicketDetailsResponse>> Handle(
        ForceCheckInReservationCommand request,
        CancellationToken cancellationToken)
        => workflow.ExecuteAsync(
            request.PracticeId,
            request.ReservationId,
            request.PaidAmount,
            force: true,
            request.Reason,
            request.IdempotencyKey,
            cancellationToken,
            request.PaymentMethod,
            request.ReferenceNumber,
            request.Notes);
}
