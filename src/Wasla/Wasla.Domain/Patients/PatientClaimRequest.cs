using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;

namespace Wasla.Domain.Patients;

public enum PatientClaimStatus { PendingReview = 1, ModificationRequested = 2, ApprovedPendingActivation = 3, Linked = 4, Rejected = 5, Expired = 6, Cancelled = 7 }
public sealed class PatientClaimRequest : AggregateRoot<Guid>, IAuditableEntity
{
    private PatientClaimRequest() { }
    public PatientClaimRequest(Guid id, Guid? targetId, string email, Guid? reference, string name, DateOnly dob,
        Gender gender, string? phone, Guid challengeId, string idempotencyKey, string fingerprint, DateTime now) : base(id)
    {
        if (id == Guid.Empty || challengeId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 ||
            dob == default || dob > DateOnly.FromDateTime(now) || gender is not Gender.Male and not Gender.Female ||
            !PatientIdentityEmail.IsValid(email) || string.IsNullOrEmpty(email) || phone?.Length > 30) throw new ArgumentException("Invalid claim.");
        TargetPatientId = targetId; ClaimantNormalizedEmail = PatientIdentityEmail.Normalize(email)!; PatientReference = reference;
        ApplicantNameAr = name.Trim(); ApplicantDateOfBirth = dob; ApplicantGender = gender; ApplicantPhoneNumber = phone?.Trim();
        SourceChallengeId = challengeId; IdempotencyKey = idempotencyKey; RequestFingerprint = fingerprint;
        EvidenceRevision = 1; CreatedOnUtc = SubmittedOnUtc = LastActivityOnUtc = now;
    }
    public Guid? TargetPatientId { get; private set; }
    public string ClaimantNormalizedEmail { get; private set; } = string.Empty;
    public Guid? PatientReference { get; private set; }
    public PatientClaimStatus Status { get; private set; } = PatientClaimStatus.PendingReview;
    public string ApplicantNameAr { get; private set; } = string.Empty;
    public DateOnly ApplicantDateOfBirth { get; private set; }
    public Gender ApplicantGender { get; private set; }
    public string? ApplicantPhoneNumber { get; private set; }
    public Guid SourceChallengeId { get; private set; }
    public int EvidenceRevision { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public DateTime SubmittedOnUtc { get; private set; }
    public DateTime LastActivityOnUtc { get; private set; }
    public DateTime? ReviewedOnUtc { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? ApprovedTargetPatientId { get; private set; }
    public Guid? LinkedApplicationUserId { get; private set; }
    public DateTime? ClosedOnUtc { get; private set; }
    public bool IsUnderLegalHold { get; private set; }
    public string? LegalHoldReason { get; private set; }
    public Guid? LegalHoldSetByApplicationUserId { get; private set; }
    public DateTime? LegalHoldSetOnUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];
    public bool IsTerminal => Status is PatientClaimStatus.Linked or PatientClaimStatus.Rejected or PatientClaimStatus.Expired or PatientClaimStatus.Cancelled;
    public Result Decide(PatientClaimStatus status, Guid reviewer, string? reason, Guid? target, DateTime now)
    {
        if (Status != PatientClaimStatus.PendingReview || reviewer == Guid.Empty ||
            status is not (PatientClaimStatus.ApprovedPendingActivation or PatientClaimStatus.Rejected or PatientClaimStatus.ModificationRequested))
            return Result.Fail(PatientIdentityErrors.Conflict("PatientClaims.InvalidState"));
        if (reason?.Length > 1000 || status != PatientClaimStatus.ApprovedPendingActivation && string.IsNullOrWhiteSpace(reason) ||
            status == PatientClaimStatus.ApprovedPendingActivation && (target is null || target == Guid.Empty)) return Result.Fail(PatientIdentityErrors.Proof);
        Status = status; ReviewedByApplicationUserId = reviewer; ReviewedOnUtc = now; DecisionReason = reason?.Trim();
        // Pending/modification inactivity runs from the applicant's last submission.
        // Approval issues an invitation and begins its separate activity window.
        if (status != PatientClaimStatus.ModificationRequested) LastActivityOnUtc = now;
        if (status == PatientClaimStatus.ApprovedPendingActivation) ApprovedTargetPatientId = TargetPatientId = target;
        if (status == PatientClaimStatus.Rejected) ClosedOnUtc = now;
        return Result.Ok();
    }
    public Result Resubmit(DateTime now)
    {
        if (Status != PatientClaimStatus.ModificationRequested) return Result.Fail(PatientIdentityErrors.Conflict("PatientClaims.InvalidState"));
        Status = PatientClaimStatus.PendingReview; EvidenceRevision++; SubmittedOnUtc = LastActivityOnUtc = now; return Result.Ok();
    }
    public Result CorrectApplicant(string? name, DateOnly? dob, Gender? gender, string? phone, DateTime now)
    {
        if (Status != PatientClaimStatus.ModificationRequested) return Result.Fail(PatientIdentityErrors.Conflict("PatientClaims.InvalidState"));
        if (name is not null && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) || dob.HasValue && (dob.Value == default || dob.Value > DateOnly.FromDateTime(now)) ||
            gender.HasValue && gender is not (Gender.Male or Gender.Female) || phone?.Length > 30) return Result.Fail(PatientIdentityErrors.Invalid);
        ApplicantNameAr = name?.Trim() ?? ApplicantNameAr; ApplicantDateOfBirth = dob ?? ApplicantDateOfBirth;
        ApplicantGender = gender ?? ApplicantGender; ApplicantPhoneNumber = phone?.Trim() ?? ApplicantPhoneNumber; return Result.Ok();
    }
    public Result Link(Guid userId, DateTime now)
    {
        if (Status != PatientClaimStatus.ApprovedPendingActivation || ApprovedTargetPatientId is null || userId == Guid.Empty)
            return Result.Fail(PatientIdentityErrors.Conflict("PatientClaims.InvalidState"));
        LinkedApplicationUserId = userId; Status = PatientClaimStatus.Linked; ClosedOnUtc = LastActivityOnUtc = now; return Result.Ok();
    }
    public bool Expire(DateTime now, int inactivityDays)
    {
        if (IsTerminal || now < LastActivityOnUtc.AddDays(inactivityDays)) return false;
        Status = PatientClaimStatus.Expired; ClosedOnUtc = LastActivityOnUtc = now; return true;
    }
    public void InvitationResent(DateTime now)
    {
        if (Status != PatientClaimStatus.ApprovedPendingActivation) throw new InvalidOperationException("Not awaiting activation.");
        LastActivityOnUtc = now;
    }
    public void SetLegalHold(bool held, Guid actor, string reason, DateTime now)
    {
        if (actor == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ArgumentException("Legal hold requires authorized actor and reason.");
        IsUnderLegalHold = held; LegalHoldReason = reason.Trim(); LegalHoldSetByApplicationUserId = actor; LegalHoldSetOnUtc = now;
    }
}
