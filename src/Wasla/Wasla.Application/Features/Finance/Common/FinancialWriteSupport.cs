using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using Wasla.Application.Persistence;
using Wasla.Domain.Payments;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Finance.Common;

internal sealed class PaymentForFinancialWriteSpecification : Specification<Payment>
{
    public PaymentForFinancialWriteSpecification(Guid paymentId)
    {
        AddCriteria(payment => payment.Id == paymentId);
        AddInclude(payment => payment.Refund);
        AddInclude(payment => payment.CorrectionHistory);
        UseTracking();
    }
}

internal sealed class RefundForFinancialWriteSpecification : Specification<Refund>
{
    public RefundForFinancialWriteSpecification(Guid refundId)
    {
        AddCriteria(refund => refund.Id == refundId);
        AddInclude(refund => refund.CorrectionHistory);
        UseTracking();
    }
}

internal static class FinancialWriteIdempotency
{
    public static string Fingerprint(params object?[] values)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));

    public static async Task<Result<FinancialIdempotencyRecord>> BeginAsync(
        IWriteRepository<FinancialIdempotencyRecord, WaslaWritePersistence> repository,
        Guid actorId,
        string operation,
        string key,
        string fingerprint,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
        {
            return Result<FinancialIdempotencyRecord>.Fail(FinancialWriteErrors.IdempotencyKeyRequired);
        }

        var normalizedKey = key.Trim();
        var existing = await repository.GetByPropertyAsync(record =>
            record.ActorApplicationUserId == actorId &&
            record.Operation == operation &&
            record.IdempotencyKey == normalizedKey,
            cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Result<FinancialIdempotencyRecord>.Ok(existing)
                : Result<FinancialIdempotencyRecord>.Fail(FinancialWriteErrors.IdempotencyKeyReused);
        }

        var record = new FinancialIdempotencyRecord(
            Guid.NewGuid(), actorId, operation, normalizedKey, fingerprint, nowUtc);
        await repository.AddAsync(record, cancellationToken);
        return Result<FinancialIdempotencyRecord>.Ok(record);
    }
}

internal static class FinancialWriteErrors
{
    public static Error NotFound => Error.NotFound("Finance.NotFound", ErrorMessage.GetString("FinanceNotFound"));
    public static Error AccessDenied => Error.Security("Finance.AccessDenied", ErrorMessage.GetString("FinanceAccessDenied"));
    public static Error IdempotencyKeyRequired => Error.Validation("Finance.IdempotencyKeyRequired", ErrorMessage.GetString("FinanceIdempotencyKeyRequired"));
    public static Error IdempotencyKeyReused => Error.Conflict("Finance.IdempotencyKeyReused", ErrorMessage.GetString("FinanceIdempotencyKeyReused"));
    public static Error ConcurrentModification => Error.Conflict("Finance.ConcurrentModification", ErrorMessage.GetString("FinanceConcurrentModification"));
    public static Error TicketMustBeCancelled => Error.Conflict("Finance.TicketMustBeCancelled", ErrorMessage.GetString("FinanceTicketMustBeCancelled"));
    public static Error ServiceAlreadyStarted => Error.Conflict("Finance.ServiceAlreadyStarted", ErrorMessage.GetString("FinanceServiceAlreadyStarted"));
    public static Error AlreadyRefunded => Error.Conflict("Finance.AlreadyRefunded", ErrorMessage.GetString("FinanceAlreadyRefunded"));
    public static Error InvalidPayment => Error.Conflict("Finance.InvalidPayment", ErrorMessage.GetString("FinanceInvalidPayment"));
    public static Error ReplayUnavailable => Error.Conflict("Finance.ReplayUnavailable", ErrorMessage.GetString("FinanceReplayUnavailable"));
}
