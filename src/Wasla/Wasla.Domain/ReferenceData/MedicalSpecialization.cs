using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.ReferenceData;

public sealed class MedicalSpecialization : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 1000;

    private MedicalSpecialization()
    {
    }

    private MedicalSpecialization(
        Guid id,
        string nameAr,
        string? nameEn,
        string? descriptionAr,
        string? descriptionEn,
        int sortOrder,
        Guid createdByApplicationUserId)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        SortOrder = sortOrder;
        IsActive = true;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public Guid? DeletedByApplicationUserId { get; private set; }
    public Guid? RestoredByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<MedicalSpecialization> Create(
        Guid id,
        string nameAr,
        string? nameEn,
        string? descriptionAr,
        string? descriptionEn,
        int sortOrder,
        Guid createdByApplicationUserId)
    {
        var normalized = Normalize(nameAr, nameEn, descriptionAr, descriptionEn, sortOrder);
        if (id == Guid.Empty || createdByApplicationUserId == Guid.Empty || normalized.IsFailure)
        {
            return Result<MedicalSpecialization>.Fail(MedicalSpecializationErrors.Invalid);
        }

        var value = normalized.Value;
        return Result<MedicalSpecialization>.Ok(new MedicalSpecialization(
            id,
            value.NameAr,
            value.NameEn,
            value.DescriptionAr,
            value.DescriptionEn,
            sortOrder,
            createdByApplicationUserId));
    }

    public Result Update(
        string nameAr,
        string? nameEn,
        string? descriptionAr,
        string? descriptionEn,
        int sortOrder,
        Guid actorId)
    {
        if (IsDeleted)
        {
            return Result.Fail(MedicalSpecializationErrors.Deleted);
        }

        var normalized = Normalize(nameAr, nameEn, descriptionAr, descriptionEn, sortOrder);
        if (actorId == Guid.Empty || normalized.IsFailure)
        {
            return Result.Fail(MedicalSpecializationErrors.Invalid);
        }

        var value = normalized.Value;
        NameAr = value.NameAr;
        NameEn = value.NameEn;
        DescriptionAr = value.DescriptionAr;
        DescriptionEn = value.DescriptionEn;
        SortOrder = sortOrder;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Activate(Guid actorId)
    {
        if (IsDeleted)
        {
            return Result.Fail(MedicalSpecializationErrors.Deleted);
        }

        if (IsActive)
        {
            return Result.Fail(MedicalSpecializationErrors.AlreadyActive);
        }

        IsActive = true;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Deactivate(Guid actorId)
    {
        if (IsDeleted)
        {
            return Result.Fail(MedicalSpecializationErrors.Deleted);
        }

        if (!IsActive)
        {
            return Result.Fail(MedicalSpecializationErrors.AlreadyInactive);
        }

        IsActive = false;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result SoftDelete(Guid actorId)
    {
        if (IsDeleted)
        {
            return Result.Fail(MedicalSpecializationErrors.Deleted);
        }

        if (IsActive)
        {
            return Result.Fail(MedicalSpecializationErrors.MustDeactivateBeforeDelete);
        }

        IsDeleted = true;
        DeletedByApplicationUserId = actorId;
        RestoredByApplicationUserId = null;
        return Result.Ok();
    }

    public Result Restore(Guid actorId)
    {
        if (!IsDeleted)
        {
            return Result.Fail(MedicalSpecializationErrors.NotDeleted);
        }

        IsDeleted = false;
        IsActive = false;
        RestoredByApplicationUserId = actorId;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static Result<NormalizedValues> Normalize(
        string? nameAr,
        string? nameEn,
        string? descriptionAr,
        string? descriptionEn,
        int sortOrder)
    {
        var normalizedNameAr = nameAr?.Trim() ?? string.Empty;
        var normalizedNameEn = NormalizeOptional(nameEn);
        var normalizedDescriptionAr = NormalizeOptional(descriptionAr);
        var normalizedDescriptionEn = NormalizeOptional(descriptionEn);
        if (normalizedNameAr.Length is 0 or > MaximumNameLength ||
            normalizedNameEn?.Length > MaximumNameLength ||
            normalizedDescriptionAr?.Length > MaximumDescriptionLength ||
            normalizedDescriptionEn?.Length > MaximumDescriptionLength ||
            sortOrder < 0)
        {
            return Result<NormalizedValues>.Fail(MedicalSpecializationErrors.Invalid);
        }

        return Result<NormalizedValues>.Ok(new NormalizedValues(
            normalizedNameAr,
            normalizedNameEn,
            normalizedDescriptionAr,
            normalizedDescriptionEn));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedValues(
        string NameAr,
        string? NameEn,
        string? DescriptionAr,
        string? DescriptionEn);
}
