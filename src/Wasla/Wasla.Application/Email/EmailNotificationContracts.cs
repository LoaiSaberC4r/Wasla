namespace Wasla.Application.Email;

using Wasla.Domain.Families;

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
    EmailNotificationContent SubscriptionLifecycle(string eventType, string doctorName, DateTime? endsAtUtc,
        DateTime? startsAtUtc = null, string? planCode = null, int? practiceLimit = null, int? receptionLimit = null, Guid? contractId = null, Guid? periodId = null, IReadOnlyList<string>? practiceNames = null, decimal? renewalPrice = null)
    {
        var title = eventType switch
        {
            "TrialStarted" => "بدأت الفترة التجريبية / Trial started",
            "TrialReminder" => "تذكير بانتهاء الفترة التجريبية / Trial reminder",
            "TrialExpired" => "انتهت الفترة التجريبية / Trial expired",
            "PaidReminder" => "تذكير بانتهاء الاشتراك / Subscription reminder",
            "Expired" => "انتهى الاشتراك / Subscription expired",
            "TrialExtended" => "تم تمديد الفترة التجريبية / Trial extended",
            _ => "تم تفعيل الاشتراك / Subscription activated"
        };
        var text = $"{title}\n{doctorName}\nالباقة / Plan: {planCode}\nتاريخ البداية / Starts (UTC): {startsAtUtc:O}\nتاريخ الانتهاء / Ends (UTC): {endsAtUtc:O}\nعدد العيادات / Practices: {practiceLimit}\nمقاعد الاستقبال / Reception seats: {receptionLimit}\nلا يوجد تجديد أو خصم تلقائي / No automatic renewal or charge.";
        text += $"\nContract: {contractId}\nPeriod: {periodId}\nLinked practices ({practiceNames?.Count ?? 0}): {string.Join(", ", practiceNames ?? [])}\nRenewal (EGP): {renewalPrice:0.00}";
        return new("Wasla | " + title, "<p dir=\"auto\">" + System.Net.WebUtility.HtmlEncode(text).Replace("\n", "<br/>", StringComparison.Ordinal) + "</p>", text);
    }
    EmailNotificationContent PasswordResetOtp(string otp, int expirationMinutes);
    EmailNotificationContent DoctorLifecycle(DoctorEmailEvent emailEvent, string doctorName, string? reason = null);
    EmailNotificationContent DoctorSpecializationModificationRequested(
        string doctorName,
        string message,
        int revisionNumber);
    EmailNotificationContent FamilyRelationshipStatus(
        string patientName,
        FamilyRelationshipRequestAction action,
        int revisionNumber,
        string? message = null);
    EmailNotificationContent ReceptionAccountCreated(string receptionName, string userName)
        => new(
            "Wasla | Reception Account Created",
            $"<p>A reception account was created for {receptionName} ({userName}).</p>",
            $"A reception account was created for {receptionName} ({userName}).");
}

