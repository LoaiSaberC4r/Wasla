using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace Wasla.Domain.Patients;

public enum PatientOnboardingPurpose { Register = 1, Claim = 2, EmailChange = 3 }
public enum PatientOnboardingAction { RegisterNew = 1, Claim = 2, LoginOrRecovery = 3, IdentityConflict = 4, EmailChange = 5 }

public sealed class PatientOnboardingChallenge : Entity<Guid>
{
    private PatientOnboardingChallenge() { }
    public PatientOnboardingChallenge(Guid id, PatientOnboardingPurpose purpose, string email, Guid? userId,
        Guid? patientReference, string otpHash, DateTime now, DateTime expires, int maxAttempts) : base(id)
    {
        if (id == Guid.Empty || !Enum.IsDefined(purpose) || !PatientIdentityEmail.IsValid(email) ||
            string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otpHash) || expires <= now || maxAttempts < 1 ||
            purpose == PatientOnboardingPurpose.EmailChange && userId is null) throw new ArgumentException("Invalid challenge.");
        Purpose = purpose; NormalizedEmail = PatientIdentityEmail.Normalize(email)!; ApplicationUserId = userId;
        PatientReference = patientReference; OtpHash = otpHash; CreatedOnUtc = LastSentOnUtc = now;
        OtpExpiresOnUtc = expires; MaxAttempts = maxAttempts;
    }
    public PatientOnboardingPurpose Purpose { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public Guid? ApplicationUserId { get; private set; }
    public Guid? PatientReference { get; private set; }
    public string OtpHash { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; } = 5;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime OtpExpiresOnUtc { get; private set; }
    public DateTime LastSentOnUtc { get; private set; }
    public DateTime? VerifiedOnUtc { get; private set; }
    public string? ContinuationTokenHash { get; private set; }
    public DateTime? ContinuationTokenExpiresOnUtc { get; private set; }
    public PatientOnboardingAction? AllowedAction { get; private set; }
    public DateTime? ConsumedOnUtc { get; private set; }
    public DateTime? InvalidatedOnUtc { get; private set; }
    public string? IdempotencyFingerprint { get; private set; }
    public string? MutationIdempotencyKey { get; private set; }
    public string? MutationResponseJson { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool CanVerify(DateTime now) => now < OtpExpiresOnUtc && AttemptCount < MaxAttempts && VerifiedOnUtc is null && InvalidatedOnUtc is null;
    public void FailedAttempt(DateTime now) { if (CanVerify(now)) { AttemptCount++; if (AttemptCount >= MaxAttempts) InvalidatedOnUtc = now; } }
    public void Invalidate(DateTime now) => InvalidatedOnUtc ??= now;
    public void Verify(string tokenHash, PatientOnboardingAction action, DateTime now, DateTime expires)
    {
        if (!CanVerify(now) || expires <= now || string.IsNullOrEmpty(tokenHash)) throw new InvalidOperationException("Challenge cannot be verified.");
        VerifiedOnUtc = now; ContinuationTokenHash = tokenHash; ContinuationTokenExpiresOnUtc = expires; AllowedAction = action;
    }
    public bool CanUse(PatientOnboardingAction action, DateTime now, bool mutation = true) =>
        VerifiedOnUtc is not null && InvalidatedOnUtc is null && ContinuationTokenExpiresOnUtc > now && AllowedAction == action && (!mutation || ConsumedOnUtc is null);
    public void Consume(DateTime now, string fingerprint)
    {
        if (AllowedAction is not { } action || !CanUse(action, now)) throw new InvalidOperationException("Continuation cannot be consumed.");
        ConsumedOnUtc = now; IdempotencyFingerprint = fingerprint;
    }
    public void CompleteMutation(string key, string responseJson) { MutationIdempotencyKey = key; MutationResponseJson = responseJson; }
}
