using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionMigrationIssue : AggregateRoot<Guid>
{
    private SubscriptionMigrationIssue() { }
    public SubscriptionMigrationIssue(Guid resourceId, string code, DateTime now)
    { Id = Guid.NewGuid(); ResourceId = resourceId; IssueCode = code; CreatedOnUtc = now; }
    public Guid ResourceId { get; private set; }
    public string IssueCode { get; private set; } = "";
    public DateTime CreatedOnUtc { get; private set; }
}
