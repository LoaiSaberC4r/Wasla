using Microsoft.Extensions.Configuration;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Tests.Unit;

public sealed class SubscriptionCatalogOptionsTests
{
    internal static SubscriptionCatalogOptions Configured()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Wasla.sln"))) root = root.Parent;
        return new ConfigurationBuilder().AddJsonFile(Path.Combine(root!.FullName, "src/Wasla/Wasla.Api/appsettings.json"))
            .Build().GetSection(SubscriptionCatalogOptions.SectionName).Get<SubscriptionCatalogOptions>()!;
    }
    [Fact] public void Approved_v12_configuration_is_valid_with_per_practice_booking_and_self_started_trial()
    {
        var o = Configured(); Assert.True(new SubscriptionCatalogOptionsValidator().Validate(null, o).Succeeded);
        Assert.True(o.BookingOnly.Enabled); Assert.Equal(50m, o.BookingOnly.MonthlyUnitPrice); Assert.False(o.FullPlatform.Trial.StartsOnDoctorApproval); Assert.False(o.PaymentMethods.Card); Assert.False(o.PaymentMethods.EWallet);
        Assert.Equal(499m, o.FullPlatform.Plans.Single(p => p.Code == "STARTER").MonthlyPrice);
        Assert.Equal(9990m, o.FullPlatform.Plans.Single(p => p.Code == "PROFESSIONAL").AnnualPrice);
        Assert.Equal(1799m, o.FullPlatform.Plans.Single(p => p.Code == "PREMIUM").MonthlyPrice);
    }
    [Theory]
    [InlineData("missing-price")][InlineData("negative-price")][InlineData("zero-capacity")][InlineData("negative-capacity")]
    [InlineData("annual-mismatch")][InlineData("duplicate-plan")][InlineData("booking-zero-price")][InlineData("approval-started-trial")]
    [InlineData("booking-wrong-plan")][InlineData("booking-wrong-price")][InlineData("booking-price")][InlineData("automatic-renewal")]
    [InlineData("overflow-price")][InlineData("fractional-price")][InlineData("overflow-duration")]
    public void Invalid_configuration_fails_fast(string invalid)
    {
        var o = Configured();
        switch (invalid)
        {
            case "missing-price": o.FullPlatform.Plans[0].MonthlyPrice = null; break;
            case "negative-price": o.FullPlatform.Plans[0].MonthlyPrice = -1; break;
            case "zero-capacity": o.FullPlatform.Plans[0].PracticeLimit = 0; break;
            case "negative-capacity": o.FullPlatform.Plans[0].ReceptionLimit = -1; break;
            case "annual-mismatch": o.FullPlatform.Plans[0].AnnualPrice++; break;
            case "duplicate-plan": o.FullPlatform.Plans.Add(o.FullPlatform.Plans[0]); break;
            case "overflow-price": o.FullPlatform.Plans[0].MonthlyPrice = decimal.MaxValue; break;
            case "fractional-price": o.FullPlatform.Plans[0].MonthlyPrice = 499.001m; break;
            case "overflow-duration": o.FullPlatform.Trial.DurationDays = int.MaxValue; break;
            case "automatic-renewal": o.Policies.AutomaticRenewalEnabled = true; break;
            case "approval-started-trial": o.FullPlatform.Trial.StartsOnDoctorApproval = true; break;
            case "booking-wrong-plan": o.BookingOnly.PlanCode = "OTHER"; break;
            case "booking-wrong-price": o.BookingOnly.MonthlyUnitPrice = 51; break;
            default: o.BookingOnly.MonthlyUnitPrice = 0; break;
        }
        Assert.True(new SubscriptionCatalogOptionsValidator().Validate(null, o).Failed);
    }
    [Fact] public void Environment_style_override_wins_over_json()
    {
        var value = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SubscriptionCatalog:FullPlatform:Plans:0:MonthlyPrice"] = "499" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SubscriptionCatalog:FullPlatform:Plans:0:MonthlyPrice"] = "599" }).Build();
        Assert.Equal(599m, value.GetSection("SubscriptionCatalog").Get<SubscriptionCatalogOptions>()!.FullPlatform.Plans[0].MonthlyPrice);
    }
}

