using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.GetMyTicketDetails;

public sealed record GetMyTicketDetailsQuery(Guid TicketId) : IQuery<TicketDetailsResponse>;

internal sealed class GetMyTicketDetailsQueryValidator : AbstractValidator<GetMyTicketDetailsQuery>
{
    public GetMyTicketDetailsQueryValidator()
        => RuleFor(query => query.TicketId).NotEmpty();
}

internal sealed class GetMyTicketDetailsQueryHandler(
    TicketAccessService access,
    ITicketQueueReader queueReader)
    : IQueryHandler<GetMyTicketDetailsQuery, TicketDetailsResponse>
{
    public async Task<Result<TicketDetailsResponse>> Handle(
        GetMyTicketDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var patientId = await access.ResolveOwnPatientIdAsync(cancellationToken);
        if (patientId.IsFailure)
        {
            return Result<TicketDetailsResponse>.Fail(patientId.Errors);
        }

        var details = await queueReader.GetDetailsAsync(request.TicketId, cancellationToken);
        return details is null || details.PatientId != patientId.Value
            ? Result<TicketDetailsResponse>.Fail(TicketErrors.NotFound)
            : Result<TicketDetailsResponse>.Ok(details);
    }
}
