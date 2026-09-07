using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Doctors;

public static class DoctorErrors
{
    public static Error NotFound => Error.NotFound("Doctor.NotFound", ErrorMessage.DoctorNotFound);
    public static Error InvalidStatus => Error.Conflict("Doctor.InvalidStatus", ErrorMessage.DoctorInvalidStatus);
    public static Error NationalIdRequired => Error.Validation("Doctor.NationalIdRequired", ErrorMessage.DoctorNationalIdRequired);
    public static Error NationalIdAlreadyExists => Error.Conflict("Doctor.NationalIdAlreadyExists", ErrorMessage.DoctorNationalIdAlreadyExists);
    public static Error MediaNotFound => Error.NotFound("Doctor.MediaNotFound", ErrorMessage.DoctorMediaNotFound);
    public static Error ReasonRequired => Error.Validation("Doctor.ReasonRequired", ErrorMessage.ReasonRequired);
    public static Error ConcurrencyConflict => Error.Conflict("Doctor.ConcurrencyConflict", ErrorMessage.InvalidRowVersion);
}

