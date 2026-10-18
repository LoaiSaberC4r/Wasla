using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
namespace Wasla.Domain.Patients;
public enum PatientActivationPurpose { ReceptionFull = 1, ApprovedClaim = 2 }
public sealed class PatientActivationInvitation : Entity<Guid>
{
    private PatientActivationInvitation() { }
    public PatientActivationInvitation(Guid id, PatientActivationPurpose purpose, Guid? userId, Guid? claimId,
        string email, string hash, DateTime now, DateTime expires, int sendCount = 1) : base(id)
    {
        if (id == Guid.Empty || expires <= now || string.IsNullOrEmpty(hash) || !PatientIdentityEmail.IsValid(email) || string.IsNullOrEmpty(email) ||
            purpose == PatientActivationPurpose.ReceptionFull && (userId is null || claimId is not null) ||
            purpose == PatientActivationPurpose.ApprovedClaim && (claimId is null || userId is not null) || !Enum.IsDefined(purpose)) throw new ArgumentException("Invalid invitation.");
        Purpose = purpose; ApplicationUserId = userId; ClaimRequestId = claimId; NormalizedEmail = PatientIdentityEmail.Normalize(email)!;
        TokenHash = hash; CreatedOnUtc = now; ExpiresOnUtc = expires; SendCount = sendCount;
    }
    public PatientActivationPurpose Purpose { get; private set; }
    public Guid? ApplicationUserId { get; private set; }
    public Guid? ClaimRequestId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? ConsumedOnUtc { get; private set; }
    public DateTime? RevokedOnUtc { get; private set; }
    public int SendCount { get; private set; }
    public string? ResendIdempotencyKey { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool CanActivate(DateTime now) => now < ExpiresOnUtc && ConsumedOnUtc is null && RevokedOnUtc is null;
    public void StampResend(string key) => ResendIdempotencyKey = key;
    public void Revoke(DateTime now) { if (ConsumedOnUtc is null) RevokedOnUtc ??= now; }
    public void Consume(DateTime now)
    {
        if (!CanActivate(now)) throw new InvalidOperationException("Invitation cannot be consumed.");
        ConsumedOnUtc = now;
    }
}
