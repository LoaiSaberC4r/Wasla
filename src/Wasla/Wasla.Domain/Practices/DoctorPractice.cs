using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public sealed class DoctorPractice : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPractice()
    {
    }

    private DoctorPractice(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude,
        Guid? actorId,
        bool isLegacyOnboarding)
        : base(id)
    {
        DoctorId = doctorId;
        NameAr = nameAr;
        NameEn = nameEn;
        GovernorateId = governorateId;
        CityId = cityId;
        AreaId = areaId;
        DetailedAddress = detailedAddress;
        Latitude = latitude;
        Longitude = longitude;
        CreatedByApplicationUserId = actorId;
        IsLegacyOnboarding = isLegacyOnboarding;
        IsActive = false;
    }

    public Guid DoctorId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public int GovernorateId { get; private set; }
    public int CityId { get; private set; }
    public int AreaId { get; private set; }
    public string DetailedAddress { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLegacyOnboarding { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPractice> Create(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude,
        Guid? actorId = null,
        bool isLegacyOnboarding = false)
    {
        var normalizedNameAr = nameAr?.Trim() ?? string.Empty;
        var normalizedNameEn = NormalizeOptional(nameEn);
        var normalizedAddress = detailedAddress?.Trim() ?? string.Empty;
        var validation = Validate(
            id,
            doctorId,
            normalizedNameAr,
            normalizedNameEn,
            governorateId,
            cityId,
            areaId,
            normalizedAddress,
            latitude,
            longitude);
        return validation.IsFailure
            ? Result<DoctorPractice>.Fail(validation.Errors)
            : Result<DoctorPractice>.Ok(new DoctorPractice(
                id,
                doctorId,
                normalizedNameAr,
                normalizedNameEn,
                governorateId,
                cityId,
                areaId,
                normalizedAddress,
                latitude,
                longitude,
                actorId,
                isLegacyOnboarding));
    }

    public Result Update(
        string nameAr,
        string? nameEn,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude,
        Guid? actorId)
    {
        var normalizedNameAr = nameAr?.Trim() ?? string.Empty;
        var normalizedNameEn = NormalizeOptional(nameEn);
        var normalizedAddress = detailedAddress?.Trim() ?? string.Empty;
        var validation = Validate(
            Id,
            DoctorId,
            normalizedNameAr,
            normalizedNameEn,
            governorateId,
            cityId,
            areaId,
            normalizedAddress,
            latitude,
            longitude);
        if (validation.IsFailure)
        {
            return validation;
        }

        NameAr = normalizedNameAr;
        NameEn = normalizedNameEn;
        GovernorateId = governorateId;
        CityId = cityId;
        AreaId = areaId;
        DetailedAddress = normalizedAddress;
        Latitude = latitude;
        Longitude = longitude;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Update(
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
        => Update(
            NameAr,
            NameEn,
            governorateId,
            cityId,
            areaId,
            detailedAddress,
            latitude,
            longitude,
            ModifiedByApplicationUserId);

    public Result Activate(bool hasConfiguration, bool hasBranding, bool hasLogo, Guid? actorId = null)
    {
        if (IsActive)
        {
            return Result.Fail(DoctorPracticeErrors.AlreadyActive);
        }

        if (!hasConfiguration || !hasBranding || !hasLogo)
        {
            return Result.Fail(DoctorPracticeErrors.ActivationRequirementsNotMet);
        }

        IsActive = true;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Deactivate(Guid? actorId = null)
    {
        if (!IsActive)
        {
            return Result.Fail(DoctorPracticeErrors.AlreadyInactive);
        }

        IsActive = false;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static Result Validate(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        decimal latitude,
        decimal longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return Result.Fail(DoctorPracticeErrors.InvalidLatitude);
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Fail(DoctorPracticeErrors.InvalidLongitude);
        }

        return id == Guid.Empty || doctorId == Guid.Empty ||
               nameAr.Length is 0 or > 200 || nameEn?.Length > 200 ||
               governorateId <= 0 || cityId <= 0 || areaId <= 0 ||
               detailedAddress.Length is 0 or > 500
            ? Result.Fail(DoctorPracticeErrors.Invalid)
            : Result.Ok();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

