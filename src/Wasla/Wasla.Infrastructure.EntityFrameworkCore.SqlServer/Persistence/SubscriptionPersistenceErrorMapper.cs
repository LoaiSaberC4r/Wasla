using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class SubscriptionPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is DbUpdateConcurrencyException concurrency && concurrency.Entries.Any(e => e.Entity.GetType().Namespace == typeof(DoctorSubscription).Namespace))
        { error = SubscriptionErrors.Conflict(); return true; }
        var inner = exception;
        while (inner.InnerException is not null && inner is not SqlException) inner = inner.InnerException;
        if (inner is SqlException sql && (sql.Number == 51060 || sql.Number is 2601 or 2627 && exception is DbUpdateException update && update.Entries.Any(e => e.Entity.GetType().Namespace == typeof(DoctorSubscription).Namespace)))
        {
            var code = sql.Message.Contains("UX_OneTrialPerDoctor", StringComparison.Ordinal) ? "TrialAlreadyUsed" :
                sql.Message.Contains("IX_SubscriptionPayments_IdempotencyKey", StringComparison.Ordinal) ? "PaymentAlreadyRecorded" : "ConcurrencyConflict";
            error = SubscriptionErrors.Conflict(code); return true;
        }
        return false;
    }
}
