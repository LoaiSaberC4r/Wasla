using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionMutationReceipt : AggregateRoot<Guid>
{
    private SubscriptionMutationReceipt() { }
    public SubscriptionMutationReceipt(string key, Guid actorId, string fingerprint, Guid resultId, DateTime now)
    { Id = Guid.NewGuid(); IdempotencyKey = key; ActorId = actorId; Fingerprint = fingerprint; ResultId = resultId; CreatedOnUtc = now; }
    public string IdempotencyKey { get; private set; } = "";
    public Guid ActorId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public Guid ResultId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}
