using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.GetTicketDetails;

public sealed record GetTicketDetailsQuery(Guid PracticeId, Guid TicketId)
    : IQuery<TicketDetailsResponse>;

internal sealed class GetTicketDetailsQueryValidator : AbstractValidator<GetTicketDetailsQuery>
{
    public GetTicketDetailsQueryValidator()
    {
        RuleFor(query => query.PracticeId).NotEmpty();
        RuleFor(query => query.TicketId).NotEmpty();
    }
}

internal sealed class GetTicketDetailsQueryHandler(
    TicketAccessService access,
    ITicketQueueReader queueReader)
    : IQueryHandler<GetTicketDetailsQuery, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        GetTicketDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await access.AuthorizeDoctorOrReceptionAsync(
            request.PracticeId,
            PermissionNames.DoctorPracticeTicketsViewOwn,
            PermissionNames.PracticeTicketsView,
            cancellationToken);
        if (actor.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(actor.Errors);
        }

        var details = await queueReader.GetDetailsAsync(request.TicketId, cancellationToken);
        if (details is null || details.DoctorPracticeId != request.PracticeId)
        {
            return Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound);
        }

        if (details.RefundableAmount > 0 &&
            (await access.AuthorizeDoctorOrReceptionAsync(
                request.PracticeId,
                PermissionNames.DoctorPracticePaymentsRefundOwn,
                PermissionNames.PracticePaymentsRefund,
                cancellationToken)).IsSuccess)
        {
            details = details with { CanRefund = true };
        }

        return Result<TicketDetailsResponse>.Ok(details);
    }
}
