using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Domain.Doctors;

public sealed class Doctor : AggregateRoot<Guid>, IAuditableEntity
{
    private Doctor()
    {
    }

    private Doctor(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? profileImageMediaKey,
        string personalIdFrontMediaKey,
        string personalIdBackMediaKey,
        string syndicateCardFrontMediaKey,
        string? syndicateCardBackMediaKey)
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
        SyndicateCardFrontMediaKey = syndicateCardFrontMediaKey;
        SyndicateCardBackMediaKey = syndicateCardBackMediaKey;
        ApprovalStatus = DoctorApprovalStatus.Pending;
    }

    public Guid ApplicationUserId { get; private set; }
    public ApplicationUser ApplicationUser { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? ProfileImageMediaKey { get; private set; }
    public string PersonalIdFrontMediaKey { get; private set; } = string.Empty;
    public string PersonalIdBackMediaKey { get; private set; } = string.Empty;
    public string SyndicateCardFrontMediaKey { get; private set; } = string.Empty;
    public string? SyndicateCardBackMediaKey { get; private set; }
    public string? NationalId { get; private set; }
    public DoctorApprovalStatus ApprovalStatus { get; private set; }
    public Guid? ApprovedByApplicationUserId { get; private set; }
    public DateTime? ApprovedOnUtc { get; private set; }
    public Guid? RejectedByApplicationUserId { get; private set; }
    public DateTime? RejectedOnUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? SuspendedByApplicationUserId { get; private set; }
    public DateTime? SuspendedOnUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public Guid? ReactivatedByApplicationUserId { get; private set; }
    public DateTime? ReactivatedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Doctor> Create(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        DateOnly dateOfBirth,
        Gender gender,
        string? profileImageMediaKey,
        string personalIdFrontMediaKey,
        string personalIdBackMediaKey,
        string syndicateCardFrontMediaKey,
        string? syndicateCardBackMediaKey,
        DateOnly today)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (id == Guid.Empty || applicationUserId == Guid.Empty ||
            normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200 ||
            dateOfBirth == default || dateOfBirth > today ||
            gender is not Gender.Male and not Gender.Female ||
            string.IsNullOrWhiteSpace(personalIdFrontMediaKey) ||
            string.IsNullOrWhiteSpace(personalIdBackMediaKey) ||
            string.IsNullOrWhiteSpace(syndicateCardFrontMediaKey))
        {
            return Result<Doctor>.Fail(Error.Domain("Doctor.Invalid", ErrorMessage.DoctorInvalidStatus));
        }

        return Result<Doctor>.Ok(new Doctor(
            id,
            applicationUserId,
            normalizedAr,
            normalizedEn,
            dateOfBirth,
            gender,
            NormalizeKey(profileImageMediaKey),
            personalIdFrontMediaKey.Trim(),
            personalIdBackMediaKey.Trim(),
            syndicateCardFrontMediaKey.Trim(),
            NormalizeKey(syndicateCardBackMediaKey)));
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

    public Result Approve(string nationalId, Guid actorId, DateTime occurredOnUtc)
    {
        if (ApprovalStatus != DoctorApprovalStatus.Pending)
        {
            return Result.Fail(DoctorErrors.InvalidStatus);
        }

        var normalized = nationalId?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 100)
        {
            return Result.Fail(DoctorErrors.NationalIdRequired);
        }

        NationalId = normalized;
        ApprovalStatus = DoctorApprovalStatus.Approved;
        ApprovedByApplicationUserId = actorId;
        ApprovedOnUtc = RequireUtc(occurredOnUtc);
        return Result.Ok();
    }

    public Result Reject(string reason, Guid actorId, DateTime occurredOnUtc)
    {
        if (ApprovalStatus != DoctorApprovalStatus.Pending)
        {
            return Result.Fail(DoctorErrors.InvalidStatus);
        }

        var normalized = NormalizeReason(reason);
        if (normalized is null)
        {
            return Result.Fail(DoctorErrors.ReasonRequired);
        }

        ApprovalStatus = DoctorApprovalStatus.Rejected;
        RejectedByApplicationUserId = actorId;
        RejectedOnUtc = RequireUtc(occurredOnUtc);
        RejectionReason = normalized;
        return Result.Ok();
    }

    public Result Suspend(string reason, Guid actorId, DateTime occurredOnUtc)
    {
        if (ApprovalStatus != DoctorApprovalStatus.Approved)
        {
            return Result.Fail(DoctorErrors.InvalidStatus);
        }

        var normalized = NormalizeReason(reason);
        if (normalized is null)
        {
            return Result.Fail(DoctorErrors.ReasonRequired);
        }

        ApprovalStatus = DoctorApprovalStatus.Suspended;
        SuspendedByApplicationUserId = actorId;
        SuspendedOnUtc = RequireUtc(occurredOnUtc);
        SuspensionReason = normalized;
        return Result.Ok();
    }

    public Result Reactivate(Guid actorId, DateTime occurredOnUtc)
    {
        if (ApprovalStatus != DoctorApprovalStatus.Suspended)
        {
            return Result.Fail(DoctorErrors.InvalidStatus);
        }

        ApprovalStatus = DoctorApprovalStatus.Approved;
        ReactivatedByApplicationUserId = actorId;
        ReactivatedOnUtc = RequireUtc(occurredOnUtc);
        return Result.Ok();
    }

    private static string? NormalizeReason(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1000
            ? null
            : normalized;
    }

    private static string? NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

