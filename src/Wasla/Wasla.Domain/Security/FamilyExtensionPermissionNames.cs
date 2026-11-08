namespace Wasla.Domain.Security;

public static class FamilyExtensionPermissionNames
{
    public const string ManageContacts = "DependentPatientProfile.ManageContacts";
    public const string ManagePhoto = "DependentPatientProfile.ManagePhoto";
    public const string Transactions = "DependentFinance.ViewTransactions";
    public const string Receipts = "DependentFinance.ViewReceipts";
    public static readonly IReadOnlyList<string> All = [ManageContacts, ManagePhoto, Transactions, Receipts];
}
