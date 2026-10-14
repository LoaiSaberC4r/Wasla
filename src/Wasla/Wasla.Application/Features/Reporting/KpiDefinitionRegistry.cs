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
            => definitions.Add(new(code, ar, en, en, sources, "Actor report allow-list and current permission/ownership/delegation; platform patient aggregates are privacy suppressed; outgoing finance is Doctor-only.",
                basis, "Reconstructed state at AsOfUtc; recorded transactions only.", numerator, denominator,
                exclusions, "Practice-local labeled dates for operations; explicit report zone for financial movements.",
                completeness, "Ignore events/movements recorded after AsOfUtc; do not infer missing history.",
                Example(code, unit, denominator), unit));

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
        Add("UnusedCapacityMinutes", "دقائق المواعيد غير المستخدمة", "Unused booking capacity minutes", "Immutable schedule snapshots; reservation interval snapshots", "Scheduled local date", "Effective available minutes minus union of booked minutes intersecting availability.", unit: "minutes", completeness: "Historical effective schedules required; time capacity is separate from headcount constraints.");
        Add("MaximumDailyPatientCapacity", "حد المرضى اليومي المجمع", "Configured daily patient capacity", "Immutable MaximumDailyPatients policy snapshots", "Working local days", "Sum of configured daily patient limits for active working days.", completeness: "Unbounded limits or intraday policy changes are NotMeasured; never translate headcount into invented minutes.");
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
        Add("HandoversCreated", "التسليمات المنشأة", "Handovers created", "HandoverHistory.Created", "Recorded event date in report zone", "Recorded creation events in permitted Practices.");
        Add("HandoverAcknowledgements", "إقرارات استلام التسليم", "Handover acknowledgements", "HandoverHistory.Acknowledged", "Recorded event date in report zone", "Recorded acknowledgement events.");
        Add("HandoversClosed", "التسليمات المغلقة", "Handovers closed", "HandoverHistory.Closed/DoctorDirectClosure", "Recorded event date in report zone", "Recorded terminal closure events.");
        Add("OpenInvestigations", "التحقيقات المفتوحة", "Open investigations", "InvestigationHistory", "At AsOfUtc", "Nonclosed administrative cases.");
        Add("InvestigationResolutions", "التحقيقات المحلولة", "Investigation resolutions", "InvestigationHistory.Resolve", "Event date", "Recorded resolution transitions.");
        Add("CurrentBookableDoctors", "أطباء لديهم مواعيد متاحة مسجلة", "Doctors with recorded bookable slots", "Current public availability projection + current configuration", "Current observed projection", "Distinct approved active Doctors with visible available future slots and active normal consultation pricing.");
        Add("PublicEligibleDoctors", "أطباء مؤهلون للظهور", "Public eligible Doctors", "Doctor + active account + active practice", "Current source", "Distinct approved Doctors with active account and at least one active practice.");
        Add("PendingApprovalAgeMinutes", "عمر انتظار الاعتماد", "Pending approval age", "Doctor.CreatedOnUtc + current pending status", "Current pending cohort", "Sum current registration-to-now minutes", "Pending Doctors with trustworthy creation timestamps", "minutes");
        Add("VerificationCompleteDoctors", "مكتملو مستندات التحقق", "Doctors with verification documents", "Doctor required document-presence metadata", "Current metadata", "Doctors with front/back identity and syndicate front document keys; contents are excluded.");
        Add("DoctorSuspensions", "إيقاف الأطباء", "Doctor suspensions", "DoctorStatusHistory", "Status event date", "Recorded transitions to Suspended.");
        Add("DoctorReactivations", "إعادة تفعيل الأطباء", "Doctor reactivations", "DoctorStatusHistory", "Status event date", "Recorded Suspended to Approved transitions.");
        Add("SpecializationApprovals", "اعتماد التخصصات", "Specialization approvals", "DoctorSpecializationRequestHistory", "Decision event date", "Recorded specialization approval events.");
        Add("SpecializationReviewMinutes", "زمن مراجعة التخصص", "Specialization review time", "Specialization submission/review timestamps", "Reviewed decision cohort", "Sum valid submission-to-review minutes", "Requests with trustworthy reviewed/submitted timestamps", "minutes");
        Add("ActiveUserAccounts", "الحسابات النشطة", "Active user accounts", "ApplicationUser.IsActive", "Current state", "Distinct active accounts.");
        Add("InactiveUserAccounts", "الحسابات غير النشطة", "Inactive user accounts", "ApplicationUser.IsActive", "Current state", "Distinct inactive accounts.");
        Add("FirstLoginAccounts", "حسابات الدخول الأول", "First-login accounts", "ApplicationUser.IsFirstLogin", "Current state", "Distinct accounts requiring first-login completion.");
        Add("RoleAssignments", "إسناد الأدوار", "Role assignments", "UserRole", "Current governance", "Distinct role assignment rows.");
        Add("PermissionGrants", "منح الصلاحيات", "Permission grants", "RolePermission", "Current governance", "Distinct role permission grants.");
        Add("AssignedPracticePermissionGrants", "الصلاحيات المفوضة للعيادات", "Delegated practice permission grants", "Active Reception assignment permissions", "Current governance", "Delegated permission rows on active assignments.");
        Add("SuperAdminAccounts", "حسابات الإدارة", "SuperAdmin accounts", "SuperAdmin.IsDeleted", "Current governance", "Nondeleted SuperAdmin identities; founder policy remains on existing commands.");
        Add("PasswordResetChallenges", "طلبات إعادة تعيين كلمة المرور", "Password reset challenges", "PasswordResetChallenge.CreatedOnUtc", "Creation event date", "Recorded challenge creations; no OTP, hash or token metadata.");
        Add("PasswordResetConsumptions", "إتمام إعادة تعيين كلمة المرور", "Password reset completions", "PasswordResetChallenge.ConsumedOnUtc", "Completion event date", "Recorded reset consumption events.");
        Add("SecurityLoginEvents", "أحداث تسجيل الدخول", "Login security events", "No persisted login-event store", "Activity date", "NotInstrumented; no inferred authentication activity.");
        Add("PageViewAdoption", "استخدام الصفحات", "Page view adoption", "No persisted page-view telemetry", "Activity date", "NotInstrumented; no inferred page views.");
        Add("SearchAdoption", "استخدام البحث", "Search adoption", "No persisted search telemetry", "Activity date", "NotInstrumented; no inferred search requests.");
        Add("PracticeCoverage", "تغطية العيادات", "Practice coverage", "DoctorPractice geography keys", "Current state", "Distinct Practices in geographic group.");
        Add("SpecializationCoverage", "تغطية التخصصات", "Specialization coverage", "Approved DoctorSpecialization selections", "Current state", "Distinct approved Doctors associated with specialization.");
        Add("FeatureBookingDoctors", "أطباء يستخدمون الحجز", "Doctors using booking", "Reservation.CreatedOnUtc", "Activity date", "Distinct Doctors with recorded created bookings.");
        Add("FeatureQueueDoctors", "أطباء يستخدمون الطابور", "Doctors using queue", "Ticket.CheckInTimeUtc", "Activity date", "Distinct Doctors with recorded check-ins.");
        return definitions.ToArray();
    }
    private static string Example(string code, string unit, string? denominator) => code switch
    {
        "GrossCollected" => "EGP300.00 + EGP200.00 collections = EGP500.00 gross.",
        "Refunds" => "EGP50.00 + EGP25.00 refunds = EGP75.00.",
        "NetCollected" => "EGP500.00 gross - EGP75.00 refunds = EGP425.00 net.",
        "PaidOperatingExpenses" => "EGP500.00 settlements - EGP100.00 reversals = EGP400.00 outgoing cash.",
        "NetOperatingCashResult" => "EGP425.00 net collections - EGP200.00 outgoing cash = EGP225.00.",
        "ProjectedResultForPeriod" => "EGP225.00 net operating cash - EGP100.00 unpaid due-in-period = EGP125.00.",
        "AllOutstandingAsOf" or "OutstandingDueInPeriod" or "OverdueAsOf" => "Eligible immutable allocated liabilities EGP200.00 + EGP100.00 = EGP300.00; each metric applies its own due-date scope.",
        "CapacityMinutes" => "Availability08:00-09:00 and08:30-09:30 has90 union minutes, not120.",
        "UnusedCapacityMinutes" => "90 available union minutes -30 booked union minutes =60 unused minutes.",
        "MaximumDailyPatientCapacity" => "Two working days with recorded limits30 each contribute60 patient places; an unbounded limit remains unmeasured.",
        _ when denominator != null && unit == "percent" => "2 eligible successes /4 eligible records =50%; an empty denominator is unavailable.",
        _ when denominator != null && unit == "minutes" => "60 total valid interval minutes /4 valid intervals =15 minutes; an empty denominator is unavailable.",
        _ when denominator != null => "2 eligible units /4 eligible records =0.5; an empty denominator is unavailable.",
        _ => "3 distinct eligible source IDs contribute3; an empty complete cohort is0."
    };
}
