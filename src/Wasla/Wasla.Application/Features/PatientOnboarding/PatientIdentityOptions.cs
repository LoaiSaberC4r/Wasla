namespace Wasla.Application.Features.PatientOnboarding;
public sealed class PatientIdentityOptions
{
    public int EmailOtpTtlMinutes { get; set; } = 10;
    public int EmailOtpMaxAttempts { get; set; } = 5;
    public int EmailOtpResendCooldownSeconds { get; set; } = 60;
    public int OtpLength { get; set; } = 6;
    public int VerifiedContinuationTtlMinutes { get; set; } = 30;
    public int ActivationInvitationTtlHours { get; set; } = 48;
    public int ClaimInactivityExpiryDays { get; set; } = 90;
    public int EvidenceAfterClosureRetentionDays { get; set; } = 90;
    public int AuditAfterClosureRetentionMonths { get; set; } = 24;
    public int PatientReuseAttestationTtlMinutes { get; set; } = 15;
    public int ExpiredChallengeCleanupHours { get; set; } = 24;
    public bool EvidencePurgingEnabled { get; set; }
    public bool AuditPurgingEnabled { get; set; }
    public int WorkerBatchSize { get; set; } = 50;
    public int WorkerIntervalSeconds { get; set; } = 300;
    public bool IsValid() => EmailOtpTtlMinutes is >= 1 and <= 30 && EmailOtpMaxAttempts is >= 1 and <= 10 &&
        EmailOtpResendCooldownSeconds is >= 60 and <= 3600 && OtpLength is >= 6 and <= 8 &&
        VerifiedContinuationTtlMinutes is >= 1 and <= 60 && ActivationInvitationTtlHours is >= 1 and <= 48 &&
        ClaimInactivityExpiryDays >= 90 && EvidenceAfterClosureRetentionDays >= 90 && AuditAfterClosureRetentionMonths >= 24 &&
        PatientReuseAttestationTtlMinutes is >= 1 and <= 15 && ExpiredChallengeCleanupHours >= 24 && WorkerBatchSize is >= 1 and <= 100 && WorkerIntervalSeconds >= 30;
}
