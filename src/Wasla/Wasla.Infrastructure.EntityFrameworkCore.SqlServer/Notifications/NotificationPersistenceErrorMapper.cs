using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal sealed class NotificationPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        var conflict = exception is DbUpdateConcurrencyException c && c.Entries.Any(e => e.Entity.GetType().Namespace == "Wasla.Domain.Notifications");
        var nested = exception;
        while (nested.InnerException is not null && nested is not SqlException) nested = nested.InnerException;
        conflict |= nested is SqlException { Number: 51017 } || nested is SqlException { Number: 2601 or 2627 } && exception is DbUpdateException d && d.Entries.Any(e => e.Entity.GetType().Namespace == "Wasla.Domain.Notifications");
        if (!conflict) return false;
        error = Wasla.Domain.Notifications.NotificationErrors.Conflict; return true;
    }
}
