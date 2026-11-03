using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class PlanFeatureEntitlement : Entity<Guid>
{
    private PlanFeatureEntitlement() { }
    public PlanFeatureEntitlement(Guid id, Guid planId, Guid featureId, FeatureAccessLevel accessLevel, DateTime createdOnUtc) : base(id)
    {
        PlanId = planId;
        FeatureId = featureId;
        AccessLevel = accessLevel;
        CreatedOnUtc = createdOnUtc;
    }
    public Guid PlanId { get; private set; }
    public Guid FeatureId { get; private set; }
    public FeatureAccessLevel AccessLevel { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}
