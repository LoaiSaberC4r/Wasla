using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Doctors;

public sealed class DoctorPracticeLocation : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeLocation()
    {
    }

    private DoctorPracticeLocation(
        Guid id,
        Guid doctorId,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
        : base(id)
    {
        DoctorId = doctorId;
        GovernorateId = governorateId;
        CityId = cityId;
        AreaId = areaId;
        DetailedAddress = detailedAddress;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid DoctorId { get; private set; }
    public int GovernorateId { get; private set; }
    public int CityId { get; private set; }
    public int AreaId { get; private set; }
    public string DetailedAddress { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeLocation> Create(
        Guid id,
        Guid doctorId,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
    {
        var validation = Validate(id, doctorId, governorateId, cityId, areaId, detailedAddress, latitude, longitude);
        return validation.IsFailure
            ? Result<DoctorPracticeLocation>.Fail(validation.Errors)
            : Result<DoctorPracticeLocation>.Ok(new DoctorPracticeLocation(
                id,
                doctorId,
                governorateId,
                cityId,
                areaId,
                detailedAddress.Trim(),
                latitude,
                longitude));
    }

    public Result Update(
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
    {
        var validation = Validate(Id, DoctorId, governorateId, cityId, areaId, detailedAddress, latitude, longitude);
        if (validation.IsFailure)
        {
            return validation;
        }

        GovernorateId = governorateId;
        CityId = cityId;
        AreaId = areaId;
        DetailedAddress = detailedAddress.Trim();
        Latitude = latitude;
        Longitude = longitude;
        return Result.Ok();
    }

    private static Result Validate(
        Guid id,
        Guid doctorId,
        int governorateId,
        int cityId,
        int areaId,
        string? detailedAddress,
        decimal latitude,
        decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return Result.Fail(DoctorPracticeLocationErrors.InvalidLatitude);
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Fail(DoctorPracticeLocationErrors.InvalidLongitude);
        }

        var address = detailedAddress?.Trim() ?? string.Empty;
        return id == Guid.Empty || doctorId == Guid.Empty || governorateId <= 0 || cityId <= 0 || areaId <= 0 || address.Length is 0 or > 500
            ? Result.Fail(DoctorPracticeLocationErrors.Invalid)
            : Result.Ok();
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
