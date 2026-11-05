using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Tests.Unit;

public sealed class CommercialV12InvariantTests
{
    [Fact]
    public void Contract_periods_cannot_overlap_but_independent_contracts_and_exclusive_successors_fit()
    {
        var now = DateTime.SpecifyKind(new DateTime(2026, 11, 5), DateTimeKind.Utc);
        var period = DoctorSubscription.Select(Guid.NewGuid(), SubscriptionDomainTests.Terms(SubscriptionKind.Paid, BillingCycle.Monthly), now).Value;
        period.Activate(now, Guid.NewGuid());
        Assert.False(SubscriptionContract.PeriodFits(period.SubscriptionContractId, now.AddDays(1), now.AddMonths(2), [period]));
        Assert.True(SubscriptionContract.PeriodFits(period.SubscriptionContractId, period.EndsAtUtc!.Value, now.AddMonths(2), [period]));
        Assert.True(SubscriptionContract.PeriodFits(Guid.NewGuid(), now, now.AddMonths(1), [period]));
    }

    [Fact]
    public void Historical_read_predicate_binds_practice_before_time_and_cannot_borrow_another_practices_window()
    {
        var practice = Guid.NewGuid(); var other = Guid.NewGuid(); var start = DateTime.UnixEpoch; var end = start.AddDays(1);
        var scope = new SubscriptionReadScope(false, [new("PRESCRIPTION", start, end, practice)]);
        var predicate = scope.Predicate<Row>(r => r.CreatedAt, r => r.PracticeId).Compile();
        Assert.True(predicate(new(practice, start))); Assert.False(predicate(new(other, start))); Assert.False(predicate(new(practice, end)));
        Assert.False(scope.Covers(start, other)); Assert.True(scope.Covers(start, practice));
    }

    [Fact]
    public void Original_booking_grant_is_snapshotted_and_cannot_gain_clinical_rights_from_mutated_input()
    {
        var features = new List<SubscriptionFeatureTerms> { new(Guid.NewGuid(), "ONLINE_BOOKING", FeatureAccessLevel.Full) };
        var terms = SubscriptionDomainTests.Terms(SubscriptionKind.Paid, BillingCycle.Monthly) with
        { ProductCode = "ONLINE_BOOKING_ONLY", Features = features, SelectedPracticeCount = 5, PracticeLimit = 5, ReceptionLimit = 0, PriceAmount = 250, MonthlyUnitPrice = 50 };
        var origin = new ReservationCommercialProvenance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UnixEpoch, terms);
        features.Add(new(Guid.NewGuid(), "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full));
        Assert.True(origin.Grants("ONLINE_BOOKING", FeatureAccessLevel.Full)); Assert.False(origin.Grants("MEDICAL_ENCOUNTER", FeatureAccessLevel.Full));
        Assert.Equal(50m, origin.Terms.MonthlyUnitPrice); Assert.Equal(250m, origin.Terms.PriceAmount);
    }

    [Fact]
    public void Allocation_release_preserves_original_contract_and_cannot_rewrite_a_closed_interval()
    {
        var actor = Guid.NewGuid(); var contract = Guid.NewGuid(); var start = DateTime.UnixEpoch;
        var allocation = new PracticeSubscriptionAllocation(Guid.NewGuid(), contract, start, actor);
        Assert.True(allocation.Release(start.AddTicks(-1), actor, "invalid").IsFailure);
        Assert.True(allocation.Release(start.AddDays(1), actor, "Transfer").IsSuccess);
        Assert.True(allocation.Release(start.AddDays(2), actor, "retry").IsFailure);
        Assert.Equal(contract, allocation.SubscriptionContractId); Assert.Equal(start.AddDays(1), allocation.ReleasedAtUtc);
    }
    private sealed record Row(Guid PracticeId, DateTime CreatedAt);
}
