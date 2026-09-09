using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Patients;

public static class PatientErrors
{
    public static Error Invalid => Error.Validation("Patient.Invalid", ErrorMessage.PatientInvalid);
    public static Error NotFound => Error.NotFound("Patient.NotFound", ErrorMessage.PatientNotFound);
    public static Error AccountLinkNotFound => Error.NotFound("Patient.AccountLinkNotFound", ErrorMessage.PatientAccountLinkNotFound);
    public static Error InvalidAccountLink => Error.Validation("Patient.AccountLinkInvalid", ErrorMessage.PatientAccountLinkInvalid);
    public static Error ContactRequired => Error.Validation("Patient.ContactRequired", ErrorMessage.PatientContactRequired);
    public static Error ContactNotFound => Error.NotFound("Patient.ContactNotFound", ErrorMessage.PatientContactNotFound);
    public static Error LastContactRequired => Error.Conflict("Patient.LastContactRequired", ErrorMessage.PatientLastContactRequired);
    public static Error InvalidContact => Error.Validation("Patient.ContactInvalid", ErrorMessage.PatientContactInvalid);
    public static Error AccessForbidden => Error.Security("Patient.AccessForbidden", ErrorMessage.AccessForbidden);
    public static Error ConcurrencyConflict => Error.Conflict("Patient.ConcurrencyConflict", ErrorMessage.InvalidRowVersion);
    public static Error MediaNotFound => Error.NotFound("Patient.MediaNotFound", ErrorMessage.PatientMediaNotFound);
}
