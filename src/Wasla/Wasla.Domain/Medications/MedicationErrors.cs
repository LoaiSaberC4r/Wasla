using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Medications;

public static class MedicationErrors
{
    public static Error Validation(string code, string? source = null)
        => Error.Validation(code, ErrorMessage.GetString(code), source: source);
    public static Error Conflict(string code) => Error.Conflict(code, ErrorMessage.GetString(code));
    public static Error NotFound(string code) => Error.NotFound(code, ErrorMessage.GetString(code));
    public static Error AccessDenied(string code) => Error.Security(code, ErrorMessage.GetString(code));
}
