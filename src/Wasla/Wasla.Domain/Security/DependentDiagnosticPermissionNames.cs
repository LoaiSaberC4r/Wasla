namespace Wasla.Domain.Security;
public static class DependentDiagnosticPermissionNames
{
    public const string LabCreate = "DependentLabSubmissions.Create";
    public const string LabView = "DependentLabSubmissions.View";
    public const string LabWithdraw = "DependentLabSubmissions.Withdraw";
    public const string RadiologyCreate = "DependentRadiologySubmissions.Create";
    public const string RadiologyView = "DependentRadiologySubmissions.View";
    public const string RadiologyWithdraw = "DependentRadiologySubmissions.Withdraw";
    public static readonly IReadOnlyList<string> All = [LabCreate, LabView, LabWithdraw, RadiologyCreate, RadiologyView, RadiologyWithdraw];
    public static string For(bool lab, string operation) => (lab ? "DependentLabSubmissions." : "DependentRadiologySubmissions.") + operation;
}
