using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class FeaturePermissionMapping : Entity<Guid>
{
    private FeaturePermissionMapping() { }
    public FeaturePermissionMapping(Guid id, Guid featureId, Guid permissionId, FeatureAccessLevel requiredAccessLevel, DateTime createdOnUtc) : base(id)
    {
        FeatureId = featureId;
        PermissionId = permissionId;
        RequiredAccessLevel = requiredAccessLevel;
        CreatedOnUtc = createdOnUtc;
    }
    public Guid FeatureId { get; private set; }
    public Guid PermissionId { get; private set; }
    public FeatureAccessLevel RequiredAccessLevel { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}
