using System.Globalization;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

// Public only because the physical Razor template is compiled into a separate assembly.
public sealed record PdfPrintSpec(string QuestionEn, string QuestionAr, string[] Headlines, PdfPrintSource[] Sources);
public sealed record PdfPrintSource(string TitleEn, string TitleAr, string Source, string[] Codes,
    string CaptionEn, string CaptionAr, bool Table = false, bool Optional = false);
public sealed record PdfPrintPoint(string Group, ReportMetric Metric);
public sealed record PdfPrintSection(string Title, string Caption, string Source, IReadOnlyList<PdfPrintPoint> Points,
    bool Temporal, bool Table, string? ChartDataUrl);
public sealed record PdfPrintDocument(string Question, IReadOnlyList<ReportMetric> Cards,
    IReadOnlyList<PdfPrintSection> Sections, IReadOnlyList<string> QualityNotes);

public static class PdfPrintPresentation
{
    private static PdfPrintSource S(string en, string ar, string source, string codes, string captionEn, string captionAr,
        bool table = false, bool optional = false) => new(en, ar, source, codes.Split(' ', StringSplitOptions.RemoveEmptyEntries), captionEn, captionAr, table, optional);
    private static readonly PdfPrintSource BookingTrend = S("Booking creation and scheduled appointments", "إنشاء الحجوزات والمواعيد المجدولة", "Series", "BookingsCreated AppointmentsScheduled",
        "Creation dates and scheduled dates are distinct bases; these are not a conversion cohort.", "تاريخ الإنشاء يختلف عن تاريخ الموعد؛ هذه ليست مجموعة لقياس التحويل.");
    private static readonly PdfPrintSource QueueTrend = S("Check-ins and operational completions", "الحضور واكتمال التذاكر تشغيليًا", "Series", "CheckIns TicketsCompleted",
        "Check-in and completion event dates; ticket completion is separate from clinical completion.", "تواريخ الحضور وأحداث اكتمال التذاكر؛ الاكتمال التشغيلي مستقل عن الاكتمال السريري.");
    private static readonly PdfPrintSource Funnel = S("Booking creation cohort", "مجموعة الحجوزات المنشأة", "BookingCreationCohortFunnel", "BookingsCreated CheckIns TicketsCompleted",
        "Only stages reported inside this same creation cohort; drop-off does not establish no-show or cancellation.", "المراحل المسجلة داخل مجموعة الإنشاء نفسها فقط؛ الفرق لا يثبت عدم الحضور أو الإلغاء.");
    private static readonly PdfPrintSource Durations = S("Waiting and service intervals", "فترات الانتظار والخدمة", "Metrics", "WaitToFirstCall WaitToServiceStart ServiceDuration",
        "Separate reported intervals, in minutes; eligible and excluded samples are retained in the appendix.", "فترات مستقلة بالدقائق؛ العينات المؤهلة والمستبعدة محفوظة في الملحق.");
    private static readonly PdfPrintSource BookingCategories = S("Booking creation and cancellation events", "إنشاء الحجوزات وأحداث الإلغاء", "BookingWeekday:|BookingHour:|CancellationReason:|CancellationInitiator:", "",
        "Weekday/hour describe booking creation, not appointment demand. Cancellation groups are event counts.", "اليوم والساعة يصفان إنشاء الحجز وليس وقت الموعد؛ مجموعات الإلغاء تعد أحداثًا.", table: true);
    private static readonly PdfPrintSource Balances = S("Period obligations and snapshot balances", "التزامات الفترة وأرصدة اللقطة", "Metrics", "OutstandingDueInPeriod AllOutstandingAsOf OverdueAsOf ProjectedResultForPeriod",
        "Due in period uses the selected due dates. All outstanding and overdue are as-of stocks, not daily cash flows. Projection is supplied by the report.", "مستحقات الفترة حسب تواريخ الاستحقاق المختارة؛ الإجمالي والمتأخر أرصدة عند اللقطة وليست تدفقات يومية. النتيجة المتوقعة واردة من التقرير.", table: true);
    private static readonly PdfPrintSource Approval = S("Approval-state distribution", "توزيع حالات الاعتماد", "Approval:", "",
        "Reported approval states are snapshot counts, not transitions or proof of bookability.", "حالات الاعتماد أعداد عند اللقطة وليست انتقالات أو إثباتًا لإتاحة الحجز.");

