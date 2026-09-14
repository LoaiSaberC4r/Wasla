using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Practices;

public static class DoctorPracticeErrors
{
    public static Error Invalid => Validation("DoctorPractice.Invalid", "DoctorPracticeInvalid");
    public static Error NotFound => Error.NotFound("DoctorPractice.NotFound", Text("DoctorPracticeNotFound"));
    public static Error NotOwned => Error.Security("DoctorPractice.NotOwned", Text("DoctorPracticeNotOwned"));
    public static Error InvalidLatitude => Validation("DoctorPractice.InvalidLatitude", "DoctorPracticeInvalidLatitude");
    public static Error InvalidLongitude => Validation("DoctorPractice.InvalidLongitude", "DoctorPracticeInvalidLongitude");
    public static Error ActivationRequirementsNotMet => Validation(
        "DoctorPractice.ActivationRequirementsNotMet",
        "DoctorPracticeActivationRequirementsNotMet");
    public static Error AlreadyActive => Error.Conflict("DoctorPractice.AlreadyActive", Text("DoctorPracticeAlreadyActive"));
    public static Error AlreadyInactive => Error.Conflict("DoctorPractice.AlreadyInactive", Text("DoctorPracticeAlreadyInactive"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPractice.ConcurrencyConflict",
        Text("DoctorPracticeConcurrencyConflict"));

    private static Error Validation(string code, string resource) => Error.Validation(code, Text(resource));
    internal static string Text(string resource) => ErrorMessage.GetString(resource);
}
