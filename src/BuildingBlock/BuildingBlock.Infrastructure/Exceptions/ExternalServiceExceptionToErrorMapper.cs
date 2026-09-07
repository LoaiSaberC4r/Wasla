using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;

namespace BuildingBlock.Infrastructure.Exceptions
{
    internal sealed class ExternalServiceExceptionToErrorMapper : IExceptionToErrorMapper
    {
        public bool TryMap(Exception exception, out Error error)
        {
            if (exception is ExternalServiceException external)
            {
                error = external.Error;
                return true;
            }

            error = null!;
            return false;
        }
    }
}
