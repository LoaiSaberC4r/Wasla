using System.Globalization;
using System.Net;
using System.Text;
using BuildingBlock.Application.Time;
using Wasla.Domain.Families;

namespace Wasla.Application.Email;

internal sealed class BilingualEmailNotificationFactory(
    IDateTimeProvider clock,
    IEmailBrandingProvider branding)
    : IEmailNotificationFactory
{
    public EmailNotificationContent PasswordResetOtp(string otp, int expirationMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otp);
        var expiration = expirationMinutes.ToString(CultureInfo.InvariantCulture);
        return Build(
            "Wasla | رمز إعادة تعيين كلمة المرور | Password Reset Code",
            [
                "تلقينا طلبًا لإعادة تعيين كلمة المرور لحسابك في وصلة.",
                $"رمز التحقق: {otp}",
                $"تنتهي صلاحية الرمز خلال {expiration} دقائق.",
                "إذا لم تطلب إعادة تعيين كلمة المرور، تجاهل هذه الرسالة."
            ],
            [
                "We received a request to reset your Wasla account password.",
                $"Verification code: {otp}",
                $"This code expires in {expiration} minutes.",
                "If you did not request a password reset, you can ignore this email."
            ]);
    }

    public EmailNotificationContent DoctorLifecycle(
        DoctorEmailEvent emailEvent,
        string doctorName,
        string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(doctorName);
        return emailEvent switch
        {
            DoctorEmailEvent.RegistrationReceived => Build(
                "Wasla | تم استلام طلب التسجيل | Registration Received",
                [$"مرحبًا د. {doctorName}،", "تم استلام طلب تسجيلك وهو الآن قيد المراجعة."],
                [$"Hello Dr. {doctorName},", "Your registration was received and is now pending review."]),
            DoctorEmailEvent.Approved => Build(
                "Wasla | تم اعتماد حساب الطبيب | Doctor Account Approved",
                [$"مرحبًا د. {doctorName}،", "تم اعتماد حسابك كطبيب على وصلة."],
                [$"Hello Dr. {doctorName},", "Your doctor account on Wasla has been approved."]),
            DoctorEmailEvent.Rejected => BuildWithReason(
                "Wasla | تحديث طلب التسجيل | Registration Update",
                doctorName,
                "تم رفض طلب تسجيلك.",
                "Your registration was rejected.",
                reason),
            DoctorEmailEvent.Suspended => BuildWithReason(
                "Wasla | تم تعليق حساب الطبيب | Doctor Account Suspended",
                doctorName,
                "تم تعليق حسابك كطبيب.",
                "Your doctor account has been suspended.",
                reason),
            DoctorEmailEvent.Reactivated => Build(
                "Wasla | تمت إعادة تفعيل حساب الطبيب | Doctor Account Reactivated",
                [$"مرحبًا د. {doctorName}،", "تمت إعادة تفعيل حسابك كطبيب."],
                [$"Hello Dr. {doctorName},", "Your doctor account has been reactivated."]),
            _ => throw new ArgumentOutOfRangeException(nameof(emailEvent))
        };
    }

    public EmailNotificationContent DoctorSpecializationModificationRequested(
        string doctorName,
        string message,
        int revisionNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(doctorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var revision = revisionNumber.ToString(CultureInfo.InvariantCulture);
        return Build(
            "Wasla | مطلوب تعديل التخصصات الطبية | Specialization Changes Requested",
            [
                $"مرحبًا د. {doctorName}،",
                $"مطلوب تعديل طلب التخصصات الطبية للإصدار رقم {revision}.",
                $"رسالة المراجعة: {message}"
            ],
            [
                $"Hello Dr. {doctorName},",
                $"Changes were requested for medical-specialization revision {revision}.",
                $"Review message: {message}"
            ]);
    }

    public EmailNotificationContent FamilyRelationshipStatus(
        string patientName,
        FamilyRelationshipRequestAction action,
        int revisionNumber,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patientName);
        var revision = revisionNumber.ToString(CultureInfo.InvariantCulture);
        return action switch
        {
            FamilyRelationshipRequestAction.Approved => Build(
                "Wasla | تم اعتماد علاقة الأسرة | Family Relationship Approved",
                [$"مرحبًا {patientName}،", $"تم اعتماد طلب علاقة الأسرة للإصدار رقم {revision}."],
                [$"Hello {patientName},", $"Your family relationship request revision {revision} was approved."]),
            FamilyRelationshipRequestAction.Rejected => Build(
                "Wasla | تم رفض علاقة الأسرة | Family Relationship Rejected",
                [$"مرحبًا {patientName}،", $"تم رفض طلب علاقة الأسرة للإصدار رقم {revision}.", $"السبب: {message}"],
                [$"Hello {patientName},", $"Your family relationship request revision {revision} was rejected.", $"Reason: {message}"]),
            FamilyRelationshipRequestAction.ModificationRequested => Build(
                "Wasla | مطلوب تعديل إثبات علاقة الأسرة | Family Relationship Changes Requested",
                [$"مرحبًا {patientName}،", $"مطلوب تعديل طلب علاقة الأسرة للإصدار رقم {revision}.", $"رسالة المراجعة: {message}"],
                [$"Hello {patientName},", $"Changes are required for family relationship request revision {revision}.", $"Review message: {message}"]),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private EmailNotificationContent BuildWithReason(
        string subject,
        string doctorName,
        string arabicMessage,
        string englishMessage,
        string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return Build(
            subject,
            [$"مرحبًا د. {doctorName}،", arabicMessage, $"السبب: {reason}"],
            [$"Hello Dr. {doctorName},", englishMessage, $"Reason: {reason}"]);
    }

    private EmailNotificationContent Build(
        string subject,
        IEnumerable<string> arabicParagraphs,
        IEnumerable<string> englishParagraphs)
    {
        var html = new StringBuilder(2048)
            .Append("<!doctype html><html><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;color:#222;line-height:1.7\"><div style=\"max-width:680px;margin:0 auto\">")
            .Append("<header><h2>Wasla | وصلة</h2></header><section dir=\"rtl\" lang=\"ar\" style=\"text-align:right\">")
            .AppendJoin(string.Empty, arabicParagraphs.Select(Paragraph))
            .Append("</section><hr style=\"border:0;border-top:1px solid #bbb;margin:28px 0\">")
            .Append("<section dir=\"ltr\" lang=\"en\" style=\"text-align:left\">")
            .AppendJoin(string.Empty, englishParagraphs.Select(Paragraph))
            .Append("</section><footer style=\"margin-top:28px;color:#666\">Wasla | وصلة<br>&copy; ")
            .Append(clock.UtcNow.Year)
            .Append("</footer><table role=\"presentation\" width=\"100%\" style=\"margin-top:24px\"><tr><td align=\"center\"><img src=\"")
            .Append(WebUtility.HtmlEncode(branding.FooterImageUrl))
            .Append("\" alt=\"Wasla\" width=\"700\" style=\"display:block;width:100%;max-width:700px;height:auto;border:0\"></td></tr></table></div></body></html>")
            .ToString();
        return new EmailNotificationContent(subject, html);
    }

    private static string Paragraph(string value) => $"<p>{WebUtility.HtmlEncode(value)}</p>";
}
