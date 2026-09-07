using BuildingBlock.Application.Abstraction.Results;
using BuildingBlock.Application.Exceptions;
using MediatR;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class ExceptionMappingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IReadOnlyCollection<IExceptionToErrorMapper> _mappers;

        public ExceptionMappingBehavior(IEnumerable<IExceptionToErrorMapper> mappers)
        {
            _mappers = mappers.ToArray();
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                foreach (var mapper in _mappers)
                {
                    if (mapper.TryMap(exception, out var error))
                    {
                        return (TResponse)ResultFailureFactory.Create(typeof(TResponse), error);
                    }
                }

                throw;
            }
        }
    }
}
