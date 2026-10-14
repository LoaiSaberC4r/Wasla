using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Payments;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class Phase18PersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is DbUpdateConcurrencyException concurrency && concurrency.Entries.Any(e => Phase18(e.Entity)))
        { error = ReportingErrors.Conflict; return true; }
        var nested = exception;
        while (nested.InnerException != null && nested is not SqlException) nested = nested.InnerException;
        if (nested is SqlException sql)
        {
            if (sql.Number == 51018) { error = Error.Conflict("Phase18.PersistenceInvariantConflict", Wasla.Domain.Resources.ErrorMessage.GetString("Phase18Conflict")); return true; }
            if (sql.Number == 51019) { error = Error.Infra("Report.PolicyCoordinationBusy", Wasla.Domain.Resources.ErrorMessage.GetString("Phase18Conflict")); return true; }
            if (sql.Number is 2601 or 2627 && exception is DbUpdateException update && update.Entries.Any(e => Phase18(e.Entity) ||
                e.Entity is FinancialIdempotencyRecord record && record.Operation.StartsWith("P18.", StringComparison.Ordinal)))
            { error = ReportingErrors.Conflict; return true; }
        }
        return false;
    }
    private static bool Phase18(object entity) => entity.GetType().Namespace is "Wasla.Domain.OperatingFinance" or "Wasla.Domain.Operations" or "Wasla.Domain.Reporting";
}
