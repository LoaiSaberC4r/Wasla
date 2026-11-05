using System.Text.Json.Serialization;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using Wasla.Application.Persistence;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubscriptionSelection(string ProductCode, string PlanCode, BillingCycle? BillingCycle, string? OfferCode = null, int? SelectedPracticeCount = null);

public interface ISubscriptionCatalogResolver
{
    Task<Result<SubscriptionTerms>> ResolveAsync(SubscriptionSelection? selection, CancellationToken ct);
}
public sealed class SubscriptionCatalogResolver(ISubscriptionDataStore store, IOptionsMonitor<SubscriptionCatalogOptions> options) : ISubscriptionCatalogResolver
{
    public async Task<Result<SubscriptionTerms>> ResolveAsync(SubscriptionSelection? selection, CancellationToken ct)
    {
        if (selection is null) return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("InvalidSelection"));
        var o = options.CurrentValue; var data = await store.CatalogAsync(ct);
        var product = data.Products.SingleOrDefault(p => p.Code == selection.ProductCode && p.IsActive && p.IsPubliclySelectable);
        if (product is null || selection.ProductCode == "ONLINE_BOOKING_ONLY" && !o.BookingOnly.Enabled)
            return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("ProductNotAvailable"));
        var plan = data.Plans.SingleOrDefault(p => p.ProductId == product.Id && p.Code == selection.PlanCode && p.IsActive && p.IsPubliclySelectable);
        if (plan is null) return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("PlanNotAvailable"));
        var trial = selection.OfferCode is not null;
        decimal price; int practices; int reception;
        if (selection.ProductCode == "FULL_PLATFORM")
        {
            var configured = o.FullPlatform.Plans.SingleOrDefault(p => p.Code == plan.Code);
            if (configured is null || selection.SelectedPracticeCount.HasValue) return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("InvalidSelection"));
            if (trial)
            {
                if (!o.FullPlatform.Trial.Enabled || selection.PlanCode != "STARTER" || selection.OfferCode != o.FullPlatform.Trial.OfferCode || selection.BillingCycle.HasValue)
                    return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("TrialNotEligible"));
                price = 0; practices = o.FullPlatform.Trial.PracticeLimit; reception = o.FullPlatform.Trial.ReceptionLimit;
            }
            else
            {
                if (selection.BillingCycle is null || !Enum.IsDefined(selection.BillingCycle.Value)) return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("InvalidBillingCycle"));
                price = (selection.BillingCycle == BillingCycle.Annual ? configured.AnnualPrice : configured.MonthlyPrice)!.Value;
                practices = configured.PracticeLimit; reception = configured.ReceptionLimit;
            }
        }
        else
        {
            if (selection.ProductCode != "ONLINE_BOOKING_ONLY" || trial || selection.BillingCycle != BillingCycle.Monthly || selection.PlanCode != o.BookingOnly.PlanCode || selection.SelectedPracticeCount is not > 0)
                return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("InvalidSelection"));
            try { price = checked(o.BookingOnly.MonthlyUnitPrice * selection.SelectedPracticeCount.Value); }
            catch (OverflowException) { return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("PriceOverflow")); }
            if (price <= 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
                return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("PriceOverflow"));
            practices = selection.SelectedPracticeCount.Value; reception = 0;
        }
        var features = (from e in data.Entitlements join f in data.Features on e.FeatureId equals f.Id
                        where e.PlanId == plan.Id && f.IsActive select new SubscriptionFeatureTerms(f.Id, f.Code, e.AccessLevel)).ToArray();
        if (features.Length == 0) return Result<SubscriptionTerms>.Fail(SubscriptionErrors.Invalid("ConfigurationInvalid"));
        return Result<SubscriptionTerms>.Ok(new(product.Id, product.Code, plan.Id, plan.Code, trial ? SubscriptionKind.Trial : SubscriptionKind.Paid,
            selection.BillingCycle, selection.SelectedPracticeCount, price, o.Currency, practices, reception, o.CatalogVersion,
            trial ? o.FullPlatform.Trial.DurationDays : null, trial ? [.. o.FullPlatform.Trial.ReminderDaysBeforeEnd] : [7, 3, 1, 0], features,
            selection.ProductCode == "ONLINE_BOOKING_ONLY" ? o.BookingOnly.MonthlyUnitPrice : null));
    }
}