    public static PdfPrintSpec? Find(ReportActor actor, string code) => (actor, code) switch
    {
        (ReportActor.Doctor, "overview") => new("How are clinical operations performing?", "كيف يسير التشغيل السريري؟",
            ["AppointmentsScheduled", "CheckIns", "ReservationToTicketConversion", "CancellationRateScheduledCohort", "ReservationNoShowRate"],
            [S("Scheduled appointments and check-ins", "المواعيد المجدولة والحضور", "Series", "AppointmentsScheduled CheckIns", "Distinct scheduled-date and check-in-date activity; not an official conversion ratio.", "نشاط بتاريخ الموعد وبتاريخ الحضور؛ ليس نسبة التحويل الرسمية."), Funnel,
             S("Additional reported measures", "مقاييس إضافية مسجلة", "Metrics", "WaitToFirstCall WaitToServiceStart ServiceDuration NetCollected", "Measures retain their own units and source definitions.", "لكل مقياس وحدته وتعريفه الأصلي.", table: true, optional: true)]),
        (ReportActor.Doctor, "reservations") => new("How do bookings progress to attendance?", "كيف تنتقل الحجوزات إلى الحضور؟",
            ["BookingsCreated", "AppointmentsScheduled", "ReservationToTicketConversion", "CancellationRateScheduledCohort", "ReservationNoShowRate"], [BookingTrend, Funnel, BookingCategories]),
        (ReportActor.Doctor, "queue") => Queue(),
        (ReportActor.Doctor, "patients") => new("Who returns to this Doctor's care?", "من يعود للرعاية لدى هذا الطبيب؟",
            ["UniqueDoctorPatients", "NewToDoctorPatients", "ReturningDoctorPatients", "PatientRetention90d", "ClinicalWorkflowCompleted"],
            [S("New and returning Doctor patients", "مرضى الطبيب الجدد والعائدون", "Metrics", "NewToDoctorPatients ReturningDoctorPatients", "Doctor-scoped visit cohort counts; 90-day retention is a separate ratio.", "أعداد مجموعة زيارات الطبيب؛ الاحتفاظ خلال ٩٠ يومًا نسبة مستقلة."),
             S("Follow-up and clinical completion", "المتابعة والاكتمال السريري", "Metrics", "FollowUpActivity ClinicalWorkflowCompleted", "Clinical workflow completion is independent of ticket completion.", "اكتمال المسار السريري مستقل عن اكتمال التذكرة.", table: true)]),
        (ReportActor.Doctor, "practice-comparison") => new("How do authorized practices compare?", "كيف تقارن العيادات المصرح بها؟",
            ["AppointmentsScheduled", "CheckIns", "WaitToFirstCall", "ReservationToTicketConversion", "NetCollected"],
            [S("Practice comparison matrix", "مصفوفة مقارنة العيادات", "Practices", "AppointmentsScheduled CheckIns WaitToFirstCall ReservationToTicketConversion NetCollected", "Each practice retains its own unit, cohort and denominator. A single practice is shown without a peer comparison.", "لكل عيادة وحدتها ومجموعتها ومقامها؛ العيادة الواحدة تعرض دون مقارنة مع غيرها.", table: true)]),
        (ReportActor.Doctor, "operating-finance") => new("What cash moved during the report period?", "ما التدفقات النقدية خلال فترة التقرير؟",
            ["GrossCollected", "Refunds", "NetCollected", "PaidOperatingExpenses", "NetOperatingCashResult"],
            [S("Recorded operating cash flows", "التدفقات النقدية التشغيلية المسجلة", "Series", "NetCollected PaidOperatingExpenses", "Net collected − paid operating expenses = reported net operating cash result; not accounting profit or bank balance.", "صافي المتحصلات − المصروفات المدفوعة = نتيجة النقد التشغيلي المسجلة؛ ليست الربح المحاسبي أو رصيد البنك."), Balances]),
        (ReportActor.Doctor, "expenses") => Finance("Which operating expenses are paid or outstanding?", "ما المصروفات المدفوعة وغير المسددة؟", "Recorded expense movements", "حركات المصروفات المسجلة"),
        (ReportActor.Doctor, "salary") => Finance("Which salary obligations are settled or due?", "ما التزامات الرواتب المسددة والمستحقة؟", "Recorded salary settlements", "تسويات الرواتب المسجلة"),
        (ReportActor.Doctor, "recurring-expenses") => Finance("Which recurring obligations are settled or due?", "ما الالتزامات الدورية المسددة والمستحقة؟", "Recorded recurring payments", "المدفوعات الدورية المسجلة"),
        (ReportActor.Reception, "overview") => new("How are assigned practice operations progressing?", "كيف يسير تشغيل العيادات المفوضة؟",
            ["AppointmentsScheduled", "CheckIns", "TicketsCompleted", "WaitToFirstCall", "WaitToServiceStart"],
            [S("Assigned practice activity", "نشاط العيادات المفوضة", "Series", "AppointmentsScheduled CheckIns TicketsCompleted", "Scheduled, check-in and operational completion dates have distinct bases.", "تواريخ الموعد والحضور والاكتمال التشغيلي لها أسس مختلفة."), Durations,
             S("Handover events", "أحداث التسليم", "Metrics", "HandoversCreated HandoverAcknowledgements HandoversClosed OpenHandovers", "Recorded events and open snapshot counts are separate measures.", "الأحداث المسجلة وأعداد اللقطة المفتوحة مقاييس مستقلة.", table: true, optional: true)]),
        (ReportActor.Reception, "reservations") => new("When are bookings created and how do they progress?", "متى تنشأ الحجوزات وكيف تتقدم؟",
            ["BookingsCreated", "AppointmentsScheduled", "ReservationToTicketConversion", "CancellationRateScheduledCohort", "ReservationNoShowRate"],
            [S("Booking creation weekday", "يوم إنشاء الحجز", "BookingWeekday:", "", "Creation weekday, not busiest appointment day; full hour distribution is in the appendix.", "يوم إنشاء الحجز وليس أكثر أيام المواعيد ازدحامًا؛ توزيع الساعات الكامل في الملحق."), Funnel, BookingTrend]),
        (ReportActor.Reception, "queue") => Queue(),
        (ReportActor.Reception, "activity") => new("What operational events were recorded?", "ما الأحداث التشغيلية المسجلة؟", ["ReceptionActivity"],
            [S("Recorded reception events", "أحداث الاستقبال المسجلة", "Metrics", "ReceptionActivity", "Event counts are not patients served or employee productivity. Authorized event details follow in the appendix.", "عدد الأحداث لا يمثل المرضى المخدومين أو إنتاجية الموظف؛ التفاصيل المصرح بها في الملحق.", table: true),
             S("Dated reception activity", "نشاط الاستقبال المؤرخ", "Series", "ReceptionActivity", "Only a directly reported reception-activity series is shown.", "يعرض فقط تسلسل نشاط الاستقبال الوارد مباشرة من المصدر.", optional: true)]),
        (ReportActor.SuperAdmin, "overview") => new("What is the authorized platform lifecycle snapshot?", "ما لقطة مراحل المنصة المصرح بها؟",
            ["RegisteredDoctors", "PendingDoctors", "ApprovedDoctors", "ActivePractices", "CurrentBookableDoctors"], [Approval]),
        (ReportActor.SuperAdmin, "doctor-lifecycle") => new("What are the recorded Doctor approval states?", "ما حالات اعتماد الأطباء المسجلة؟",
            ["RegisteredDoctors", "PendingDoctors", "ApprovedDoctors", "RejectedDoctors", "SuspendedDoctors"], [Approval,
             S("Lifecycle events and waiting age", "أحداث دورة الاعتماد وعمر الانتظار", "Metrics", "DoctorSuspensions DoctorReactivations PendingApprovalAgeMinutes", "Suspensions/reactivations are recorded events; pending approval age is minutes.", "الإيقاف وإعادة التفعيل أحداث مسجلة؛ عمر انتظار الاعتماد بالدقائق.", table: true)]),
        (ReportActor.SuperAdmin, "adoption") => new("What acquisition and cohort activity is measured?", "ما الاكتساب ونشاط المجموعات المقاس؟",
            ["DoctorAcquisition", "PatientAcquisition", "DoctorRetention", "PatientRetention", "FeatureBookingDoctors"],
            [S("Doctor registration cohorts", "مجموعات تسجيل الأطباء", "DoctorRegistrationCohort:", "", "Reported monthly cohort activity; unavailable families remain suppressed.", "نشاط مجموعات التسجيل الشهرية المسجل؛ تبقى المجموعات غير المتاحة محجوبة."),
             S("Patient registration cohorts", "مجموعات تسجيل المرضى", "PatientRegistrationCohort:", "", "Only permitted cohort cells are displayed, without reconstructing hidden categories.", "تعرض خلايا المجموعات المصرح بها فقط دون استنتاج الفئات المحجوبة.", optional: true),
             S("Feature measurement coverage", "تغطية قياس الميزات", "Metrics", "FeatureBookingDoctors FeatureQueueDoctors PageViewAdoption SearchAdoption", "Uninstrumented page views and searches remain unavailable.", "مشاهدات الصفحات وعمليات البحث غير المسجلة تبقى غير متاحة.", table: true)]),
        (ReportActor.SuperAdmin, "operations") => new("What operational availability is observed?", "ما الإتاحة التشغيلية المرصودة؟",
            ["ActivePractices", "CurrentBookableDoctors", "PublicEligibleDoctors", "CheckIns", "TicketsCompleted"],
            [QueueTrend, S("Observed operational availability", "الإتاحة التشغيلية المرصودة", "Metrics", "ActivePractices CurrentBookableDoctors PublicEligibleDoctors", "Bookable Doctors are a current observed projection, not historical uptime or SLA.", "الأطباء المتاحون للحجز إسقاط مرصود حاليًا وليس سجل إتاحة تاريخيًا أو مستوى خدمة.", table: true)]),
        (ReportActor.SuperAdmin, "geography") => new("Where is reported practice and specialization coverage?", "أين تغطية العيادات والتخصصات المسجلة؟", ["PracticeCoverage", "SpecializationCoverage"],
            [S("Governorate coverage", "تغطية المحافظات", "Governorate:", "", "Governorates are ranked independently; no inferred capacity or geographic coordinates.", "ترتب المحافظات مستقلة دون استنتاج سعة أو إحداثيات."),
             S("City coverage", "تغطية المدن", "City:", "", "City counts are separate from governorate totals.", "أعداد المدن مستقلة عن إجماليات المحافظات.", optional: true),
             S("Specialization coverage", "تغطية التخصصات", "Specialization:", "", "Reported specialization categories only.", "فئات التخصص المسجلة فقط.", optional: true)]),
        (ReportActor.SuperAdmin, "security-metadata") => new("What governance metadata is recorded?", "ما بيانات الحوكمة المسجلة؟",
            ["ActiveUserAccounts", "InactiveUserAccounts", "RoleAssignments", "PermissionGrants", "AssignedPracticePermissionGrants"],
            [S("Role assignment matrix", "مصفوفة إسناد الأدوار", "Role:", "", "Assignment counts are governance metadata, not security quality or login activity.", "أعداد الإسناد بيانات حوكمة وليست تقييمًا للأمان أو نشاط الدخول.", table: true),
             S("Delegations and reset events", "التفويضات وأحداث إعادة التعيين", "Metrics", "AssignedPracticePermissionGrants PasswordResetChallenges PasswordResetConsumptions SecurityLoginEvents", "Snapshot grants and reset events have distinct bases; login events are unavailable without instrumentation.", "منح الصلاحيات عند اللقطة وأحداث إعادة التعيين لها أسس مختلفة؛ أحداث الدخول غير متاحة دون تسجيل.", table: true)]),
        _ => null
    };

