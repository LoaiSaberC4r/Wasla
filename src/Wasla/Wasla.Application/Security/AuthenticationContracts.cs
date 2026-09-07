using Wasla.Domain.Common;

namespace Wasla.Application.Security;

public sealed record AccessTokenDescriptor(
    Guid ApplicationUserId,
    string UserName,
    string Email,
    UserType UserType,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    bool PasswordChangeRequired,
    Guid? DoctorId,
    Guid? PatientId);

public sealed record GeneratedAccessToken(string AccessToken, DateTime ExpiresOnUtc);

public interface IAccessTokenService
{
    GeneratedAccessToken Create(AccessTokenDescriptor descriptor);
}

public interface IPasswordResetProtector
{
    string GenerateOtp(int length);
    string GenerateResetToken();
    string HashOtp(string otp);
    string HashResetToken(string resetToken);
    bool VerifyOtp(string otp, string hash);
    bool VerifyResetToken(string resetToken, string hash);
}

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";
    public int OtpLength { get; set; } = 6;
    public int OtpExpirationMinutes { get; set; } = 5;
    public int MaximumVerificationAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int ResetTokenExpirationMinutes { get; set; } = 10;
    public string HmacSecret { get; set; } = string.Empty;
}

