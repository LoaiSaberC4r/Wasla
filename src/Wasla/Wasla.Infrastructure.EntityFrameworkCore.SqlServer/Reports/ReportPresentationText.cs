using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public static class ReportPresentationText
{
    public static string Unit(string unit, bool ar) => unit switch
    { "percent" => "%", "count" => ar ? "عدد" : "count", "minutes" => ar ? "دقيقة" : "minutes", "EGP" => ar ? "جنيه" : "EGP", _ => unit };
    public static string Status(string status, bool ar) => !ar ? status : status switch
    {
        "Complete" => "مكتمل", "Partial" => "جزئي", "NotAvailable" => "غير متاح", "NotMeasured" => "غير مقاس",
        "NotInstrumented" => "غير مسجل", "Stale" => "قديم", "Active" => "نشط", "Cancelled" => "ملغى", "NoShow" => "لم يحضر",
        "Waiting" => "انتظار", "Called" => "تم النداء", "InProgress" => "الخدمة جارية", "Completed" => "مكتمل تشغيليًا",
        "Unpaid" => "غير مسدد", "Paid" => "مسدد", "Settlement" => "سداد", "Reversal" => "عكس سداد", "Payment" => "تحصيل",
        "Refund" => "استرداد", "Pending" => "قيد المراجعة", "Approved" => "معتمد", "Rejected" => "مرفوض", "Suspended" => "موقوف", _ => status
    };
    public static string Resource(string resource, bool ar) => !ar ? resource : resource switch
    { "Reservation" => "حجز", "Ticket" => "تذكرة", "Doctor" => "طبيب", "OperatingObligation" => "التزام", "OperatingSettlement" => "سداد",
        "OperatingReversal" => "عكس سداد", "Payment" => "تحصيل", "Refund" => "استرداد", _ => resource };
    public static string MetricValue(ReportMetric m) => m.Value?.ToString(m.Unit == "EGP" ? "#,##0.00" : "#,##0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-";
    public static IReadOnlyList<(string Label, string Value)> Filters(ReportPresentation p)
    {
        var f = p.Report.AppliedFilters; List<(string, string)> result = [];
        void Add(string en, string ar, object? value) { if (value != null) result.Add((p.IsArabic ? ar : en, value.ToString()!)); }
        Add("Practice", "العيادة", f.PracticeId); Add("Doctor", "الطبيب", f.DoctorId); Add("Segment", "الفئة", f.SegmentId);
        Add("Visit type", "نوع الزيارة", f.VisitTypeId); Add("Booking source", "مصدر الحجز", f.BookingSource);
        Add("Reservation status", "حالة الحجز", f.ReservationStatus == null ? null : Status(f.ReservationStatus.ToString()!, p.IsArabic));
        Add("Ticket status", "حالة التذكرة", f.TicketStatus == null ? null : Status(f.TicketStatus.ToString()!, p.IsArabic));
        Add("Expense category", "فئة المصروف", f.ExpenseCategory); Add("Reception", "موظف الاستقبال", f.ReceptionId); Add("Plan", "الخطة", f.PlanId);
        Add("Grouping", "التجميع", f.Grouping); if (f.ComparePreviousPeriod) Add("Comparison", "المقارنة", p.IsArabic ? "الفترة السابقة المساوية" : "Preceding equal period");
        return result;
    }
}
