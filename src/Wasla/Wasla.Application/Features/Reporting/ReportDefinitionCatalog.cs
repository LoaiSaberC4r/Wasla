using Wasla.Domain.Security;

namespace Wasla.Application.Features.Reporting;

public sealed record ReportDefinition(string Code, string TitleAr, string TitleEn, IReadOnlyList<ReportActor> Actors,
    bool Financial = false, bool Drilldown = true);
public static class ReportDefinitionCatalog
{
    public static readonly IReadOnlyList<ReportDefinition> Definitions = [
        new("overview", "نظرة عامة", "Overview", [ReportActor.Doctor, ReportActor.Reception, ReportActor.SuperAdmin]),
        new("reservations", "تحليل الحجوزات", "Reservation analytics", [ReportActor.Doctor, ReportActor.Reception]),
        new("queue", "تحليل الطابور والخدمة", "Queue and operational service", [ReportActor.Doctor, ReportActor.Reception]),
        new("patients", "تحليل نشاط مرضى الطبيب", "Doctor patient activity", [ReportActor.Doctor]),
        new("practice-comparison", "مقارنة العيادات", "Practice comparison", [ReportActor.Doctor]),
        new("operating-finance", "التمويل التشغيلي", "Operating financial statement", [ReportActor.Doctor], true),
        new("expenses", "المصروفات التشغيلية", "Operating expenses", [ReportActor.Doctor], true),
        new("salary", "الرواتب والالتزامات", "Salary obligations and movements", [ReportActor.Doctor], true),
        new("recurring-expenses", "المصروفات الدورية", "Recurring expense obligations", [ReportActor.Doctor], true),
        new("activity", "نشاط الاستقبال", "Reception activity", [ReportActor.Reception]),
        new("doctor-lifecycle", "مراحل اعتماد الأطباء", "Doctor lifecycle", [ReportActor.SuperAdmin]),
        new("adoption", "النمو والاستخدام", "Growth and adoption", [ReportActor.SuperAdmin], Drilldown: false),
        new("operations", "تشغيل المنصة", "Platform operations", [ReportActor.SuperAdmin], Drilldown: false),
        new("geography", "التغطية الجغرافية والتخصصات", "Geographic and specialization coverage", [ReportActor.SuperAdmin]),
        new("security-metadata", "بيانات الحوكمة والأمان", "Security governance metadata", [ReportActor.SuperAdmin], Drilldown: false)
    ];
    public static string ViewPermission(ReportActor actor) => actor switch
    { ReportActor.Doctor => Phase18PermissionNames.DoctorReports, ReportActor.Reception => Phase18PermissionNames.ReceptionReports,
        ReportActor.SuperAdmin => Phase18PermissionNames.PlatformReports, _ => Phase18PermissionNames.PatientDashboard };
    public static string ExportPermission(ReportActor actor, bool financial) => actor switch
    { ReportActor.Doctor => financial ? Phase18PermissionNames.DoctorExportFinancial : Phase18PermissionNames.DoctorExportOperational,
        ReportActor.Reception => Phase18PermissionNames.ReceptionExport, ReportActor.SuperAdmin => Phase18PermissionNames.PlatformExport,
        _ => string.Empty };
}