    private static PdfPrintSpec Queue() => new("How does the operational queue progress?", "كيف يتقدم الطابور التشغيلي؟",
        ["CheckIns", "TicketsCompleted", "TicketsNoShow", "WaitToFirstCall", "WaitToServiceStart"], [QueueTrend, Durations,
         S("Waiting age and recorded priority", "عمر الانتظار والأولوية المسجلة", "WaitingAgeMinutes:|SegmentPriority:", "", "Priority codes retain their source labels; no VIP or emergency classification is inferred.", "رموز الأولوية تحتفظ بتسميات المصدر دون استنتاج تصنيف طوارئ أو كبار زوار.", table: true, optional: true)]);
    private static PdfPrintSpec Finance(string en, string ar, string trendEn, string trendAr) => new(en, ar,
        ["PaidOperatingExpenses", "OutstandingDueInPeriod", "AllOutstandingAsOf", "OverdueAsOf"],
        [S(trendEn, trendAr, "Series", "PaidOperatingExpenses", "Recorded signed settlements/reversals only; no future payment schedule is inferred.", "التسويات وعكس السداد المسجل بإشارته فقط؛ لا يستنتج جدول مدفوعات مستقبلي."), Balances]);

    public static bool Measured(ReportMetric m) => m.Value.HasValue &&
        m.DataStatus is ReportDataStatus.Complete or ReportDataStatus.Partial &&
        !((m.Unit is "percent" or "minutes") && m.Denominator is <= 0);
    public static string Value(ReportMetric m) => Measured(m) ? ReportPresentationText.MetricValue(m) : "—";
    public static string MetricState(ReportMetric m, bool ar) => !Measured(m) && m.DataStatus is ReportDataStatus.Complete or ReportDataStatus.Partial
        ? m.Value == null ? ar ? "لا توجد قيمة واردة من المصدر." : "No value was supplied by the source."
        : ar ? "لا يوجد مقام مؤهل موجب لهذا المقياس." : "No positive eligible denominator is available for this measure."
        : State(m.DataStatus, ar);
    public static string Name(ReportMetric m, bool ar) => ar ? m.NameAr : m.NameEn;
    public static string Number(decimal? value) => value?.ToString("#,##0.##", CultureInfo.InvariantCulture) ?? "—";
    public static string Confidentiality(ReportActor actor, bool ar) => actor == ReportActor.SuperAdmin
        ? ar ? "سري · تجميعات المنصة المصرح بها" : "Confidential · authorized platform aggregates"
        : actor == ReportActor.Reception ? ar ? "سري · تشغيل العيادات المفوضة" : "Confidential · delegated practice operations"
        : ar ? "سري · تقرير الطبيب المصرح به" : "Confidential · authorized Doctor report";

