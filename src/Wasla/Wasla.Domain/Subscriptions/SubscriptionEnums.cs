namespace Wasla.Domain.Subscriptions;

public enum FeatureImplementationStatus { Planned = 1, InProgress = 2, Implemented = 3, Deferred = 4 }
public enum FeatureReleaseReadiness { NotReviewed = 1, NeedsReview = 2, Ready = 3, Blocked = 4 }
public enum FeatureAccessLevel { ReadOnly = 1, Full = 2 }
public enum SubscriptionKind { Paid = 1, Trial = 2, LegacyTransition = 3 }
public enum SubscriptionStatus { Pending = 1, Active = 2, Expired = 3, Cancelled = 4, Superseded = 5, Scheduled = 6 }
public enum BillingCycle { Monthly = 1, Annual = 2 }
public enum SubscriptionPaymentType { Initial = 1, UpgradeDifference = 2, Renewal = 3 }
public enum SubscriptionPaymentMethod { Cash = 1, Card = 2, EWallet = 3 }
public enum SubscriptionChangeType { Upgrade = 1, Downgrade = 2, BillingCycle = 3, Renewal = 4 }
public enum SubscriptionChangeStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4, Scheduled = 5, Applied = 6 }
public enum SubscriptionReminderStatus { Pending = 1, Queued = 2, Cancelled = 3 }

public sealed record SubscriptionFeatureTerms(Guid FeatureId, string Code, FeatureAccessLevel AccessLevel);
public sealed record SubscriptionTerms(
    Guid ProductId, string ProductCode, Guid PlanId, string PlanCode,
    SubscriptionKind Kind, BillingCycle? BillingCycle, int? SelectedPracticeCount,
    decimal PriceAmount, string Currency, int PracticeLimit, int ReceptionLimit,
    string CatalogVersion, int? DurationDays, int[] ReminderDaysBeforeEnd,
    IReadOnlyList<SubscriptionFeatureTerms> Features, decimal? MonthlyUnitPrice = null);