public sealed class SubscriptionDomainTests
{
    internal static SubscriptionTerms Terms(SubscriptionKind kind = SubscriptionKind.Trial, BillingCycle? cycle = null)
    {
        var o = SubscriptionCatalogOptionsTests.Configured(); var plan = o.FullPlatform.Plans[0];
        return new(Guid.NewGuid(), "FULL_PLATFORM", Guid.NewGuid(), plan.Code, kind, cycle, null,
            kind == SubscriptionKind.Paid ? (cycle == BillingCycle.Annual ? plan.AnnualPrice : plan.MonthlyPrice)!.Value : 0,
            o.Currency, plan.PracticeLimit, plan.ReceptionLimit, o.CatalogVersion,
            kind == SubscriptionKind.Trial ? o.FullPlatform.Trial.DurationDays : null, [.. o.FullPlatform.Trial.ReminderDaysBeforeEnd],
            [new(Guid.NewGuid(), "PRACTICE_MANAGEMENT", FeatureAccessLevel.Full)]);
    }
    [Fact] public void Trial_starts_at_explicit_activation_once_with_exclusive_end_and_no_billing_cycle()
    {
        var approved = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var s = DoctorSubscription.Select(Guid.NewGuid(), Terms(), approved.AddDays(-4)).Value;
        Assert.Equal(SubscriptionStatus.Pending, s.Status); Assert.Null(s.StartsAtUtc);
        Assert.True(s.Activate(approved, Guid.NewGuid()).IsSuccess);
        Assert.Equal(approved, s.TrialStartedAtUtc); Assert.Equal(approved.AddDays(20), s.TrialEndsAtUtc);
        Assert.True(s.IsValidAt(approved)); Assert.False(s.IsValidAt(approved.AddDays(20)));
        Assert.True(s.Activate(approved, Guid.NewGuid()).IsFailure); Assert.Null(s.BillingCycle); Assert.Equal(0, s.PriceAmount);
    }
    [Theory][InlineData(BillingCycle.Monthly)][InlineData(BillingCycle.Annual)]
    public void Paid_period_uses_calendar_month_or_year_and_cancellation_preserves_period(BillingCycle cycle)
    {
        var now = new DateTime(2026, 1, 31, 8, 0, 0, DateTimeKind.Utc); var s = DoctorSubscription.Select(Guid.NewGuid(), Terms(SubscriptionKind.Paid, cycle), now).Value;
        s.Activate(now, Guid.NewGuid()); Assert.Equal(cycle == BillingCycle.Monthly ? now.AddMonths(1) : now.AddYears(1), s.EndsAtUtc);
        s.CancelAtPeriodEnd(now); Assert.True(s.IsValidAt(now)); Assert.True(s.WillNotRenew);
    }
    [Fact] public void Trial_extension_is_active_only_positive_reasoned_and_preserves_start()
    {
        var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); var s = DoctorSubscription.Select(Guid.NewGuid(), Terms(), now).Value;
        Assert.True(s.ExtendTrial(5, "reason", now).IsFailure); s.Activate(now, Guid.NewGuid());
        Assert.True(s.ExtendTrial(0, "reason", now).IsFailure); Assert.True(s.ExtendTrial(5, " ", now).IsFailure);
        Assert.True(s.ExtendTrial(int.MaxValue, "reason", now).IsFailure);
        Assert.True(s.ExtendTrial(5, "approved exception", now).IsSuccess); Assert.Equal(now, s.TrialStartedAtUtc);
        Assert.Equal(now.AddDays(25), s.TrialEndsAtUtc); Assert.Equal(1, s.TrialExtensionCount);
        Assert.True(s.ExtendTrial(5, "reason", now.AddDays(25)).IsFailure);
    }
    [Fact] public void Snapshot_does_not_change_when_source_configuration_or_arrays_change()
    {
        var t = Terms(); var s = DoctorSubscription.Select(Guid.NewGuid(), t, DateTime.UnixEpoch).Value;
        t.ReminderDaysBeforeEnd[0] = 99;
        Assert.Equal(5, s.Terms.ReminderDaysBeforeEnd[0]);
        Assert.Equal(t.PriceAmount, s.PriceAmount); Assert.Equal(t.PracticeLimit, s.PracticeLimit);
    }
    [Fact] public void Feature_matrix_contains_only_approved_booking_capabilities()
    {
        var booking = CommercialFeatures.All.Where(f => CommercialFeatures.SeedAccess("BOOKING_ONLY", f).HasValue).ToArray();
        Assert.Equal(7, booking.Length);
        Assert.Equal(FeatureAccessLevel.ReadOnly, CommercialFeatures.SeedAccess("BOOKING_ONLY", booking.Single(f => f.Code == "RESERVATION_MANAGEMENT")));
        Assert.Null(CommercialFeatures.SeedAccess("BOOKING_ONLY", CommercialFeatures.All.Single(f => f.Code == "MEDICAL_ENCOUNTER")));
        Assert.Equal(28, CommercialFeatures.All.Count);
    }
}
