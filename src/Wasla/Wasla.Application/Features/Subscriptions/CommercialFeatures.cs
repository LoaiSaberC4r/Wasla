using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record CommercialFeatureDefinition(string Code, string Group, string NameAr, string NameEn, string MinimumPlan);

public static class CommercialFeatures
{
    public static readonly IReadOnlyList<CommercialFeatureDefinition> All =
    [
        new("DOCTOR_PROFILE", "Doctor", "ملف الطبيب", "Doctor profile", "STARTER"),
        new("PRACTICE_MANAGEMENT", "Practice", "إدارة العيادات", "Practice management", "STARTER"),
        new("PRACTICE_BRANDING", "Practice", "هوية العيادة", "Practice branding", "STARTER"),
        new("SCHEDULE_AVAILABILITY", "Practice", "المواعيد والإتاحة", "Schedule and availability", "STARTER"),
        new("PUBLIC_DISCOVERY", "Booking", "ظهور الطبيب", "Public discovery", "STARTER"),
        new("ONLINE_BOOKING", "Booking", "الحجز الإلكتروني", "Online booking", "STARTER"),
        new("PATIENT_MANAGEMENT", "Patient", "إدارة المرضى", "Patient management", "STARTER"),
        new("RESERVATION_MANAGEMENT", "Booking", "إدارة الحجوزات", "Reservation management", "STARTER"),
        new("BASIC_SEGMENTS_PRICING", "Booking", "الشرائح والتسعير", "Basic segments and pricing", "STARTER"),
        new("RECEPTION_MANAGEMENT", "Operations", "إدارة الاستقبال", "Reception management", "STARTER"),
        new("CHECKIN_WALKIN", "Operations", "الحضور والكشف المباشر", "Check in and walk in", "STARTER"),
        new("TICKET_QUEUE", "Operations", "التذاكر والطابور", "Ticket queue", "STARTER"),
        new("PAYMENT_COLLECTION", "Finance", "تحصيل المدفوعات", "Payment collection", "STARTER"),
        new("DOCTOR_REVENUE_DASHBOARD", "Finance", "إيرادات الطبيب", "Doctor revenue dashboard", "STARTER"),
        new("MEDICAL_ENCOUNTER", "Clinical", "الزيارة الطبية", "Medical encounter", "STARTER"),
        new("DIAGNOSIS", "Clinical", "التشخيص", "Diagnosis", "STARTER"),
        new("PRESCRIPTION", "Clinical", "الوصفات الطبية", "Prescription", "STARTER"),
        new("LAB_RADIOLOGY", "Clinical", "التحاليل والأشعة", "Lab and radiology", "STARTER"),
        new("FOLLOWUP_ELIGIBILITY", "Clinical", "أهلية المتابعة", "Follow up eligibility", "STARTER"),
        new("MEDICAL_RECORD_ACCESS", "Clinical", "السجل الطبي للطبيب المعالج", "Treating doctor medical records", "STARTER"),
        new("ADVANCED_SEGMENTS_PRIORITY_QUOTA", "Operations", "أولوية وحصص الشرائح", "Segment priority and quota", "PROFESSIONAL"),
        new("RECEPTION_HANDOVER", "Operations", "تسليم الاستقبال", "Reception handover", "PROFESSIONAL"),
        new("OPERATING_EXPENSES", "Finance", "مصروفات التشغيل", "Operating expenses", "PROFESSIONAL"),
        new("SALARY_MANAGEMENT", "Finance", "إدارة الرواتب", "Salary management", "PROFESSIONAL"),
        new("OPERATIONAL_REPORTS", "Reporting", "تقارير التشغيل", "Operational reports", "PROFESSIONAL"),
        new("PDF_XLSX_EXPORT", "Reporting", "تصدير التقارير", "PDF and XLSX export", "PROFESSIONAL"),
        new("CROSS_PRACTICE_COMPARISON", "Reporting", "مقارنة العيادات", "Cross practice comparison", "PREMIUM"),
        new("ADVANCED_RULE_BASED_INSIGHTS", "Reporting", "مؤشرات متقدمة", "Advanced rule based insights", "PREMIUM")
    ];
    public static FeatureAccessLevel? SeedAccess(string planCode, CommercialFeatureDefinition feature)
    {
        if (planCode == "BOOKING_ONLY") return feature.Code switch
        {
            "DOCTOR_PROFILE" or "PRACTICE_MANAGEMENT" or "PRACTICE_BRANDING" or "SCHEDULE_AVAILABILITY" or "PUBLIC_DISCOVERY" or "ONLINE_BOOKING" => FeatureAccessLevel.Full,
            "RESERVATION_MANAGEMENT" => FeatureAccessLevel.ReadOnly,
            _ => null
        };
        var included = planCode == "LEGACY_TRANSITION" || planCode == "PREMIUM" || feature.MinimumPlan == "STARTER" ||
            planCode == "PROFESSIONAL" && feature.MinimumPlan == "PROFESSIONAL";
        return included ? FeatureAccessLevel.Full : null;
    }

