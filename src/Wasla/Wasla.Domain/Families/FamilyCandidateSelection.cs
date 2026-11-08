using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Families;

public sealed class FamilyCandidateSelection : Entity<Guid>
{
    private FamilyCandidateSelection() { }
    public FamilyCandidateSelection(Guid actor, Guid parent, Guid target, string hash, string criteriaHash, DateTime now, DateTime expires)
    { Id = Guid.NewGuid(); ActorApplicationUserId = actor; ParentPatientId = parent; TargetPatientId = target; TokenHash = hash; CriteriaHash = criteriaHash; CreatedAtUtc = now; ExpiresAtUtc = expires; }
    public Guid ActorApplicationUserId { get; private set; }
    public Guid ParentPatientId { get; private set; }
    public Guid TargetPatientId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string CriteriaHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = "ParentChildRelationship";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? SubmissionFingerprint { get; private set; }
    public Guid? RelationshipRequestId { get; private set; }
    public Guid? GovernanceCaseId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void ConsumeConflict(string key, string fingerprint, Guid caseId, DateTime now)
    {
        if (ConsumedAtUtc.HasValue || ExpiresAtUtc <= now || caseId == Guid.Empty) throw new InvalidOperationException("Candidate reference unavailable.");
        IdempotencyKey = key; SubmissionFingerprint = fingerprint; GovernanceCaseId = caseId; ConsumedAtUtc = now;
    }
    public void Consume(string key, string fingerprint, Guid request, DateTime now)
    {
        if (ConsumedAtUtc.HasValue || ExpiresAtUtc <= now) throw new InvalidOperationException("Candidate reference unavailable.");
        IdempotencyKey = key; SubmissionFingerprint = fingerprint; RelationshipRequestId = request; ConsumedAtUtc = now;
    }
}
