namespace Wasla.Domain.Security;
public static class MedicalDocumentPermissionNames
{
    public const string CreateOwn = "MedicalDocuments.CreateOwn";
    public const string ViewOwn = "MedicalDocuments.ViewOwn";
    public const string ReviseOwn = "MedicalDocuments.ReviseOwn";
    public const string WithdrawOwn = "MedicalDocuments.WithdrawOwn";
    public const string CreateDependent = "MedicalDocuments.CreateDependent";
    public const string ViewDependent = "MedicalDocuments.ViewDependent";
    public const string ReviseDependent = "MedicalDocuments.ReviseDependent";
    public const string WithdrawDependent = "MedicalDocuments.WithdrawDependent";
    public static readonly IReadOnlyList<string> All = [CreateOwn, ViewOwn, ReviseOwn, WithdrawOwn, CreateDependent, ViewDependent, ReviseDependent, WithdrawDependent];
}
