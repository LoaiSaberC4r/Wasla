namespace Wasla.Domain.Subscriptions;

public static class SubscriptionPermissionNames
{
    public const string ViewOwn = "Subscriptions.ViewOwn";
    public const string RequestChangeOwn = "Subscriptions.RequestChangeOwn";
    public const string CancelAtPeriodEndOwn = "Subscriptions.CancelAtPeriodEndOwn";
    public const string ViewAll = "Subscriptions.ViewAll";
    public const string ViewDetails = "Subscriptions.ViewDetails";
    public const string Activate = "Subscriptions.Activate";
    public const string ExtendTrial = "Subscriptions.ExtendTrial";
    public const string Renew = "Subscriptions.Renew";
    public const string ReviewChangeRequests = "Subscriptions.ReviewChangeRequests";
    public const string EnrollLegacyTransition = "Subscriptions.EnrollLegacyTransition";
    public const string ReceptionAccountStateOwn = "ReceptionUsers.ManageAccountStateOwn";
    public static readonly IReadOnlyList<string> DoctorDefaults = [ViewOwn, RequestChangeOwn, CancelAtPeriodEndOwn, ReceptionAccountStateOwn];
    public static readonly IReadOnlyList<string> AdminDefaults = [ViewAll, ViewDetails, Activate, ExtendTrial, Renew, ReviewChangeRequests, EnrollLegacyTransition];
    public static readonly IReadOnlyList<string> All = [.. DoctorDefaults, .. AdminDefaults];
}
