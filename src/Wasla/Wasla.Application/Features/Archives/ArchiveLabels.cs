namespace Wasla.Application.Features.Archives;

public static class ArchiveLabels
{
    public static string English(string code)
    {
        if (code.StartsWith("TicketHistory.", StringComparison.Ordinal) && int.TryParse(code.AsSpan(14), out var ticket))
            code = Enum.GetName((Wasla.Domain.Tickets.TicketHistoryEventType)ticket) ?? code;
        else if (code.StartsWith("ReservationHistory.", StringComparison.Ordinal) && int.TryParse(code.AsSpan(19), out var reservation))
            code = "Reservation: " + (Enum.GetName((Wasla.Domain.Reservations.ReservationHistoryEventType)reservation) ?? code);
        else if (code.StartsWith("DoctorStatus.", StringComparison.Ordinal) && int.TryParse(code.AsSpan(13), out var status))
            code = "Doctor status: " + (Enum.GetName((Wasla.Domain.Common.DoctorApprovalStatus)status) ?? code);
        var text = new System.Text.StringBuilder();
        for (var index = 0; index < code.Length; index++)
        {
            var value = code[index];
            if (value == '.') { text.Append(": "); continue; }
            if (index > 0 && char.IsUpper(value) && char.IsLower(code[index - 1])) text.Append(' ');
            text.Append(value);
        }
        return text.ToString();
    }
    private static readonly Dictionary<string,string> Labels = new(StringComparer.Ordinal)
    {
        ["Running"]="قيد التنفيذ",["Succeeded"]="نجح",["CompletedWithExceptions"]="اكتمل مع استثناءات",["Failed"]="فشل",["Paused"]="متوقف مؤقتاً",
        ["UnverifiedDayClosure"]="تعذر التحقق من انتهاء اليوم",["MissingPayment"]="دفعة مفقودة",["PaymentMismatch"]="عدم تطابق الدفعة",
        ["EncounterMismatch"]="عدم تطابق الزيارة",["HistoryMismatch"]="عدم تطابق السجل",["ReservationMismatch"]="عدم تطابق الحجز",["DatabaseFailure"]="تعذر إكمال معاملة قاعدة البيانات",
        ["Waiting"]="انتظار",["Called"]="تم النداء",["InProgress"]="قيد الخدمة",["NoShow"]="لم يحضر",["Completed"]="مكتمل",["Cancelled"]="ملغى",
        ["Active"]="نشط",["Expired"]="منتهي",["ConvertedToTicket"]="تحول إلى تذكرة",["Ticket"]="تذكرة",["Reservation"]="حجز",["Encounter"]="زيارة",
        ["MedicalAccess"]="وصول للسجل الطبي",["Financial"]="عملية مالية",["Handover"]="تسليم",["Investigation"]="تحقيق",["Export"]="تصدير",
        ["ArchiveControl"]="التحكم في الأرشفة",["ArchiveLookup"]="طلب وصول للأرشيف",["ArchiveLookupDenied"]="رفض طلب وصول للأرشيف",["ArchiveControlRejected"]="رفض طلب تحكم بالأرشفة",["OperationalDay"]="يوم العمل",["Governance"]="الحوكمة",["Allowed"]="مسموح",["Denied"]="مرفوض",
        ["TicketArchived"]="أرشفة تذكرة",["ReservationArchived"]="أرشفة حجز",["ArchivedTicketOpened"]="فتح تذكرة مؤرشفة",["ArchiveFailed"]="فشل الأرشفة",
        ["ArchivePaused"]="إيقاف الأرشفة مؤقتاً",["ArchiveResumed"]="استئناف الأرشفة",["RestrictedAuditSearched"]="بحث مبرر في سجل الوصول المقيد",
        ["NoShowFinalized"]="تثبيت عدم الحضور بعد انتهاء اليوم",["OperationalDayObserved"]="تسجيل نهاية يوم العمل",["OperationalDayRevised"]="تعديل نهاية يوم العمل",
        ["OperationalDayEvidenceInvalidated"]="تعذر اعتماد نهاية يوم العمل",["IdentityMismatch"]="عدم تطابق الهوية الدائمة",
        ["PermissionGranted"]="منح صلاحية",["PermissionRevoked"]="سحب صلاحية",["DelegationCreated"]="إنشاء تفويض",["DelegationChanged"]="تعديل تفويض",
        ["DelegationDeactivated"]="تعطيل تفويض",["DelegationGranted"]="منح صلاحية مفوضة",["DelegationRevoked"]="سحب صلاحية مفوضة",
        ["PaymentCorrected"]="تصحيح بيانات دفعة",["RefundRecorded"]="تسجيل رد دفعة",["RefundCorrected"]="تصحيح بيانات رد دفعة",["ReportExported"]="تصدير تقرير"
    };
    public static string Arabic(string code)
    {
        if (Labels.TryGetValue(code, out var label)) return label;
        if (code.StartsWith("TicketHistory.", StringComparison.Ordinal) && int.TryParse(code.AsSpan(14), out var ticket))
            return ticket switch { 1 => "إنشاء تذكرة", 2 => "تسجيل حضور حجز", 3 => "تسجيل حضور استثنائي", 4 => "حضور دون حجز", 5 => "نداء", 6 => "إعادة نداء", 7 => "لا استجابة للنداء", 8 => "نداء يدوي", 9 => "تثبيت عدم الحضور", 10 => "استعادة عدم الحضور", 11 => "منح أولوية", 12 => "بدء الزيارة", 13 => "إكمال التذكرة", 14 => "إلغاء التذكرة", 15 => "انتهاء يوم العمل", _ => "حدث تذكرة" };
        if (code.StartsWith("ReservationHistory.", StringComparison.Ordinal) && int.TryParse(code.AsSpan(19), out var reservation))
            return reservation switch { 1 => "إنشاء حجز", 2 => "إعادة جدولة حجز", 3 => "إلغاء حجز", 4 => "تثبيت عدم الحضور", 5 => "استعادة حجز", 6 => "انتهاء صلاحية الحجز", 7 => "تحويل حجز إلى تذكرة", _ => "حدث حجز" };
        return code.StartsWith("Encounter.", StringComparison.Ordinal) ? "وصول لزيارة طبية" :
            code.StartsWith("MedicalAccess.", StringComparison.Ordinal) ? "وصول لسجل طبي" :
            code.StartsWith("Handover.", StringComparison.Ordinal) ? "إجراء تسليم" :
            code.StartsWith("Investigation.", StringComparison.Ordinal) ? "إجراء تحقيق" :
            code.StartsWith("DoctorStatus.", StringComparison.Ordinal) ? "تغيير حالة اعتماد طبيب" : "حدث " + code;
    }
}
