using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Domain.Patients;

public sealed class Patient : AggregateRoot<Guid>, IAuditableEntity
{
    private Patient()
    {
    }

    private Patient(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? profileImageMediaKey,
        string? personalIdFrontMediaKey,
        string? personalIdBackMediaKey)
        : base(id)
    {
        ApplicationUserId = applicationUserId;
        NameAr = nameAr;
        NameEn = nameEn;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        ProfileImageMediaKey = profileImageMediaKey;
        PersonalIdFrontMediaKey = personalIdFrontMediaKey;
        PersonalIdBackMediaKey = personalIdBackMediaKey;
    }

    public Guid ApplicationUserId { get; private set; }
    public ApplicationUser ApplicationUser { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? ProfileImageMediaKey { get; private set; }
    public string? PersonalIdFrontMediaKey { get; private set; }
    public string? PersonalIdBackMediaKey { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public static Result<Patient> Create(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? profileImageMediaKey,
        string? personalIdFrontMediaKey,
        string? personalIdBackMediaKey,
        DateOnly today)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (id == Guid.Empty || applicationUserId == Guid.Empty ||
            normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200 ||
            dateOfBirth == default || dateOfBirth > today ||
            gender is not Gender.Male and not Gender.Female)
        {
            return Result<Patient>.Fail(Error.Domain("Patient.Invalid", ErrorMessage.DateOfBirthFuture));
        }

        return Result<Patient>.Ok(new Patient(
            id,
            applicationUserId,
            normalizedAr,
            normalizedEn,
            dateOfBirth,
            gender,
            Normalize(profileImageMediaKey),
            Normalize(personalIdFrontMediaKey),
            Normalize(personalIdBackMediaKey)));
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

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
