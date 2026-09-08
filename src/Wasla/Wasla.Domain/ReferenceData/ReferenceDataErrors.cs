using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.ReferenceData;

public static class MedicalSpecializationErrors
{
    public static Error Invalid => Error.Validation("MedicalSpecialization.Invalid", ErrorMessage.MedicalSpecializationInvalid);
    public static Error NotFound => Error.NotFound("MedicalSpecialization.NotFound", ErrorMessage.MedicalSpecializationNotFound);
    public static Error DuplicateNameAr => Error.Conflict("MedicalSpecialization.DuplicateNameAr", ErrorMessage.MedicalSpecializationDuplicateNameAr);
    public static Error DuplicateNameEn => Error.Conflict("MedicalSpecialization.DuplicateNameEn", ErrorMessage.MedicalSpecializationDuplicateNameEn);
    public static Error AlreadyActive => Error.Conflict("MedicalSpecialization.AlreadyActive", ErrorMessage.MedicalSpecializationAlreadyActive);
    public static Error AlreadyInactive => Error.Conflict("MedicalSpecialization.AlreadyInactive", ErrorMessage.MedicalSpecializationAlreadyInactive);
    public static Error MustDeactivateBeforeDelete => Error.Conflict("MedicalSpecialization.MustDeactivateBeforeDelete", ErrorMessage.MedicalSpecializationMustDeactivateBeforeDelete);
    public static Error Deleted => Error.Conflict("MedicalSpecialization.Deleted", ErrorMessage.MedicalSpecializationDeleted);
    public static Error NotDeleted => Error.Conflict("MedicalSpecialization.NotDeleted", ErrorMessage.MedicalSpecializationNotDeleted);
    public static Error ConcurrencyConflict => Error.Conflict("MedicalSpecialization.ConcurrencyConflict", ErrorMessage.MedicalSpecializationConcurrencyConflict);
}

public static class LocationErrors
{
    public static Error Invalid => Error.Validation("Location.Invalid", ErrorMessage.LocationInvalid);
    public static Error GovernorateNotFound => Error.NotFound("Location.GovernorateNotFound", ErrorMessage.LocationGovernorateNotFound);
    public static Error CityNotFound => Error.NotFound("Location.CityNotFound", ErrorMessage.LocationCityNotFound);
    public static Error AreaNotFound => Error.NotFound("Location.AreaNotFound", ErrorMessage.LocationAreaNotFound);
    public static Error InvalidHierarchy => Error.Validation("Location.InvalidHierarchy", ErrorMessage.LocationInvalidHierarchy);
    public static Error Inactive => Error.Validation("Location.Inactive", ErrorMessage.LocationInactive);
}
