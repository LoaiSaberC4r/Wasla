namespace Wasla.Domain.Security;

public static class Phase18PermissionNames
{
    public const string DoctorDashboard = "Dashboards.Doctor.ViewOwn";
    public const string DoctorReports = "Reports.Doctor.ViewOwn";
    public const string DoctorExportOperational = "Reports.Doctor.ExportOperational";
    public const string DoctorExportFinancial = "Reports.Doctor.ExportFinancial";
    public const string ExpensesView = "FinancialExpenses.ViewOwn";
    public const string ExpensesManage = "FinancialExpenses.ManageOwn";
    public const string ExpensesSettle = "FinancialExpenses.SettleOwn";
    public const string ExpensesReverse = "FinancialExpenses.ReverseOwn";
    public const string SalaryPlansView = "SalaryPlans.ViewOwn";
    public const string SalaryPlansManage = "SalaryPlans.ManageOwn";
    public const string SalaryObligationsView = "SalaryObligations.ViewOwn";
    public const string SalarySettle = "SalarySettlements.RecordOwn";
    public const string SalaryReverse = "SalarySettlements.ReverseOwn";
    public const string DoctorHandoversView = "Handovers.Doctor.ViewOwn";
    public const string DoctorHandoversManage = "Handovers.Doctor.ManageOwn";
    public const string InsightsView = "Insights.Doctor.ViewOwn";
    public const string InsightsTrack = "Insights.Doctor.TrackOwn";
    public const string ReceptionDashboard = "Dashboards.Reception.ViewAssigned";
    public const string ReceptionReports = "Reports.Reception.ViewAssigned";
    public const string ReceptionExport = "Reports.Reception.ExportOperational";
    public const string HandoverCreate = "Handovers.CreateAssigned";
    public const string HandoverView = "Handovers.ViewAssigned";
    public const string HandoverAcknowledge = "Handovers.AcknowledgeAssigned";
    public const string HandoverClose = "Handovers.CloseAssigned";
    public const string HandoverAnnotate = "Handovers.AnnotateAssigned";
    public const string PlatformDashboard = "Dashboards.Platform.View";
    public const string PlatformReports = "Reports.Platform.View";
    public const string PlatformExport = "Reports.Platform.Export";
    public const string InvestigationsView = "Investigations.View";
    public const string InvestigationsCreate = "Investigations.Create";
    public const string InvestigationsAssign = "Investigations.Assign";
    public const string InvestigationsStartReview = "Investigations.StartReview";
    public const string InvestigationsResolve = "Investigations.Resolve";
    public const string InvestigationsClose = "Investigations.Close";
    public const string InvestigationsReopen = "Investigations.Reopen";
    public const string JobsView = "PlatformJobs.ViewHealth";
    public const string DataQualityView = "PlatformDataQuality.View";
    public const string SecurityMetadataView = "PlatformSecurityMetadata.View";
    public const string PatientDashboard = "Dashboards.Patient.ViewOwn";

    public static readonly IReadOnlyList<string> DoctorDefaults = [DoctorDashboard, DoctorReports,
        DoctorExportOperational, DoctorExportFinancial, ExpensesView, ExpensesManage, ExpensesSettle,
        ExpensesReverse, SalaryPlansView, SalaryPlansManage, SalaryObligationsView, SalarySettle,
        SalaryReverse, DoctorHandoversView, DoctorHandoversManage, InsightsView, InsightsTrack];
    public static readonly IReadOnlyList<string> ReceptionDefaults = [ReceptionDashboard, ReceptionReports,
        ReceptionExport, HandoverCreate, HandoverView, HandoverAcknowledge, HandoverClose, HandoverAnnotate];
    public static readonly IReadOnlyList<string> PlatformDefaults = [PlatformDashboard, PlatformReports,
        PlatformExport, InvestigationsView, InvestigationsCreate, InvestigationsAssign, InvestigationsStartReview,
        InvestigationsResolve, InvestigationsClose, InvestigationsReopen, JobsView, DataQualityView, SecurityMetadataView];
    public static readonly IReadOnlyList<string> All = [.. DoctorDefaults, .. ReceptionDefaults,
        .. PlatformDefaults, PatientDashboard];
}
