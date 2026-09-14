using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;
using Wasla.Domain.Practices;

namespace Wasla.Domain.Doctors;

// Compatibility factory for the retired one-location onboarding contract. New code must use DoctorPractice.
public static class DoctorPracticeLocation
{
    public static Result<DoctorPractice> Create(
        Guid id,
        Guid doctorId,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return Result<DoctorPractice>.Fail(DoctorPracticeLocationErrors.InvalidLatitude);
        }

        if (longitude is < -180 or > 180)
        {
            return Result<DoctorPractice>.Fail(DoctorPracticeLocationErrors.InvalidLongitude);
        }

        var practice = DoctorPractice.Create(
            id,
            doctorId,
            "العيادة الرئيسية",
            null,
            governorateId,
            cityId,
            areaId,
            detailedAddress,
            latitude,
            longitude,
            isLegacyOnboarding: true);
        return practice.IsFailure
            ? Result<DoctorPractice>.Fail(DoctorPracticeLocationErrors.Invalid)
            : practice;
    }
}

public static class DoctorPracticeLocationErrors
{
    public static Error Invalid => Error.Validation("DoctorPracticeLocation.Invalid", ErrorMessage.DoctorPracticeLocationInvalid);
    public static Error NotFound => Error.NotFound("DoctorPracticeLocation.NotFound", ErrorMessage.DoctorPracticeLocationNotFound);
    public static Error InvalidLatitude => Error.Validation("DoctorPracticeLocation.InvalidLatitude", ErrorMessage.DoctorPracticeLocationInvalidLatitude);
    public static Error InvalidLongitude => Error.Validation("DoctorPracticeLocation.InvalidLongitude", ErrorMessage.DoctorPracticeLocationInvalidLongitude);
    public static Error RowVersionRequired => Error.Validation("DoctorPracticeLocation.RowVersionRequired", ErrorMessage.DoctorPracticeLocationRowVersionRequired);
    public static Error RowVersionMustBeNull => Error.Validation("DoctorPracticeLocation.RowVersionMustBeNull", ErrorMessage.DoctorPracticeLocationRowVersionMustBeNull);
    public static Error ConcurrencyConflict => Error.Conflict("DoctorPracticeLocation.ConcurrencyConflict", ErrorMessage.DoctorPracticeLocationConcurrencyConflict);
}
