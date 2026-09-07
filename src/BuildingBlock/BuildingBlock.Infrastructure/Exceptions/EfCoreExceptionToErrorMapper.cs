using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Exceptions
{
    internal sealed class EfCoreExceptionToErrorMapper : IExceptionToErrorMapper
    {
        public bool TryMap(Exception exception, out Error error)
        {
            ArgumentNullException.ThrowIfNull(exception);

            error = exception switch
            {
                DbUpdateConcurrencyException => Error.Conflict(
                    ErrorCodes.Persistence.Concurrency,
                    "Concurrency conflict.",
                    source: "EFCore"),
                _ => null!
            };

            return error is not null;
        }
    }
}
