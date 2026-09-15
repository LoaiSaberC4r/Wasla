using System.Globalization;
using System.Text;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Doctors;

public static class DoctorNameNormalizer
{
    public static string NormalizeArabic(string? value)
        => Normalize(value, arabic: true);

    public static string NormalizeEnglish(string? value)
        => Normalize(value, arabic: false).ToLowerInvariant();

    private static string Normalize(string? value, bool arabic)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (arabic && category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (character == '\u0640')
            {
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(arabic ? character switch
            {
                '\u0623' or '\u0625' or '\u0622' or '\u0671' => '\u0627',
                '\u0649' => '\u064a',
                _ => character
            } : character);
        }

        return result.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}

public sealed class DoctorQualification : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorQualification()
    {
    }

    private DoctorQualification(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int displayOrder,
        Guid actorId)
        : base(id)
    {
        DoctorId = doctorId;
        NameAr = nameAr;
        NameEn = nameEn;
        DisplayOrder = displayOrder;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public int DisplayOrder { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorQualification> Create(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int displayOrder,
        Guid actorId)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return !IsValid(id, doctorId, normalizedAr, normalizedEn, displayOrder, actorId)
            ? Result<DoctorQualification>.Fail(DoctorQualificationErrors.Invalid)
            : Result<DoctorQualification>.Ok(new DoctorQualification(
                id, doctorId, normalizedAr, normalizedEn, displayOrder, actorId));
    }

    public Result Update(
        string nameAr,
        string? nameEn,
        int displayOrder,
        Guid actorId)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (!IsValid(Id, DoctorId, normalizedAr, normalizedEn, displayOrder, actorId))
        {
            return Result.Fail(DoctorQualificationErrors.Invalid);
        }

        NameAr = normalizedAr;
        NameEn = normalizedEn;
        DisplayOrder = displayOrder;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static bool IsValid(
        Guid id,
        Guid doctorId,
        string nameAr,
        string? nameEn,
        int displayOrder,
        Guid actorId)
        => id != Guid.Empty && doctorId != Guid.Empty && actorId != Guid.Empty &&
           nameAr.Length is > 0 and <= 300 && (nameEn is null || nameEn.Length <= 300) &&
           displayOrder is >= 0 and <= 10000;
}

public static class DoctorQualificationErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorQualification.Invalid",
        ErrorMessage.DoctorQualificationInvalid);
    public static Error NotFound => Error.NotFound(
        "DoctorQualification.NotFound",
        ErrorMessage.DoctorQualificationNotFound);
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorQualification.ConcurrencyConflict",
        ErrorMessage.DoctorQualificationConcurrencyConflict);
}
