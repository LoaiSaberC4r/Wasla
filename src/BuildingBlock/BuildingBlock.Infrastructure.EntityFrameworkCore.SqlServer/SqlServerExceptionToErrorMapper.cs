using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer
{
    public sealed class SqlServerExceptionToErrorMapper : IExceptionToErrorMapper
    {
        public bool TryMap(Exception exception, out Error error)
        {
            ArgumentNullException.ThrowIfNull(exception);

            var mapped = exception switch
            {
                DbUpdateException dbUpdateException when TryGetSqlException(dbUpdateException, out var sqlException) =>
                    MapSqlException(sqlException),
                SqlException sqlException => MapSqlException(sqlException),
                _ => null
            };

            if (mapped is null)
            {
                error = null!;
                return false;
            }

            error = mapped;
            return true;
        }

        private static Error? MapSqlException(SqlException exception)
            => exception.Number switch
            {
                2601 or 2627 => Error.Conflict(
                    ErrorCodes.Persistence.UniqueConstraint,
                    "A persistence conflict occurred.",
                    source: "SqlServer"),
                1205 => Error.Infra(
                    ErrorCodes.Persistence.Deadlock,
                    "A transient persistence failure occurred.",
                    source: "SqlServer",
                    retryAfter: TimeSpan.FromSeconds(1)),
                -2 => Error.Infra(
                    ErrorCodes.Persistence.Timeout,
                    "The persistence operation timed out.",
                    source: "SqlServer",
                    retryAfter: TimeSpan.FromSeconds(1)),
                4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001 => Error.Infra(
                    ErrorCodes.Persistence.Unavailable,
                    "The persistence provider is unavailable.",
                    source: "SqlServer",
                    retryAfter: TimeSpan.FromSeconds(5)),
                _ => null
            };

        private static bool TryGetSqlException(Exception exception, out SqlException sqlException)
        {
            var current = exception.InnerException;
            while (current is not null)
            {
                if (current is SqlException typed)
                {
                    sqlException = typed;
                    return true;
                }

                current = current.InnerException;
            }

            sqlException = null!;
            return false;
        }
    }
}
