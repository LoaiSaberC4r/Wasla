namespace Wasla.Domain.Families;

/// <summary>A private one-use review target. Possession conveys no family authority.</summary>
public sealed class FamilyJoinInvitation
{
    private FamilyJoinInvitation() { }
    public FamilyJoinInvitation(Guid issuer, Guid parent, Guid child, Guid family, Guid authority, FamilyMemberRole role,
        string tokenHash, string key, string fingerprint, string protectedReference, DateTime now, DateTime expires)
    {
        if (issuer == Guid.Empty || parent == Guid.Empty || child == Guid.Empty || family == Guid.Empty || authority == Guid.Empty ||
            role is not (FamilyMemberRole.Father or FamilyMemberRole.Mother) || tokenHash.Length != 64 || expires <= now)
            throw new ArgumentException("Invalid invitation.");
        Id = Guid.NewGuid(); IssuedByUserId = issuer; IssuerPatientId = parent; ChildPatientId = child; FamilyId = family;
        OperationalAuthorityId = authority; IntendedRole = role; TokenHash = tokenHash; IdempotencyKey = key;
        Fingerprint = fingerprint; ProtectedReference = protectedReference; CreatedAtUtc = now; ExpiresAtUtc = expires;
    }
    public Guid Id { get; private set; }
    public Guid IssuedByUserId { get; private set; }
    public Guid IssuerPatientId { get; private set; }
    public Guid ChildPatientId { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid OperationalAuthorityId { get; private set; }
    public FamilyMemberRole IntendedRole { get; private set; }
    public string TokenHash { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string ProtectedReference { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public Guid? RedeemedByUserId { get; private set; }
    public string? RedemptionKey { get; private set; }
    public string? RedemptionFingerprint { get; private set; }
    public Guid? RelationshipRequestId { get; private set; }
    public Guid? GovernanceCaseId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationKey { get; private set; }
    public void Revoke(string key, DateTime now)
    {
        if (ConsumedAtUtc.HasValue || RevokedAtUtc.HasValue) throw new InvalidOperationException("Invitation unavailable.");
        RevokedAtUtc = now; RevocationKey = key;
    }
    public byte[] RowVersion { get; private set; } = [];
    public void Consume(Guid user, string key, string fingerprint, Guid? request, Guid? review, DateTime now)
    {
        if (ConsumedAtUtc.HasValue || RevokedAtUtc.HasValue || ExpiresAtUtc <= now || user == Guid.Empty || user == IssuedByUserId || request.HasValue == review.HasValue)
            throw new InvalidOperationException("Invitation unavailable.");
        RedeemedByUserId = user; RedemptionKey = key; RedemptionFingerprint = fingerprint; RelationshipRequestId = request;
        GovernanceCaseId = review; ConsumedAtUtc = now;
    }
}
