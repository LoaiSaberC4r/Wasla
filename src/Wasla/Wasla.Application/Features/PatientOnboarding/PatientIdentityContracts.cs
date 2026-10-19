using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using Wasla.Application.Persistence;
using Wasla.Domain.Patients;
using Wasla.Domain.Common;
using FluentValidation;

namespace Wasla.Application.Features.PatientOnboarding;

public enum PatientIdentityMutation { Start, Verify, Register, Full, ConfirmReuse, ConfirmClaimIdentity, SubmitClaim, ResubmitClaim, Activate, Resend, Approve, Reject, Modify, ResolveDuplicate }
public enum PatientIdentityRead { Matches, OwnClaim, Claims, ClaimDetails, ClaimEvidence, ClaimHistory, Duplicates, DuplicateDetails }
// Product-specific CQRS envelope; all identity writes share the existing Wasla transaction behavior.
public sealed record PatientIdentityCommand(PatientIdentityMutation Operation, PatientIdentityInput Input,
    string? IdempotencyKey = null, string? Token = null, Guid? Id = null) : ICommand<object>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record PatientIdentityQuery(PatientIdentityRead Operation, PatientIdentityInput Input,
    string? Token = null, Guid? Id = null, Guid? EvidenceId = null) : IQuery<object>;
public sealed class PatientIdentityInput
{
    public string? Email { get; set; }
    public PatientOnboardingPurpose Purpose { get; set; }
    public Guid? PatientReference { get; set; }
    public Guid RequestId { get; set; }
    public string? Otp { get; set; }
    public string? EmailVerificationToken { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public string? InvitationToken { get; set; }
    public string? VerificationToken { get; set; }
    public Guid DoctorPracticeId { get; set; }
    public Guid PatientId { get; set; }
    public Guid TargetPatientId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PrimaryContactNameAr { get; set; }
    public string? PrimaryContactNameEn { get; set; }
    public string? PrimaryContactPhoneNumber { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public Guid? PrimaryContactLinkedPatientId { get; set; }
    public PatientContactRelationshipType PrimaryContactRelationshipType { get; set; }
    public bool PrimaryContactIsPrimary { get; set; } = true;
    public bool VerifiedInPerson { get; set; }
    public PatientIdentityVerificationBasis VerificationBasis { get; set; }
    public string? VerificationNote { get; set; }
    public string? MatchReviewOutcome { get; set; }
    public string? MatchReviewReason { get; set; }
    public Guid? InPersonIdentityAttestationId { get; set; }
    public IReadOnlyList<PatientClaimEvidenceType> EvidenceTypes { get; set; } = [];
    public PatientClaimIdentityCorrections? IdentityProfileCorrections { get; set; }
    public MediaUpload? ProfileImage { get; set; }
    public MediaUpload? PersonalIdFrontImage { get; set; }
    public MediaUpload? PersonalIdBackImage { get; set; }
    public MediaUpload? IdentityFront { get; set; }
    public MediaUpload? IdentityBack { get; set; }
    public MediaUpload? BirthCertificate { get; set; }
    public MediaUpload? GuardianRelationshipDocument { get; set; }
    public string? Reason { get; set; }
    public string? RowVersion { get; set; }
    public Guid[] ReviewedEvidenceIds { get; set; } = [];
    public PatientClaimStatus? Status { get; set; }
    public PatientDuplicateStatus? DuplicateStatus { get; set; }
    public PatientDuplicateStatus Resolution { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
public sealed record PatientOtpVerificationFailure(BuildingBlock.Domain.Results.Error Error);
public sealed record PatientClaimIdentityCorrections(string? NameAr = null, DateOnly? DateOfBirth = null, Gender? Gender = null, string? PhoneNumber = null);
internal sealed class PatientIdentityCommandValidator : AbstractValidator<PatientIdentityCommand>
{
    public PatientIdentityCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.IdempotencyKey).Must(k => k is { Length: <= 100 } && Guid.TryParse(k, out _))
            .When(x => x.Operation is not (PatientIdentityMutation.Start or PatientIdentityMutation.Verify or PatientIdentityMutation.Activate));
        RuleFor(x => x.Input.Email).MaximumLength(200).EmailAddress().When(x => x.Input.Email is not null);
        RuleFor(x => x.Input.DoctorPracticeId).NotEmpty().When(x => x.Operation is PatientIdentityMutation.Full or PatientIdentityMutation.ConfirmReuse or PatientIdentityMutation.ConfirmClaimIdentity);
        RuleFor(x => x.Input.EvidenceTypes).Must(e => e is not null && e.Count is >= 1 and <= 5 && e.All(Enum.IsDefined)).When(x => x.Operation is PatientIdentityMutation.SubmitClaim or PatientIdentityMutation.ResubmitClaim);
        RuleFor(x => x.Input.Reason).NotEmpty().WithErrorCode("Patient.Invalid").WithMessage(Wasla.Domain.Resources.ErrorMessage.PatientIdentityInvalid)
            .When(x => x.Operation is PatientIdentityMutation.Reject or PatientIdentityMutation.Modify or PatientIdentityMutation.ResolveDuplicate);
        RuleFor(x => x.Input.NameAr).MaximumLength(200); RuleFor(x => x.Input.NameEn).MaximumLength(200);
        RuleFor(x => x.Input.PhoneNumber).MaximumLength(30); RuleFor(x => x.Input.Reason).MaximumLength(1000);
        RuleFor(x => x.Input.VerificationNote).MaximumLength(500); RuleFor(x => x.Input.MatchReviewReason).MaximumLength(500);
        RuleFor(x => x.Input.EmailVerificationToken).MaximumLength(500); RuleFor(x => x.Token).MaximumLength(500);
        RuleFor(x => x.Input.VerificationToken).MaximumLength(500); RuleFor(x => x.Input.InvitationToken).MaximumLength(500);
        RuleFor(x => x.Input.Password).MaximumLength(1000); RuleFor(x => x.Input.ReviewedEvidenceIds).Must(a => a is not null && a.Length <= 20);
    }
}
internal sealed class PatientIdentityQueryValidator : AbstractValidator<PatientIdentityQuery>
{
    public PatientIdentityQueryValidator()
    {
        RuleFor(x => x.Input.PageNumber).InclusiveBetween(1, 100000); RuleFor(x => x.Input.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Input.Email).MaximumLength(200).EmailAddress().When(x => x.Input.Email is not null);
        RuleFor(x => x.Input.Status).IsInEnum().When(x => x.Input.Status.HasValue);
        RuleFor(x => x.Input.DuplicateStatus).IsInEnum().When(x => x.Input.DuplicateStatus.HasValue);
    }
}
public interface IPatientIdentityMediaCompensation
{
    void Track(string key);
    void Clear();
    Task CleanAsync();
}
