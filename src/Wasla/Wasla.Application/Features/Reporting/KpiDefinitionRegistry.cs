namespace Wasla.Application.Features.Reporting;

public sealed class KpiDefinitionRegistry : IKpiDefinitionRegistry
{
    public IReadOnlyList<KpiDefinition> Definitions { get; } = Build();

    public KpiDefinition FindDefinition(string code) => Definitions.Single(d => d.MetricCode == code);

    private static KpiDefinition[] Build()
    {
        List<KpiDefinition> definitions = [];
        void Add(string code, string ar, string en, string sources, string basis, string numerator,
            string? denominator = null, string unit = "count", string exclusions = "Records recorded after AsOfUtc.",
            string completeness = "Persisted authoritative source records required.")
            => definitions.Add(new(code, ar, en, en, sources, "Owning Doctor/assigned Practice; platform aggregates are privacy suppressed.",
                basis, "Reconstructed state at AsOfUtc; recorded transactions only.", numerator, denominator,
                exclusions, "Practice-local labeled dates for operations; explicit report zone for financial movements.",
                completeness, "Ignore events/movements recorded after AsOfUtc; do not infer missing history.",
                denominator is null ? "Count distinct eligible source IDs; an empty complete cohort is zero."
                    : "2 eligible successes / 4 eligible records = 50%; an empty denominator is unavailable.", unit));

        Add("BookingsCreated", "الحجوزات المنشأة", "Bookings created", "Reservation.CreatedOnUtc", "Creation date", "Distinct ReservationId created in window.");
        Add("AppointmentsScheduled", "المواعيد المجدولة", "Appointments scheduled", "ReservationHistory schedule snapshots", "Effective scheduled local date", "Distinct reservations scheduled in window at AsOfUtc.");
        Add("ReservationStatusDistribution", "حالات الحجوزات", "Reservation status distribution", "ReservationHistory", "Scheduled cohort", "Distinct reservations per effective status.");
        Add("CancellationsByEvent", "أحداث الإلغاء", "Cancellation events", "ReservationHistory.Cancelled", "Event date", "Cancellation events in window, with recorded initiator and reason.");
        Add("CancellationRateScheduledCohort", "نسبة إلغاء المواعيد", "Scheduled cancellation rate", "ReservationHistory", "Scheduled cohort", "Cancelled reservations in scheduled cohort.", "All reservations in scheduled cohort.", "percent");
        Add("ReservationNoShowRate", "نسبة عدم حضور الحجز", "Reservation no-show rate", "ReservationHistory; recorded grace snapshot", "Matured scheduled cohort", "Distinct effective NoShow reservations.", "Scheduled reservations whose grace expired, excluding pre-appointment cancellations.", "percent", completeness: "Historical grace must be recorded; otherwise NotMeasured.");
        Add("TicketNoShowRate", "نسبة عدم حضور التذكرة", "Ticket no-show rate", "TicketHistory", "Matured check-in cohort", "Distinct tickets in effective NoShow state.", "Matured checked-in tickets.", "percent");
        Add("RescheduleEvents", "أحداث إعادة الجدولة", "Reschedule events", "ReservationHistory.Rescheduled", "Event date", "Recorded reschedule events.");
        Add("RescheduledReservations", "الحجوزات المعاد جدولتها", "Rescheduled reservations", "ReservationHistory.Rescheduled", "Event date", "Distinct affected ReservationId.");
        Add("ReservationToTicketConversion", "تحويل الحجز إلى تذكرة", "Reservation-to-ticket conversion", "ReservationHistory; Ticket.ReservationId", "Matured scheduled cohort", "Distinct eligible reservations linked to checked-in tickets.", "Matured reservations excluding cancellations before appointment.", "percent", "Walk-ins; future appointments; pre-slot cancellations; records after AsOfUtc.");
        Add("BookingLeadTimeMinutes", "وقت الحجز المسبق", "Original booking lead time", "ReservationHistory.Created schedule", "Creation cohort", "Sum of valid original scheduled start minus CreatedOnUtc minutes.", "Valid original creation snapshots.", "minutes");
        Add("PunctualArrival", "الوصول في الموعد", "Punctual arrival", "ReservationHistory; Ticket.CheckInTimeUtc", "Check-in cohort", "Checked-in bookings no more than 5 minutes after last valid schedule before check-in.", "Checked-in bookings with trustworthy pre-check-in schedule.", "percent");
        Add("CheckIns", "الحضور", "Check-ins", "Ticket.CheckInTimeUtc", "Check-in local date", "Distinct TicketId.");
        Add("WalkIns", "الحضور دون حجز", "Walk-ins", "Ticket.Source", "Check-in local date", "Walk-in TicketId.");
        Add("WalkInShare", "نسبة الحضور دون حجز", "Walk-in share", "Ticket.Source", "Check-in local date", "Walk-in tickets.", "All checked-in tickets.", "percent");
        Add("Waiting", "المنتظرون", "Waiting", "TicketHistory", "Current state at AsOfUtc", "Tickets in Waiting.");
        Add("Called", "المستدعون", "Called", "TicketHistory", "Current state at AsOfUtc", "Tickets in Called.");
        Add("InProgress", "الخدمة الجارية", "In progress", "TicketHistory", "Current state at AsOfUtc", "Tickets in InProgress.");
        Add("TicketsCompleted", "التذاكر المكتملة", "Operational completions", "TicketHistory.Completed", "Completion event date", "Distinct completed operational TicketId; not clinical encounter completion.");
        Add("TicketsCancelled", "التذاكر الملغاة", "Cancelled tickets", "TicketHistory", "Check-in cohort", "Effective Cancelled TicketId.");
        Add("TicketsNoShow", "تذاكر عدم الحضور", "No-show tickets", "TicketHistory", "Check-in cohort", "Effective NoShow TicketId.");
        Add("WaitToFirstCall", "الانتظار لأول نداء", "Wait to first call", "TicketHistory; TicketCallAttempt", "Check-in cohort", "Sum of first valid call minus check-in minutes.", "Tickets with valid non-negative call intervals.", "minutes", "Missing or invalid call timestamps; show excluded count.");
        Add("WaitToServiceStart", "الانتظار لبدء الخدمة", "Wait to service start", "TicketHistory.InProgress", "Check-in cohort", "Sum of first valid InProgress minus check-in minutes.", "Tickets with valid start timestamps.", "minutes", "Missing/invalid start timestamps.");
        Add("CallToStart", "النداء إلى بدء الخدمة", "Call to service start", "TicketHistory", "Check-in cohort", "Sum of valid start minus immediately preceding effective call minutes.", "Valid call/start pairs.", "minutes", "Missing or invalid call/start pairs.");
        Add("ServiceDuration", "مدة الخدمة التشغيلية", "Operational service duration", "TicketHistory.InProgress/Completed", "Completion cohort", "Sum of completion minus preceding InProgress start per valid service cycle.", "Valid operational service cycles.", "minutes", "Missing, reversed, invalid timestamps; no clinical duration inference.");
        Add("CallAttempts", "محاولات النداء", "Call attempts", "TicketHistory.Called/Recalled/ManualCalled", "Event date", "Recorded call events.");
        Add("Recalls", "إعادة النداء", "Recalls", "TicketHistory.Recalled", "Event date", "Recorded recall events.");
        Add("OperationalExceptions", "الاستثناءات التشغيلية", "Operational exceptions", "TicketHistory force/manual/no-response/day-ended", "Event date", "Recorded exceptional operational events.");
        Add("ReceptionActivity", "نشاط الاستقبال", "Reception activity", "ReservationHistory; TicketHistory; Reception.ApplicationUserId", "Event date", "Recorded operational events by Reception actors.");
        Add("CapacityMinutes", "الدقائق المتاحة", "Effective capacity minutes", "Immutable schedule snapshots", "Scheduled local date", "Union of effective availability intervals after exceptions.", unit: "minutes", completeness: "No historical schedule snapshot: NotMeasured.");
        Add("BookedUtilization", "استغلال المواعيد", "Booked utilization", "Immutable schedule snapshots; booking intervals", "Scheduled local date", "Union of booked intervals intersecting availability.", "Effective available minutes.", "percent", completeness: "Historical effective schedule snapshots required.");
        Add("ActualServiceUtilization", "استغلال الخدمة", "Actual service utilization", "Immutable schedule snapshots; TicketHistory", "Scheduled local date", "Union of valid service intervals intersecting availability.", "Effective available minutes.", "percent", completeness: "Historical effective schedule snapshots required.");
        Add("SegmentQuotaUtilization", "استغلال حصة الشريحة", "Segment quota utilization", "Historical quota snapshots; recorded consumption", "Scheduled local date", "Recorded quota consumption.", "Effective historical quota.", "percent", completeness: "Absent historical quota: NotMeasured.");
        Add("UniqueDoctorPatients", "المرضى المميزون", "Distinct Doctor patients", "Ticket.PatientId", "Check-in cohort", "Distinct global PatientId in Doctor scope.");
        Add("NewToDoctorPatients", "مرضى جدد للطبيب", "New-to-Doctor patients", "Ticket history for owning Doctor", "Check-in cohort", "Patients whose earliest known Doctor check-in is in window.");
        Add("ReturningDoctorPatients", "المرضى العائدون", "Returning Doctor patients", "Ticket history for owning Doctor", "Check-in cohort", "Patients in selected period with an earlier known check-in with same Doctor.");
        Add("PatientRetention90d", "الاحتفاظ بالمرضى ٩٠ يومًا", "90-day patient retention", "Ticket.PatientId", "90 days preceding selected period", "Patients from prior 90-day checked-in cohort returning in selected period.", "Distinct patients in prior 90-day cohort.", "percent");
        Add("FollowUpActivity", "زيارات المتابعة", "Follow-up activity", "Ticket.VisitTypeCodeSnapshot", "Check-in cohort", "FollowUp TicketId; never a Segment.");
        Add("ClinicalWorkflowCompleted", "السجلات السريرية المكتملة", "Clinical workflow completion", "MedicalEncounter.CompletedAtUtc", "Clinical completion date", "Completed permitted clinical records; independent of Ticket.Completed.");
        Add("GrossCollected", "المتحصلات الإجمالية", "Gross collected", "Committed Payment", "Financial effective date", "Sum of committed patient payments.", unit: "EGP");
        Add("Refunds", "المبالغ المستردة", "Refunds", "Committed Refund", "Financial effective date", "Sum of committed patient refunds.", unit: "EGP");
        Add("NetCollected", "صافي المتحصلات", "Net collected", "Committed Payment/Refund", "Financial effective date", "GrossCollected - Refunds.", unit: "EGP");
        Add("PaidOperatingExpenses", "المصروفات التشغيلية المدفوعة", "Paid operating expenses", "Operating settlement/reversal allocation snapshots", "Effective paid/reversal business date", "Full salary and expense settlements minus compensating reversal movements.", unit: "EGP");
        Add("OutstandingDueInPeriod", "الالتزامات المستحقة بالفترة", "Outstanding due in period", "Obligation revisions; settlement/reversal history", "Due business date", "Unsettled obligations due within period as known at AsOfUtc.", unit: "EGP");
        Add("AllOutstandingAsOf", "كل الالتزامات غير المسددة", "All outstanding as of", "Obligation revisions; settlement/reversal history", "AsOfUtc", "All existing outstanding obligations, independent of reporting month.", unit: "EGP");
        Add("OverdueAsOf", "المستحقات المتأخرة", "Overdue as of", "Obligation revisions; settlement/reversal history", "Due business date", "Outstanding obligations whose due date precedes local AsOf date.", unit: "EGP");
        Add("NetOperatingCashResult", "نتيجة النقد التشغيلي", "Net operating cash result", "Patient collections; operating settlements/reversals", "Financial effective date", "NetCollected - PaidOperatingExpenses; not accounting profit/bank balance.", unit: "EGP");
        Add("ProjectedResultForPeriod", "النتيجة التشغيلية المتوقعة", "Projected operating result", "Collections; operating obligations/movements", "Financial effective/due dates", "NetCollected - PaidOperatingExpenses - OutstandingDueInPeriod.", unit: "EGP");
        Add("PlatformActiveDoctor30d", "الأطباء النشطون ٣٠ يومًا", "Active Doctors in 30 days", "DoctorStatusHistory; Reservation; Ticket", "Trailing 30 days at AsOfUtc", "Approved Doctors with qualifying booking creation or check-in.");
        Add("PlatformActivePatient30d", "المرضى النشطون ٣٠ يومًا", "Active patients in 30 days", "Reservation; Ticket", "Trailing 30 days at AsOfUtc", "Distinct patients with qualifying booking or check-in.");
        Add("FeatureAdoption", "استخدام الميزة", "Feature adoption", "Recorded feature transactions", "Activity window", "Distinct source-backed eligible feature users.", "Eligible actor cohort.", "percent", completeness: "Unrecorded page views/searches: NotInstrumented.");
        Add("RegisteredDoctors", "الأطباء المسجلون", "Registered Doctors", "Doctor.CreatedOnUtc", "At AsOfUtc", "Distinct registered Doctors.");
        Add("PendingDoctors", "الأطباء قيد المراجعة", "Pending Doctors", "DoctorStatusHistory", "At AsOfUtc", "Doctors pending approval.");
        Add("ApprovedDoctors", "الأطباء المعتمدون", "Approved Doctors", "DoctorStatusHistory", "At AsOfUtc", "Doctors approved; does not imply bookable.");
        Add("RejectedDoctors", "الأطباء المرفوضون", "Rejected Doctors", "DoctorStatusHistory", "At AsOfUtc", "Doctors rejected.");
        Add("SuspendedDoctors", "الأطباء الموقوفون", "Suspended Doctors", "DoctorStatusHistory", "At AsOfUtc", "Doctors suspended.");
        Add("ActivePractices", "العيادات النشطة", "Active practices", "Practice current lifecycle", "Current state", "Active practices; separate from availability.", completeness: "Historical lifecycle unavailable: NotMeasured.");
        Add("RegisteredPatients", "المرضى المسجلون", "Registered patients", "Patient.CreatedOnUtc", "At AsOfUtc", "Distinct global patients.");
        Add("ReceptionAccounts", "حسابات الاستقبال", "Reception accounts", "Reception.CreatedOnUtc", "At AsOfUtc", "Distinct Reception accounts.");
        Add("ApprovalWaitMinutes", "الانتظار للموافقة", "Approval waiting time", "Doctor.CreatedOnUtc; DoctorStatusHistory", "Registration cohort", "Sum of registration-to-first-approval duration.", "Doctors with trustworthy approval timestamps.", "minutes");
        Add("DoctorAcquisition", "اكتساب الأطباء", "Doctor acquisition", "Doctor.CreatedOnUtc", "Creation date", "Distinct newly registered Doctors.");
        Add("PatientAcquisition", "اكتساب المرضى", "Patient acquisition", "Patient.CreatedOnUtc", "Creation date", "Distinct newly registered patients.");
        Add("DoctorRetention", "استمرار الأطباء", "Doctor activity retention", "Reservation/Ticket activity", "Previous/current activity windows", "Previously active Doctors also active now.", "Distinct Doctors active in preceding equal window.", "percent");
        Add("PatientRetention", "استمرار المرضى", "Patient activity retention", "Reservation/Ticket activity", "Previous/current activity windows", "Previously active patients also active now.", "Distinct patients active in preceding equal window.", "percent");
        Add("OpenHandovers", "التسليمات المفتوحة", "Open handovers", "HandoverHistory", "At AsOfUtc", "Nonclosed Practice-scoped handovers.");
        Add("OpenInvestigations", "التحقيقات المفتوحة", "Open investigations", "InvestigationHistory", "At AsOfUtc", "Nonclosed administrative cases.");
        Add("InvestigationResolutions", "التحقيقات المحلولة", "Investigation resolutions", "InvestigationHistory.Resolve", "Event date", "Recorded resolution transitions.");
        return definitions.ToArray();
    }
}
