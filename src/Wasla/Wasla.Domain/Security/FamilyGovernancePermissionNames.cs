namespace Wasla.Domain.Security;

public static class FamilyGovernancePermissionNames
{
    public const string IdentityCorrectionRequest = "DependentIdentityCorrection.RequestOwn";
    public const string InviteCreate = "FamilyJoinInvitations.CreateOwn";
    public const string InviteRedeem = "FamilyJoinInvitations.RedeemOwn";
    public static readonly IReadOnlyList<string> All = [IdentityCorrectionRequest, InviteCreate, InviteRedeem];
}
