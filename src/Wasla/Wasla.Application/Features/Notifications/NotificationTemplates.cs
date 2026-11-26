using Wasla.Domain.Notifications;

namespace Wasla.Application.Features.Notifications;

public sealed record NotificationText(string Title, string Message);
public static class NotificationTemplates
{
    public static NotificationText Render(string code, int version, string? language = null)
    {
        if (!NotificationPolicy.KnownEvent(code) || version != NotificationPolicy.TemplateVersion) throw new ArgumentException("Notification.TemplateUnavailable");
        var english = NotificationPolicy.Language(language) == "en";
        var title = code switch
        {
            "Reservation.Created" => english ? "Reservation confirmed" : "تم تأكيد الحجز",
            "Reservation.Rescheduled" => english ? "Reservation rescheduled" : "تمت إعادة جدولة الحجز",
            "Reservation.Cancelled" or "Doctor.BookingCancelled" => english ? "Reservation cancelled" : "تم إلغاء الحجز",
            "Appointment.ReminderDue" => english ? "Appointment reminder" : "تذكير بموعد حجزك",
            "Reservation.RestoredFromNoShow" => english ? "Reservation restored" : "تمت استعادة الحجز",
            "Reservation.MarkedNoShow" => english ? "Attendance update" : "تحديث حالة الحضور",
            "Reservation.Expired" => english ? "Reservation expired" : "انتهى الحجز",
            "Patient.CheckedIn" => english ? "Check-in confirmed" : "تم تأكيد الحضور",
            "Doctor.NewBooking" => english ? "New booking" : "حجز جديد",
            "Doctor.Approved" => english ? "Account approved" : "تم اعتماد الحساب",
            "Doctor.Rejected" => english ? "Account review update" : "تحديث مراجعة الحساب",
            "Doctor.Suspended" => english ? "Account suspended" : "تم تعليق الحساب",
            _ => english ? "Medical update available" : "تحديث طبي متاح"
        };
        var message = NotificationPolicy.Clinical(code)
            ? english ? "An update is available in your Wasla medical information. Sign in to review information currently available to you." : "تم إتاحة تحديث ضمن معلوماتك الطبية على وصلة. سجّل الدخول لعرض المعلومات المتاحة لك."
            : english ? "An update is available on Wasla." : "يتوفر تحديث جديد على وصلة.";
        return new(title, message);
    }
}
