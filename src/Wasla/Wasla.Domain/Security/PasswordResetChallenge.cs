using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public sealed class PasswordResetChallenge : AggregateRoot<Guid>
{
    private PasswordResetChallenge()
    {
    }

    private PasswordResetChallenge(
        Guid id,
        Guid applicationUserId,
        string otpHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
        : base(id)
    {
        ApplicationUserId = applicationUserId;
        OtpHash = otpHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid ApplicationUserId { get; private set; }
    public string OtpHash { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public int FailedAttemptCount { get; private set; }
    public DateTime? VerifiedOnUtc { get; private set; }
    public DateTime? InvalidatedOnUtc { get; private set; }
    public DateTime? ConsumedOnUtc { get; private set; }
    public string? ResetTokenHash { get; private set; }
    public DateTime? ResetTokenExpiresOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public bool IsVerified => VerifiedOnUtc.HasValue;
    public bool IsConsumed => ConsumedOnUtc.HasValue;
    public bool IsInvalidated => InvalidatedOnUtc.HasValue;

    public static Result<PasswordResetChallenge> Create(
        Guid id,
        Guid applicationUserId,
        string otpHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        var created = RequireUtc(createdOnUtc);
        var expires = RequireUtc(expiresOnUtc);
        if (id == Guid.Empty || applicationUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(otpHash) || otpHash.Length > 128 || expires <= created)
        {
            return Result<PasswordResetChallenge>.Fail(PasswordResetErrors.ConfigurationInvalid);
        }

        return Result<PasswordResetChallenge>.Ok(new PasswordResetChallenge(
            id,
            applicationUserId,
            otpHash,
            created,
            expires));
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresOnUtc <= RequireUtc(nowUtc);

    public bool CanVerify(DateTime nowUtc, int maximumVerificationAttempts)
        => maximumVerificationAttempts > 0 &&
           !IsInvalidated &&
           !IsConsumed &&
           !IsVerified &&
           !IsExpired(nowUtc) &&
           FailedAttemptCount < maximumVerificationAttempts;

    public Result RegisterFailedAttempt(DateTime nowUtc, int maximumVerificationAttempts)
    {
        if (!CanVerify(nowUtc, maximumVerificationAttempts))
        {
            return Result.Fail(GetVerificationFailure(nowUtc, maximumVerificationAttempts));
        }

        FailedAttemptCount++;
        if (FailedAttemptCount >= maximumVerificationAttempts)
        {
            InvalidatedOnUtc = RequireUtc(nowUtc);
        }

        return Result.Ok();
    }

    public Result Verify(DateTime nowUtc, int maximumVerificationAttempts)
    {
        if (!CanVerify(nowUtc, maximumVerificationAttempts))
        {
            return Result.Fail(GetVerificationFailure(nowUtc, maximumVerificationAttempts));
        }

        VerifiedOnUtc = RequireUtc(nowUtc);
        return Result.Ok();
    }

    public Result IssueResetToken(string resetTokenHash, DateTime expiresOnUtc, DateTime nowUtc)
    {
        var expires = RequireUtc(expiresOnUtc);
        if (!IsVerified || IsInvalidated || IsConsumed || ResetTokenHash is not null ||
            string.IsNullOrWhiteSpace(resetTokenHash) || resetTokenHash.Length > 128 ||
            expires <= RequireUtc(nowUtc))
        {
            return Result.Fail(PasswordResetErrors.ResetTokenInvalid);
        }

        ResetTokenHash = resetTokenHash;
        ResetTokenExpiresOnUtc = expires;
        return Result.Ok();
    }

    public bool CanReset(DateTime nowUtc)
        => IsVerified && !IsInvalidated && !IsConsumed &&
           ResetTokenHash is not null && ResetTokenExpiresOnUtc > RequireUtc(nowUtc);

    public Result Invalidate(DateTime nowUtc)
    {
        if (IsConsumed)
        {
            return Result.Fail(PasswordResetErrors.ChallengeConsumed);
        }

        InvalidatedOnUtc ??= RequireUtc(nowUtc);
        return Result.Ok();
    }

    public Result Consume(DateTime nowUtc)
    {
        if (!CanReset(nowUtc))
        {
            return Result.Fail(IsConsumed
                ? PasswordResetErrors.ChallengeConsumed
                : PasswordResetErrors.ResetTokenInvalid);
        }

        ConsumedOnUtc = RequireUtc(nowUtc);
        return Result.Ok();
    }

    private Error GetVerificationFailure(DateTime nowUtc, int maximumVerificationAttempts)
        => IsExpired(nowUtc)
            ? PasswordResetErrors.OtpExpired
            : FailedAttemptCount >= maximumVerificationAttempts || IsInvalidated
                ? PasswordResetErrors.TooManyAttempts
                : PasswordResetErrors.OtpInvalid;

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public static class PasswordResetErrors
{
    public static Error OtpInvalid => Error.Validation("Auth.PasswordReset.InvalidOtp", ErrorMessage.OtpInvalid);
    public static Error OtpExpired => Error.Validation("Auth.PasswordReset.OtpExpired", ErrorMessage.OtpExpired);
    public static Error TooManyAttempts => Error.RateLimit(
        "Auth.PasswordReset.TooManyAttempts",
        ErrorMessage.TooManyAttempts,
        TimeSpan.Zero);
    public static Error ResetTokenInvalid => Error.Validation(
        "Auth.PasswordReset.ResetTokenInvalid",
        ErrorMessage.ResetTokenInvalid);
    public static Error ResetTokenExpired => Error.Validation(
        "Auth.PasswordReset.ResetTokenExpired",
        ErrorMessage.ResetTokenExpired);
    public static Error ChallengeConsumed => Error.Validation(
        "Auth.PasswordReset.ChallengeConsumed",
        ErrorMessage.PasswordResetChallengeConsumed);
    public static Error ConfigurationInvalid => Error.Domain(
        "Auth.PasswordReset.ConfigurationInvalid",
        ErrorMessage.PasswordResetConfigurationInvalid);
}
