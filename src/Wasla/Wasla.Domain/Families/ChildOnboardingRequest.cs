using BuildingBlock.Domain.EntitiesHelper;
using Wasla.Domain.Common;

namespace Wasla.Domain.Families;

public enum ChildOnboardingStatus { PendingReview = 1, ModificationRequested = 2, Approved = 3, Rejected = 4, ConflictReview = 5 }
public sealed class ChildOnboardingRequest : Entity<Guid>
{
    private ChildOnboardingRequest() { }
    public ChildOnboardingRequest(Guid actor, Guid parent, FamilyMemberRole role, string nameAr, string? nameEn, DateOnly birth,
        Gender gender, string? phone, string contactName, string contactPhone, string key, string fingerprint, string intent, DateTime now)
    {
        if (actor == Guid.Empty || parent == Guid.Empty || role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) ||
            string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 200 || nameEn?.Length > 200 || birth == default || gender is not (Gender.Male or Gender.Female) ||
            !ValidContact(phone, contactName, contactPhone))
            throw new ArgumentException("Invalid private child intake.");
        Id = Guid.NewGuid(); ActorApplicationUserId = actor; ParentPatientId = parent; ParentRole = role; NameAr = nameAr.Trim(); NameEn = nameEn?.Trim();
        DateOfBirth = birth; Gender = gender; PhoneNumber = phone?.Trim(); ContactNameAr = contactName.Trim(); ContactPhone = contactPhone.Trim();
        IdempotencyKey = key; Fingerprint = fingerprint; IntentHash = intent; CreatedAtUtc = now;
    }
    public Guid ActorApplicationUserId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public FamilyMemberRole ParentRole { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string ContactNameAr { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public string IntentHash { get; private set; } = string.Empty;
    public ChildOnboardingStatus Status { get; private set; } = ChildOnboardingStatus.PendingReview;
    public int Revision { get; private set; } = 1;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewMessage { get; private set; }
    public Guid? ApprovedPatientId { get; private set; }
    public Guid? FamilyRelationshipRequestId { get; private set; }
    public Guid? GovernanceCaseId { get; private set; }
    public string? ReviewIdempotencyKey { get; private set; }
    public string? ReviewFingerprint { get; private set; }
    public string? ResubmitIdempotencyKey { get; private set; }
    public string? ResubmitFingerprint { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Decide(ChildOnboardingStatus status, Guid actor, string? message, DateTime now, Guid? patient = null, Guid? relationship = null, string? key = null, string? fingerprint = null)
    {
        if (Status != ChildOnboardingStatus.PendingReview || status is ChildOnboardingStatus.PendingReview || actor == Guid.Empty || message?.Length > 2000 ||
            (status == ChildOnboardingStatus.Approved && (patient is null || relationship is null)) || (status != ChildOnboardingStatus.Approved && string.IsNullOrWhiteSpace(message)))
            throw new InvalidOperationException("Review transition unavailable.");
        Status = status; ReviewedByApplicationUserId = actor; ReviewedAtUtc = now; ReviewMessage = message?.Trim();
        ApprovedPatientId = patient; FamilyRelationshipRequestId = relationship;
        ReviewIdempotencyKey = key; ReviewFingerprint = fingerprint;
    }
    public void Resubmit(string key, string fingerprint)
    {
        if (Status != ChildOnboardingStatus.ModificationRequested) throw new InvalidOperationException("Resubmission unavailable.");
        Revision++; Status = ChildOnboardingStatus.PendingReview; ReviewMessage = null; ReviewedAtUtc = null; ReviewedByApplicationUserId = null;
        ResubmitIdempotencyKey = key; ResubmitFingerprint = fingerprint;
    }
    public void ReferConflict(Guid caseId, Guid reviewer, DateTime now, string key, string fingerprint)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("A governance case is required.");
        Decide(ChildOnboardingStatus.ConflictReview, reviewer, "Confidential membership review", now, key: key, fingerprint: fingerprint);
        GovernanceCaseId = caseId;
    }
    public static bool ValidContact(string? phone, string? contactName, string? contactPhone) =>
        phone?.Length is not > 30 && contactName?.Length is not > 200 && contactPhone?.Length is not > 30 &&
        (!string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(contactPhone)) &&
        (string.IsNullOrWhiteSpace(contactPhone) || !string.IsNullOrWhiteSpace(contactName));
    public void CorrectPendingIntake(string nameAr, string? nameEn, DateOnly birth, Gender gender, string? phone, string contactName, string contactPhone, string intent)
    {
        if (Status != ChildOnboardingStatus.ModificationRequested || string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 200 || nameEn?.Length > 200 ||
            birth == default || gender is not (Gender.Male or Gender.Female) || !ValidContact(phone, contactName, contactPhone))
            throw new ArgumentException("Invalid revised child intake.");
        NameAr = nameAr.Trim(); NameEn = nameEn?.Trim(); DateOfBirth = birth; Gender = gender; PhoneNumber = phone?.Trim(); ContactNameAr = contactName.Trim(); ContactPhone = contactPhone.Trim(); IntentHash = intent;
    }
}
public sealed class ChildOnboardingRevision : Entity<Guid>
{
    private ChildOnboardingRevision() { }
    public ChildOnboardingRevision(ChildOnboardingRequest r, DateTime now)
    { Id = Guid.NewGuid(); RequestId = r.Id; Revision = r.Revision; NameAr = r.NameAr; NameEn = r.NameEn; DateOfBirth = r.DateOfBirth; Gender = r.Gender; PhoneNumber = r.PhoneNumber; ContactNameAr = r.ContactNameAr; ContactPhone = r.ContactPhone; CreatedAtUtc = now; ResubmitIdempotencyKey = r.ResubmitIdempotencyKey; ResubmitFingerprint = r.ResubmitFingerprint; ResultRowVersion = Convert.ToBase64String(r.RowVersion); }
    public Guid RequestId { get; private set; }
    public int Revision { get; private set; }
    public string NameAr { get; private set; } = "";
    public string? NameEn { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string ContactNameAr { get; private set; } = "";
    public string ContactPhone { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public string? ResubmitIdempotencyKey { get; private set; }
    public string? ResubmitFingerprint { get; private set; }
    public string ResultRowVersion { get; private set; } = "";
}
public sealed class ChildOnboardingEvidence : Entity<Guid>
{
    private ChildOnboardingEvidence() { }
    public ChildOnboardingEvidence(Guid request, int revision, FamilyRelationshipDocumentType type, string key, string name, string mime, long size, DateTime now)
    { Id = Guid.NewGuid(); RequestId = request; Revision = revision; DocumentType = type; MediaKey = key; FileName = name; ContentType = mime; FileSize = size; CreatedAtUtc = now; }
    public Guid RequestId { get; private set; }
    public int Revision { get; private set; }
    public FamilyRelationshipDocumentType DocumentType { get; private set; }
    public string MediaKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
public sealed class ChildOnboardingHistory : Entity<Guid>
{
    private ChildOnboardingHistory() { }
    public ChildOnboardingHistory(Guid request, int revision, Guid actor, string action, DateTime now, string? message = null)
    { Id = Guid.NewGuid(); RequestId = request; Revision = revision; ActorApplicationUserId = actor; Action = action; OccurredAtUtc = now; Message = message; }
    public Guid RequestId { get; private set; }
    public int Revision { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public string? Message { get; private set; }
}
