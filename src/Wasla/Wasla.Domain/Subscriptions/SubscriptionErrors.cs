using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Subscriptions;

public static class SubscriptionErrors
{
    public static Error NotFound => Error.NotFound("Subscription.NotFound", "Subscription not found / الاشتراك غير موجود");
    public static Error Invalid(string code = "InvalidTransition") => Error.Validation("Subscription." + code, "Invalid subscription operation / عملية اشتراك غير صالحة");
    public static Error Denied(string code = "FeatureNotEntitled") => Error.Security("Subscription." + code, "Subscription access unavailable / الخدمة غير متاحة للاشتراك");
    public static Error Conflict(string code = "ConcurrencyConflict") => Error.Conflict("Subscription." + code, "Subscription changed; refresh and retry / تغير الاشتراك، يرجى إعادة المحاولة");
    public static Error Capacity(bool reception, int usage, int limit) => Error.Conflict(
        reception ? "Subscription.ReceptionLimitReached" : "Subscription.PracticeLimitReached",
        $"Capacity reached: usage {usage}, limit {limit} / تم بلوغ الحد: الاستخدام {usage} والحد {limit}");
}
