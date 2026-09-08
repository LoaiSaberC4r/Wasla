namespace Wasla.Application.Email;

public sealed record EmailNotificationContent(string Subject, string HtmlBody, string? TextBody = null);

public interface IEmailBrandingProvider
{
    string FooterImageUrl { get; }
}

public enum DoctorEmailEvent
{
    RegistrationReceived = 1,
    Approved = 2,
    Rejected = 3,
    Suspended = 4,
    Reactivated = 5
}

public interface IEmailNotificationFactory
{
    EmailNotificationContent PasswordResetOtp(string otp, int expirationMinutes);
    EmailNotificationContent DoctorLifecycle(DoctorEmailEvent emailEvent, string doctorName, string? reason = null);
    EmailNotificationContent DoctorSpecializationModificationRequested(
        string doctorName,
        string message,
        int revisionNumber);
}

