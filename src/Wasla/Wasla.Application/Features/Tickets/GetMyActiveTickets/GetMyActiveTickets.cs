using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Tickets.Common;

namespace Wasla.Application.Features.Tickets.GetMyActiveTickets;

public sealed record GetMyActiveTicketsQuery : IQuery<IReadOnlyList<MyActiveTicketResponse>>;

internal sealed class GetMyActiveTicketsQueryHandler(
    TicketAccessService access,
    ITicketQueueReader queueReader)
    : IQueryHandler<GetMyActiveTicketsQuery, IReadOnlyList<MyActiveTicketResponse>>
{
    public async Task<Result<IReadOnlyList<MyActiveTicketResponse>>> Handle(
        GetMyActiveTicketsQuery request,
        CancellationToken cancellationToken)
    {
        var patientId = await access.ResolveOwnPatientIdAsync(cancellationToken);
        return patientId.IsFailure
            ? Result<IReadOnlyList<MyActiveTicketResponse>>.Fail(patientId.Errors)
            : Result<IReadOnlyList<MyActiveTicketResponse>>.Ok(
                await queueReader.ListPatientActiveAsync(patientId.Value, cancellationToken));
    }
}
