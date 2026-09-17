using System.Security.Cryptography;
using System.Text;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.Common;

internal sealed record TicketIdempotencyState(
    TicketIdempotencyRecord? Record,
    Guid? ExistingTicketId);

internal static class TicketIdempotency
{
    public static Result<string> ValidateKey(string? key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        return normalized.Length is < 1 or > 200
            ? Result<string>.Fail(TicketErrors.IdempotencyKeyRequired)
            : Result<string>.Ok(normalized);
    }

    public static string Fingerprint(params object?[] values)
    {
        var payload = string.Join('|', values.Select(value => value?.ToString() ?? "<null>"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public static async Task<Result<TicketIdempotencyState>> BeginAsync(
        IWriteRepository<TicketIdempotencyRecord, WaslaWritePersistence> repository,
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        string fingerprint,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByPropertyAsync(
            item => item.ActorApplicationUserId == actorApplicationUserId &&
                    item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Result<TicketIdempotencyState>.Ok(new(existing, existing.TicketId))
                : Result<TicketIdempotencyState>.Fail(TicketErrors.IdempotencyKeyReused);
        }

        var record = new TicketIdempotencyRecord(
            Guid.NewGuid(), actorApplicationUserId, operation,
            idempotencyKey, fingerprint, nowUtc);
        await repository.AddAsync(record, cancellationToken);
        return Result<TicketIdempotencyState>.Ok(new(record, null));
    }
}
