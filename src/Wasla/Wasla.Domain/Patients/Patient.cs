using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;

namespace Wasla.Domain.Patients;

public sealed class Patient : AggregateRoot<Guid>, IAuditableEntity
{
    private Patient()
    {
    }

    private Patient(
        Guid id,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? phoneNumber,
        string? email,
        string? profileImageMediaKey,
        string? personalIdFrontMediaKey,
        string? personalIdBackMediaKey)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        PhoneNumber = phoneNumber;
        Email = email;
        ProfileImageMediaKey = profileImageMediaKey;
        PersonalIdFrontMediaKey = personalIdFrontMediaKey;
        PersonalIdBackMediaKey = personalIdBackMediaKey;
    }

    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? ProfileImageMediaKey { get; private set; }
    public string? PersonalIdFrontMediaKey { get; private set; }
    public string? PersonalIdBackMediaKey { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Patient> Create(
        Guid id,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? phoneNumber,
        string? email,
        string? profileImageMediaKey,
        string? personalIdFrontMediaKey,
        string? personalIdBackMediaKey,
        DateOnly today)
    {
        var normalizedAr = NormalizeRequired(nameAr);
        var normalizedEn = Normalize(nameEn);
        var normalizedPhone = Normalize(phoneNumber);
        var normalizedEmail = Normalize(email)?.ToLowerInvariant();
        if (id == Guid.Empty ||
            normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200 ||
            normalizedPhone?.Length > 30 || normalizedEmail?.Length > 200 ||
            dateOfBirth == default || dateOfBirth > today ||
            gender is not Gender.Male and not Gender.Female)
        {
            return Result<Patient>.Fail(PatientErrors.Invalid);
        }

        return Result<Patient>.Ok(new Patient(
            id, normalizedAr, normalizedEn, dateOfBirth, gender, normalizedPhone, normalizedEmail,
            Normalize(profileImageMediaKey), Normalize(personalIdFrontMediaKey), Normalize(personalIdBackMediaKey)));
    }

    public Result UpdateProfile(
        string nameAr,
        string? nameEn,
        string? phoneNumber,
        string? email,
        string? profileImageMediaKey = null,
        bool replaceProfileImage = false)
    {
        var normalizedAr = NormalizeRequired(nameAr);
        var normalizedEn = Normalize(nameEn);
        var normalizedPhone = Normalize(phoneNumber);
        var normalizedEmail = Normalize(email)?.ToLowerInvariant();
        if (normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200 ||
            normalizedPhone?.Length > 30 || normalizedEmail?.Length > 200)
        {
            return Result.Fail(PatientErrors.Invalid);
        }

        NameAr = normalizedAr;
        NameEn = normalizedEn;
        PhoneNumber = normalizedPhone;
        Email = normalizedEmail;
        if (replaceProfileImage)
        {
            ProfileImageMediaKey = Normalize(profileImageMediaKey);
        }

        return Result.Ok();
    }

    public int GetAge(DateOnly today)
    {
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