    // This is seed metadata. Runtime authorization resolves mappings from the database, never plan names.
    public static string? FeatureForPermission(string permission)
    {
        var group = permission.Split('.')[0];
        return group switch
        {
            "DoctorOnboarding" or "DoctorProfile" or "DoctorSpecializations" or "DoctorPracticeLocation" => "DOCTOR_PROFILE",
            "DoctorPractices" or "DoctorPracticeConfiguration" => "PRACTICE_MANAGEMENT",
            "DoctorPracticeBranding" => "PRACTICE_BRANDING",
            "DoctorPracticeSchedule" => "SCHEDULE_AVAILABILITY",
            "DoctorPracticeSegments" or "DoctorPracticePricing" => "BASIC_SEGMENTS_PRICING",
            "DoctorPracticeReservations" or "PracticeReservations" => "RESERVATION_MANAGEMENT",
            "ReceptionUsers" or "ReceptionAssignments" => "RECEPTION_MANAGEMENT",
            "PracticeWalkIns" => "CHECKIN_WALKIN",
            "PracticeTickets" when permission.Contains("CheckIn", StringComparison.Ordinal) || permission.Contains("WalkIn", StringComparison.Ordinal) => "CHECKIN_WALKIN",
            "PracticeTickets" when permission.Contains("Payment", StringComparison.Ordinal) => "PAYMENT_COLLECTION",
            "PracticeTickets" or "DoctorPracticeTickets" or "PracticeQueue" => "TICKET_QUEUE",
            "PracticePayments" or "DoctorPracticePayments" => "PAYMENT_COLLECTION",
            "DoctorRevenue" => "DOCTOR_REVENUE_DASHBOARD",
            "DoctorPatients" or "Patients" => "PATIENT_MANAGEMENT",
            "Encounters" or "MedicalEncounters" => "MEDICAL_ENCOUNTER",
            "Diagnoses" => "DIAGNOSIS",
            "Prescriptions" or "DrugCatalog" or "DrugCatalogRequests" => "PRESCRIPTION",
            "LabCatalog" or "LabCatalogRequests" or "LabRequests" or "LabResults" or "LabResultSubmissions" or
            "RadiologyCatalog" or "RadiologyCatalogRequests" or "RadiologyRequests" or "RadiologyResults" or "RadiologyResultSubmissions" => "LAB_RADIOLOGY",
            "FollowUpEligibility" => "FOLLOWUP_ELIGIBILITY",
            "MedicalRecords" when permission == PermissionNames.MedicalRecordsViewTreatingPatient => "MEDICAL_RECORD_ACCESS",
            "Handovers" => "RECEPTION_HANDOVER",
            "FinancialExpenses" => "OPERATING_EXPENSES",
            "SalaryPlans" or "SalaryObligations" or "SalarySettlements" => "SALARY_MANAGEMENT",
            "Reports" when permission.Contains("Export", StringComparison.Ordinal) => "PDF_XLSX_EXPORT",
            "Reports" => "OPERATIONAL_REPORTS",
            "Insights" => "ADVANCED_RULE_BASED_INSIGHTS",
            "Dashboards" when permission == Phase18PermissionNames.DoctorDashboard => "DOCTOR_REVENUE_DASHBOARD",
            "Dashboards" when permission == Phase18PermissionNames.ReceptionDashboard => "TICKET_QUEUE",
            _ => null
        };
    }
    public static FeatureAccessLevel RequiredAccess(string permission) =>
        permission.Contains(".View", StringComparison.Ordinal) || permission.Contains(".Search", StringComparison.Ordinal)
            ? FeatureAccessLevel.ReadOnly : FeatureAccessLevel.Full;
}
