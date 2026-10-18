using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
namespace Wasla.Domain.Patients;
public enum PatientIdentityVerificationBasis { GovernmentIdSeen = 1, PatientProvidedKnownDemographics = 2, ApprovedAlternateEvidence = 3 }
public enum PatientIdentityAttestationPurpose { ReuseForBooking = 1, ClaimProof = 2, NewFullRegistration = 3, SoftMatchOverride = 4 }
public sealed class PatientIdentityAttestation : Entity<Guid>
{
    private PatientIdentityAttestation() { }
    public PatientIdentityAttestation(Guid id, Guid patientId, Guid practiceId, Guid actor, PatientIdentityVerificationBasis basis,
        PatientIdentityAttestationPurpose purpose, DateTime now, DateTime? expires, string? reason, string? email = null) : base(id)
    {
        if (id == Guid.Empty || patientId == Guid.Empty || practiceId == Guid.Empty || actor == Guid.Empty || !Enum.IsDefined(basis) ||
            !Enum.IsDefined(purpose) || reason?.Length > 500 || purpose == PatientIdentityAttestationPurpose.ClaimProof &&
            (string.IsNullOrEmpty(email) || !PatientIdentityEmail.IsValid(email) || basis == PatientIdentityVerificationBasis.PatientProvidedKnownDemographics)) throw new ArgumentException("Invalid attestation.");
        PatientId = patientId; DoctorPracticeId = practiceId; ReceptionApplicationUserId = actor; VerificationBasis = basis;
        Purpose = purpose; OccurredOnUtc = now; ExpiresOnUtc = expires; Reason = reason?.Trim(); VerifiedClaimantNormalizedEmail = PatientIdentityEmail.Normalize(email);
    }
    public Guid PatientId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid ReceptionApplicationUserId { get; private set; }
    public PatientIdentityVerificationBasis VerificationBasis { get; private set; }
    public PatientIdentityAttestationPurpose Purpose { get; private set; }
    public string? VerifiedClaimantNormalizedEmail { get; private set; }
    public Guid? RelatedReservationId { get; private set; }
    public Guid? RelatedTicketId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ExpiresOnUtc { get; private set; }
    public string? Reason { get; private set; }
    public DateTime? ConsumedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsValid(Guid patient, Guid practice, Guid actor, DateTime now) => PatientId == patient && DoctorPracticeId == practice &&
        ReceptionApplicationUserId == actor && ConsumedOnUtc is null && ExpiresOnUtc > now;
    public void Associate(Guid? reservation, Guid? ticket, DateTime now)
    {
        if (ConsumedOnUtc is not null || ExpiresOnUtc <= now || Purpose != PatientIdentityAttestationPurpose.ReuseForBooking || (reservation is null) == (ticket is null))
            throw new InvalidOperationException("Invalid attestation use.");
        RelatedReservationId = reservation; RelatedTicketId = ticket; ConsumedOnUtc = now;
    }
}
