using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Patients;

public static class PatientIdentityErrors
{
    public static Error Conflict(string code) => Error.Conflict(code, ErrorMessage.PatientIdentityConflict);
    public static Error Invalid => Error.Validation("Patient.Invalid", ErrorMessage.PatientIdentityInvalid);
    public static Error Proof => Error.Domain("PatientClaims.ProofInsufficient", ErrorMessage.PatientIdentityProofRequired);
    public static Error Age(string code) => Error.Domain(code, ErrorMessage.PatientIdentityAgeRestricted);
    public static Error Denied => Error.Security("PatientClaims.AccessDenied", ErrorMessage.PatientIdentityAccessDenied);
    public static Error NotFound => Error.NotFound("PatientClaims.NotFound", ErrorMessage.PatientIdentityNotFound);
    public static Error Otp => Error.Validation("PatientOnboarding.OtpInvalidOrExpired", ErrorMessage.PatientIdentityOtpInvalid);
    public static Error Limited => Error.RateLimit("PatientIdentity.RateLimited", ErrorMessage.PatientIdentityRateLimited, TimeSpan.FromSeconds(60));
}

public static class PatientIdentityEmail
{
    public static string? Normalize(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    public static bool IsValid(string? email) => email is null || email.Length <= 200 &&
        System.Net.Mail.MailAddress.TryCreate(email, out var address) && address.Address == email && !email.Any(char.IsWhiteSpace);
    public static string? Mask(string? email)
    {
        if (string.IsNullOrEmpty(email)) return null;
        var at = email.IndexOf('@');
        return at > 0 ? email[..1] + "***" + email[at..] : "***";
    }
    public static string? MaskPhone(string? phone) => string.IsNullOrEmpty(phone) ? null : "***" + phone[^Math.Min(3, phone.Length)..];
}
