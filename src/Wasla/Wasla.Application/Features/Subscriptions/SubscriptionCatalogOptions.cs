using Microsoft.Extensions.Options;
using BuildingBlock.Application.Time;

namespace Wasla.Application.Features.Subscriptions;

public sealed class SubscriptionCatalogOptions
{
    public const string SectionName = "SubscriptionCatalog";
    public string CatalogVersion { get; set; } = "";
    public string Currency { get; set; } = "";
    public FullPlatformOptions FullPlatform { get; set; } = new();
    public BookingOnlyOptions BookingOnly { get; set; } = new();
    public LegacyTransitionOptions LegacyTransition { get; set; } = new();
    public SubscriptionPaymentOptions PaymentMethods { get; set; } = new();
    public SubscriptionPolicyOptions Policies { get; set; } = new();
    public SubscriptionBackgroundOptions BackgroundProcessing { get; set; } = new();
}
public sealed class FullPlatformOptions
{
    public TrialOfferOptions Trial { get; set; } = new();
    public List<SubscriptionPlanOptions> Plans { get; set; } = [];
}
public sealed class TrialOfferOptions
{
    public bool Enabled { get; set; }
    public string OfferCode { get; set; } = "";
    public int DurationDays { get; set; }
    public bool StartsOnDoctorApproval { get; set; }
    public int MaximumTrialsPerDoctor { get; set; }
    public int PracticeLimit { get; set; }
    public int ReceptionLimit { get; set; }
    public int[] ReminderDaysBeforeEnd { get; set; } = [];
    public bool ManualExtensionEnabled { get; set; }
}
public sealed class SubscriptionPlanOptions
{
    public string Code { get; set; } = "";
    public decimal? MonthlyPrice { get; set; }
    public decimal? AnnualPrice { get; set; }
    public int PracticeLimit { get; set; }
    public int ReceptionLimit { get; set; }
}
public sealed class BookingOnlyOptions
{
    public bool Enabled { get; set; }
    public string PlanCode { get; set; } = "";
    public decimal MonthlyUnitPrice { get; set; } = 50.00m;
    public int MinimumSelectedPractices { get; } = 1;
    [System.Text.Json.Serialization.JsonIgnore]
    public int MaximumSelectablePractices { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public List<BookingPricingTier> PricingTiers { get; set; } = [];
}
public sealed class BookingPricingTier
{
    public int MinPracticeCount { get; set; }
    public int MaxPracticeCount { get; set; }
    public decimal MonthlyPrice { get; set; }
}
public sealed class LegacyTransitionOptions
{
    public bool Enabled { get; set; }
    public int DurationDays { get; set; }
    public int MinimumPracticeLimit { get; set; }
    public int MinimumReceptionLimit { get; set; }
}
public sealed class SubscriptionPaymentOptions { public bool Cash { get; set; } public bool Card { get; set; } public bool EWallet { get; set; } }
public sealed class SubscriptionPolicyOptions
{
    public bool AutomaticRenewalEnabled { get; set; }
    public bool ProrationEnabled { get; set; }
    public bool SetupFeeEnabled { get; set; }
    public bool PlatformCommissionEnabled { get; set; }
    public bool TaxCalculationEnabled { get; set; }
    public bool ManualRenewalEnabled { get; set; }
    public bool CancellationAtPeriodEnd { get; set; }
}
public sealed class SubscriptionBackgroundOptions
{
    public bool Enabled { get; set; }
    public int PollingIntervalSeconds { get; set; }
    public int BatchSize { get; set; }
}

public sealed class SubscriptionCatalogOptionsValidator(IDateTimeProvider? clock = null) : IValidateOptions<SubscriptionCatalogOptions>
{
    private static readonly string[] RequiredPlans = ["STARTER", "PROFESSIONAL", "PREMIUM"];
    public ValidateOptionsResult Validate(string? name, SubscriptionCatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.CatalogVersion) || options.CatalogVersion.Length > 200 || options.Currency != "EGP") errors.Add("CatalogVersion (up to 200 characters) and EGP currency are required.");
        var plans = options.FullPlatform.Plans;
        if (plans.Count != 3 || plans.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count() != plans.Count ||
            RequiredPlans.Any(c => plans.All(p => p.Code != c))) errors.Add("All approved Full Platform plans are required exactly once.");
        if (plans.Any(p => !ValidMoney(p.MonthlyPrice) || !ValidMoney(p.AnnualPrice) ||
                p.AnnualPrice != p.MonthlyPrice * 10 || p.PracticeLimit <= 0 || p.ReceptionLimit <= 0))
            errors.Add("Plans require positive prices (annual = 10 monthly payments), money precision and positive capacities.");
        var trial = options.FullPlatform.Trial;
        var maximumDuration = (DateTime.MaxValue.Date - (clock?.UtcNow ?? DateTime.UnixEpoch).Date).Days;
        if (trial.Enabled && (string.IsNullOrWhiteSpace(trial.OfferCode) || trial.OfferCode.Length > 200 || trial.DurationDays != 20 || trial.DurationDays > maximumDuration || trial.StartsOnDoctorApproval ||
            trial.MaximumTrialsPerDoctor != 1 || trial.PracticeLimit <= 0 || trial.ReceptionLimit <= 0 ||
            trial.ReminderDaysBeforeEnd.Length == 0 || trial.ReminderDaysBeforeEnd.Distinct().Count() != trial.ReminderDaysBeforeEnd.Length ||
            trial.ReminderDaysBeforeEnd.Any(d => d < 0 || d >= trial.DurationDays))) errors.Add("Trial offer is invalid.");
        var starter = plans.FirstOrDefault(p => p.Code == "STARTER");
        if (trial.Enabled && starter is not null && (trial.PracticeLimit != starter.PracticeLimit || trial.ReceptionLimit != starter.ReceptionLimit)) errors.Add("Trial capacity must match Starter.");
        var booking = options.BookingOnly;
        if (booking.Enabled && (booking.PlanCode != "BOOKING_ONLY" || booking.MonthlyUnitPrice != 50.00m))
            errors.Add("Booking Only requires the approved 50 EGP monthly unit price.");
        if (options.LegacyTransition.DurationDays <= 0 || options.LegacyTransition.DurationDays > maximumDuration || options.LegacyTransition.MinimumPracticeLimit <= 0 || options.LegacyTransition.MinimumReceptionLimit <= 0) errors.Add("Legacy transition duration and minimum capacities must be valid and positive.");
        if (options.BackgroundProcessing.PollingIntervalSeconds <= 0 || options.BackgroundProcessing.BatchSize <= 0) errors.Add("Background interval and batch size must be positive.");
        if (options.Policies.AutomaticRenewalEnabled || options.Policies.ProrationEnabled || options.Policies.SetupFeeEnabled ||
            options.Policies.PlatformCommissionEnabled || options.Policies.TaxCalculationEnabled || !options.Policies.ManualRenewalEnabled || !options.Policies.CancellationAtPeriodEnd)
            errors.Add("Unsupported commercial policy enabled.");
        if (options.PaymentMethods.Card || options.PaymentMethods.EWallet || !options.PaymentMethods.Cash) errors.Add("Only manual Cash SaaS payments are enabled.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
    private static bool ValidMoney(decimal? value) => value is > 0 && decimal.Round(value.Value, 2) == value &&
        value.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "", StringComparison.Ordinal).TrimStart('0').Length <= 18;
}
