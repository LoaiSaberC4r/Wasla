using BuildingBlock.Domain.Results;
using FluentValidation;

namespace BuildingBlock.Application.Exceptions
{
    internal sealed class DefaultExceptionToErrorMapper : IExceptionToErrorMapper
    {
        public bool TryMap(Exception exception, out Error error)
        {
            ArgumentNullException.ThrowIfNull(exception);

            error = exception switch
            {
                ValidationException validationException => Error.Validation(
                    ErrorCodes.Common.Validation,
                    "Validation failed.",
                    details: string.Join(
                        ";",
                        validationException.Errors
                            .Select(failure => failure.PropertyName)
                            .Where(property => !string.IsNullOrWhiteSpace(property))
                            .Distinct(StringComparer.Ordinal)),
                    source: "FluentValidation"),
                _ => null!
            };

            return error is not null;
        }
    }
}
