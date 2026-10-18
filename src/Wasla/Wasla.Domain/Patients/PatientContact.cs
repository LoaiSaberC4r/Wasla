using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Patients;

public enum PatientContactRelationshipType
{
    Father = 1,
    Mother = 2,
    Guardian = 3,
    LegalGuardian = 4,
    Other = 5
}

public sealed class PatientContact : Entity<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private PatientContact()
    {
    }

    private PatientContact(Guid id) : base(id)
    {
    }

    public Guid PatientId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public string? ContactEmail { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public PatientContactRelationshipType RelationshipType { get; private set; }
    public Guid? LinkedPatientId { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }

    public static Result<PatientContact> Create(Guid id, Guid patientId, string nameAr, string? nameEn, string phoneNumber, PatientContactRelationshipType relationshipType, Guid? linkedPatientId, bool isPrimary, string? contactEmail = null)
    {
        var item = new PatientContact(id);
        var validation = item.Apply(id, patientId, nameAr, nameEn, phoneNumber, relationshipType, linkedPatientId, isPrimary, contactEmail);
        return validation.IsFailure ? Result<PatientContact>.Fail(validation.Errors) : Result<PatientContact>.Ok(item);
    }

    public Result Update(string nameAr, string? nameEn, string phoneNumber, PatientContactRelationshipType relationshipType, Guid? linkedPatientId, bool isPrimary, string? contactEmail = null)
        => Apply(Id, PatientId, nameAr, nameEn, phoneNumber, relationshipType, linkedPatientId, isPrimary, contactEmail);

    private Result Apply(Guid id, Guid patientId, string nameAr, string? nameEn, string phoneNumber, PatientContactRelationshipType relationshipType, Guid? linkedPatientId, bool isPrimary, string? contactEmail = null)
    {
        var normalizedEmail = PatientIdentityEmail.Normalize(contactEmail);
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        var normalizedPhone = phoneNumber?.Trim() ?? string.Empty;
        if (id == Guid.Empty || patientId == Guid.Empty || normalizedAr.Length is 0 or > 200 ||
            normalizedEn?.Length > 200 || normalizedPhone.Length is 0 or > 30 ||
            !PatientIdentityEmail.IsValid(normalizedEmail) || !Enum.IsDefined(relationshipType) || linkedPatientId == patientId)
        {
            return Result.Fail(PatientErrors.InvalidContact);
        }

        PatientId = patientId;
        ContactEmail = normalizedEmail;
        NameAr = normalizedAr;
        NameEn = normalizedEn;
        PhoneNumber = normalizedPhone;
        RelationshipType = relationshipType;
        LinkedPatientId = linkedPatientId;
        IsPrimary = isPrimary;
        return Result.Ok();
    }
}
