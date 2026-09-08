using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.ReferenceData;

public sealed class Governorate : AggregateRoot<int>, IAuditableEntity
{
    private Governorate()
    {
    }

    private Governorate(int id, string nameAr, string? nameEn, int displayOrder) : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Governorate> Create(int id, string nameAr, string? nameEn, int displayOrder)
    {
        var names = LocationReferenceDataValidation.Normalize(id, nameAr, nameEn, displayOrder);
        return names.IsFailure
            ? Result<Governorate>.Fail(names.Errors)
            : Result<Governorate>.Ok(new Governorate(id, names.Value.NameAr, names.Value.NameEn, displayOrder));
    }

    public void Deactivate() => IsActive = false;
}

public sealed class City : AggregateRoot<int>, IAuditableEntity
{
    private City()
    {
    }

    private City(int id, int governorateId, string nameAr, string? nameEn, int displayOrder) : base(id)
    {
        GovernorateId = governorateId;
        NameAr = nameAr;
        NameEn = nameEn;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public int GovernorateId { get; private set; }
    public Governorate Governorate { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<City> Create(int id, int governorateId, string nameAr, string? nameEn, int displayOrder)
    {
        var names = LocationReferenceDataValidation.Normalize(id, nameAr, nameEn, displayOrder);
        return governorateId <= 0 || names.IsFailure
            ? Result<City>.Fail(LocationErrors.Invalid)
            : Result<City>.Ok(new City(id, governorateId, names.Value.NameAr, names.Value.NameEn, displayOrder));
    }

    public void Deactivate() => IsActive = false;
}

public sealed class Area : AggregateRoot<int>, IAuditableEntity
{
    private Area()
    {
    }

    private Area(int id, int cityId, string nameAr, string? nameEn, int displayOrder) : base(id)
    {
        CityId = cityId;
        NameAr = nameAr;
        NameEn = nameEn;
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public int CityId { get; private set; }
    public City City { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Area> Create(int id, int cityId, string nameAr, string? nameEn, int displayOrder)
    {
        var names = LocationReferenceDataValidation.Normalize(id, nameAr, nameEn, displayOrder);
        return cityId <= 0 || names.IsFailure
            ? Result<Area>.Fail(LocationErrors.Invalid)
            : Result<Area>.Ok(new Area(id, cityId, names.Value.NameAr, names.Value.NameEn, displayOrder));
    }

    public void Deactivate() => IsActive = false;
}

internal static class LocationReferenceDataValidation
{
    public static Result<LocationNames> Normalize(int id, string? nameAr, string? nameEn, int displayOrder)
    {
        var ar = nameAr?.Trim() ?? string.Empty;
        var en = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return id <= 0 || displayOrder < 0 || ar.Length is 0 or > 150 || en?.Length > 150
            ? Result<LocationNames>.Fail(LocationErrors.Invalid)
            : Result<LocationNames>.Ok(new LocationNames(ar, en));
    }
}

internal sealed record LocationNames(string NameAr, string? NameEn);