    // Never expose unavailable platform category labels, including complementary cells in the same family.
    public static IReadOnlyList<ReportSeriesPoint> VisibleGroups(ReportModel r, IReadOnlyList<ReportSeriesPoint> groups)
    {
        if (r.Scope.Actor != ReportActor.SuperAdmin) return groups;
        string Family(ReportSeriesPoint g) => g.Key.Split(':')[0];
        var hidden = groups.Where(g => g.Metrics.Any(m => m.DataStatus == ReportDataStatus.NotAvailable)).Select(Family).ToHashSet(StringComparer.Ordinal);
        return groups.Where(g => !hidden.Contains(Family(g))).ToArray();
    }

    public static PdfPrintDocument Build(ReportPresentation p)
    {
        var r = p.Report; var spec = Find(r.Scope.Actor, r.ReportCode);
        string T(string en, string ar) => p.IsArabic ? ar : en;
        var cards = spec?.Headlines.Select(code => r.Metrics.FirstOrDefault(m => m.MetricCode == code)).OfType<ReportMetric>().ToArray() ?? [];
        List<PdfPrintSection> sections = [];
        foreach (var s in spec?.Sources ?? [])
        {
            IEnumerable<PdfPrintPoint> points;
            if (s.Source == "Metrics") points = s.Codes.Select(code => r.Metrics.FirstOrDefault(m => m.MetricCode == code)).OfType<ReportMetric>().Select(m => new PdfPrintPoint("", m));
            else
            {
                var groups = VisibleGroups(r, s.Source == "Series" ? r.Series : r.Breakdowns);
                if (s.Source == "Series") groups = groups.OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
                else groups = groups.Where(g => s.Source == "Practices"
                    ? Guid.TryParse(g.Key, out var id) && r.Scope.PracticeIds.Contains(id)
                    : s.Source.Split('|').Any(prefix => prefix.EndsWith(':') ? g.Key.StartsWith(prefix, StringComparison.Ordinal) : g.Key == prefix)).ToArray();
                points = groups.SelectMany(g => (s.Codes.Length == 0 ? g.Metrics : s.Codes.Select(code => g.Metrics.FirstOrDefault(m => m.MetricCode == code)).OfType<ReportMetric>())
                    .Select(m => new PdfPrintPoint(s.Source == "Series" ? g.Key : p.IsArabic ? g.NameAr : g.NameEn ?? g.NameAr, m)));
            }
            var rows = points.ToArray();
            if (s.Optional && rows.Length == 0) continue;
            var temporal = s.Source == "Series" && r.AppliedFilters.Grouping is ReportGrouping.Day or ReportGrouping.Week or ReportGrouping.Month;
            var caption = T(s.CaptionEn, s.CaptionAr) + " " + T("Source: reported measures", "المصدر: المقاييس المسجلة") + " · " + s.Source;
            if (s.Source == "Series") caption += " · " + Grouping(r.AppliedFilters.Grouping, p.IsArabic);
            if (rows.Any(x => x.Metric.DataStatus != ReportDataStatus.Complete || !Measured(x.Metric))) caption += " · " + T("Some source measures are partial or unavailable; see status beside each value.", "بعض مقاييس المصدر جزئية أو غير متاحة؛ راجع الحالة بجوار كل قيمة.");
            foreach (var warning in rows.Select(x => x.Metric.WarningCode).OfType<string>().Distinct()) caption += " · " + Warning(warning, p.IsArabic);
            var incomplete = s.Codes.Any(code => !rows.Any(x => x.Metric.MetricCode == code));
            var section = new PdfPrintSection(T(s.TitleEn, s.TitleAr), caption, s.Source, rows, temporal, s.Table || incomplete, null);
            sections.Add(section with { ChartDataUrl = PdfPrintChartRenderer.Render(section, p.IsArabic) });
        }
        var statuses = new[] { r.DataStatus }.Concat(r.Metrics.Select(m => m.DataStatus))
            .Concat(r.Series.Concat(r.Breakdowns).SelectMany(g => g.Metrics).Select(m => m.DataStatus))
            .Where(s => s != ReportDataStatus.Complete).Distinct();
        var notes = statuses.Select(s => State(s, p.IsArabic)).Concat(r.Warnings.Select(w => Warning(w, p.IsArabic))).Distinct().ToArray();
        return new(spec == null ? T("Authorized source appendix", "ملحق المصدر المصرح به") : T(spec.QuestionEn, spec.QuestionAr), cards, sections, notes);
    }
    public static string Grouping(ReportGrouping grouping, bool ar) => ar ? grouping switch
    { ReportGrouping.Day => "يومي", ReportGrouping.Week => "أسبوعي", ReportGrouping.Month => "شهري", _ => "تجميع المصدر" } : grouping.ToString();
    public static string State(ReportDataStatus status, bool ar) => (status, ar) switch
    {
        (ReportDataStatus.Partial, false) => "Partial coverage: only recorded measures are shown.",
        (ReportDataStatus.Partial, true) => "تغطية جزئية: تعرض المقاييس المسجلة فقط.",
        (ReportDataStatus.NotAvailable, false) => "Some data are unavailable or privacy-suppressed; hidden groups are not reconstructed.",
        (ReportDataStatus.NotAvailable, true) => "بعض البيانات غير متاحة أو محجوبة للخصوصية؛ لا تستنتج المجموعات المحجوبة.",
        (ReportDataStatus.NotMeasured, false) => "Not measured: the source does not establish a value.",
        (ReportDataStatus.NotMeasured, true) => "غير مقاس: المصدر لا يثبت قيمة.",
        (ReportDataStatus.NotInstrumented, false) => "Not instrumented: this activity is not recorded.",
        (ReportDataStatus.NotInstrumented, true) => "غير مسجل: لا يجري تسجيل هذا النشاط.",
        (ReportDataStatus.Stale, false) => "Stale source: a current measured value is unavailable.",
        (ReportDataStatus.Stale, true) => "مصدر قديم: لا تتوفر قيمة حالية مقاسة.",
        _ => ReportPresentationText.Status(status.ToString(), ar)
    };
    public static string Warning(string code, bool ar) => code switch
    {
        "Report.PrivacySuppressed" or "Report.SmallCohortSuppressed" => State(ReportDataStatus.NotAvailable, ar),
        "Report.NoEligibleSample" or "Report.ZeroDenominator" => ar ? "لا توجد عينة مؤهلة لحساب النسبة." : "No eligible denominator is available for this ratio.",
        "Report.NotInstrumented" => State(ReportDataStatus.NotInstrumented, ar),
        "Report.Stale" => State(ReportDataStatus.Stale, ar),
        _ => ar ? "تنبيه من المصدر؛ التفاصيل في ملحق الجودة." : "Source quality notice; details are in the quality appendix."
    };
}
