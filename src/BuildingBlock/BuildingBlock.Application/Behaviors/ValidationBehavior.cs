using BuildingBlock.Application.Abstraction.Results;
using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Domain.Results;
using FluentValidation;
using MediatR;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
        where TResponse : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var validators = _validators.ToArray();
            if (validators.Length == 0)
            {
                return await next(cancellationToken);
            }

            var errors = new List<Error>();
            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(request, cancellationToken);
                if (!result.IsValid)
                {
                    errors.AddRange(result.Errors.Select(failure =>
                        Error.Validation(
                            code: $"Validation.{(string.IsNullOrWhiteSpace(failure.ErrorCode) ? failure.PropertyName : failure.ErrorCode)}",
                            message: failure.ErrorMessage,
                            details: $"field:{failure.PropertyName}",
                            source: "FluentValidation")));
                }
            }

            if (errors.Count == 0)
            {
                return await next(cancellationToken);
            }

            BuildingBlockDiagnostics.RecordValidationFailures(
                BuildingBlockDiagnostics.GetRequestKind(typeof(TRequest)),
                errors.Count);

            var responseType = typeof(TResponse);
            if (responseType == typeof(Result) ||
                responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                return (TResponse)ResultFailureFactory.Create(responseType, errors);
            }

            throw new ValidationException(errors.Select(error => new FluentValidation.Results.ValidationFailure
            {
                ErrorCode = error.Code,
                ErrorMessage = error.Message
            }));
        }
    }
}
