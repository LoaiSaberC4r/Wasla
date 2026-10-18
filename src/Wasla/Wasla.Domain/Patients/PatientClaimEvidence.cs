using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace Wasla.Domain.Patients;
public enum PatientClaimEvidenceType { GovernmentIdFront = 1, GovernmentIdBack = 2, BirthCertificate = 3, InPersonIdentityAttestation = 4, GuardianRelationshipEvidence = 5 }
public enum PatientEvidenceUploaderType { Applicant = 1, Reception = 2 }
public sealed class PatientClaimEvidence : Entity<Guid>
{
    private PatientClaimEvidence() { }
    public PatientClaimEvidence(Guid id, Guid claimId, int revision, PatientClaimEvidenceType type, string? key,
        string? contentType, long? size, string? sha256, PatientEvidenceUploaderType uploader, Guid? actor, DateTime now,
        Guid? attestationId = null) : base(id)
    {
        if (id == Guid.Empty || claimId == Guid.Empty || revision < 1 || !Enum.IsDefined(type) ||
            type == PatientClaimEvidenceType.InPersonIdentityAttestation && attestationId is null ||
            type != PatientClaimEvidenceType.InPersonIdentityAttestation && string.IsNullOrEmpty(key)) throw new ArgumentException("Invalid evidence.");
        ClaimRequestId = claimId; RevisionNumber = revision; EvidenceType = type; PrivateMediaKey = key; ContentType = contentType;
        SizeBytes = size; Sha256Hash = sha256; UploadedByType = uploader; UploadedByApplicationUserId = actor; UploadedOnUtc = now; IdentityAttestationId = attestationId;
    }
    public Guid ClaimRequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public PatientClaimEvidenceType EvidenceType { get; private set; }
    public string? PrivateMediaKey { get; private set; }
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public string? Sha256Hash { get; private set; }
    public PatientEvidenceUploaderType UploadedByType { get; private set; }
    public Guid? UploadedByApplicationUserId { get; private set; }
    public Guid? IdentityAttestationId { get; private set; }
    public DateTime UploadedOnUtc { get; private set; }
    public DateTime? RetiredOnUtc { get; private set; }
    public DateTime? PurgedOnUtc { get; private set; }
    public void Retire(DateTime now) => RetiredOnUtc ??= now;
    public void MarkPurged(DateTime now)
    {
        if (RetiredOnUtc is null) throw new InvalidOperationException("Persist retirement before storage deletion.");
        PurgedOnUtc ??= now; // Keep the key until aggregate retention so failed deletes remain retryable.
    }
}
