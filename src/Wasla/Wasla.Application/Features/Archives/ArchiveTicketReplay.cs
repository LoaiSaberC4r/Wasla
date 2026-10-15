using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using MediatR;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.GetTicketDetails;
using Wasla.Application.Persistence;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Archives;

// This only resolves a completed, actor-bound request. It never writes/unarchives
// a Ticket, and the ordinary unified read rechecks current archive scope and rights.
internal sealed class ArchiveTicketReplay(IUnitOfWork<WaslaWritePersistence> unitOfWork,
    IArchiveLocationResolver location, ISender sender)
{
    public async Task<Result<TicketDetailsResponse>?> TryAsync(Guid actor, string operation, string key,
        string fingerprint, Guid practiceId, CancellationToken ct)
    {
        var record = await unitOfWork.WriteRepository<TicketIdempotencyRecord>().GetByPropertyAsync(
            x => x.ActorApplicationUserId == actor && x.Operation == operation && x.IdempotencyKey == key.Trim(), ct);
        if (record?.TicketId is not { } ticketId || record.CompletedOnUtc == null) return null;
        if (record.RequestFingerprint != fingerprint) return Result<TicketDetailsResponse>.Fail(TicketErrors.IdempotencyKeyReused);
        if ((await location.TicketAsync(ticketId, ct))?.IsArchived != true) return null;
        return await sender.Send(new GetTicketDetailsQuery(practiceId, ticketId), ct);
    }
}
