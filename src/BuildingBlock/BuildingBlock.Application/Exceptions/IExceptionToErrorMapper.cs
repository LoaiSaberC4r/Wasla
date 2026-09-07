using BuildingBlock.Domain.Results;

namespace BuildingBlock.Application.Exceptions
{
    public interface IExceptionToErrorMapper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "The parameter name is part of an established public interface contract.")]
        bool TryMap(Exception exception, out Error error);
    }
}
