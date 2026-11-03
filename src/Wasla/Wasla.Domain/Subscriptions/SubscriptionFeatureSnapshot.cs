using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionFeatureSnapshot : Entity<Guid>
{
    private SubscriptionFeatureSnapshot() { }
    public SubscriptionFeatureSnapshot(Guid id, Guid subscriptionId, Guid featureId, string featureCodeSnapshot, FeatureAccessLevel accessLevel, DateTime createdOnUtc) : base(id)
    {
        SubscriptionId = subscriptionId;
        FeatureId = featureId;
        FeatureCodeSnapshot = featureCodeSnapshot;
        AccessLevel = accessLevel;
        CreatedOnUtc = createdOnUtc;
    }
    public Guid SubscriptionId { get; private set; }
    public Guid FeatureId { get; private set; }
    public string FeatureCodeSnapshot { get; private set; } = "";
    public FeatureAccessLevel AccessLevel { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}
