using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionHistory : Entity<Guid>
{
    private SubscriptionHistory() { }
    public SubscriptionHistory(Guid id, Guid subscriptionId, string eventType, SubscriptionStatus? previousStatus, SubscriptionStatus? newStatus, Guid? actorApplicationUserId, DateTime occurredOnUtc, string? reason, string? previousTermsSnapshotJson, string? newTermsSnapshotJson, string? metadataJson, string eventIdempotencyKey, DateTime createdOnUtc) : base(id)
    {
        SubscriptionId = subscriptionId;
        EventType = eventType;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ActorApplicationUserId = actorApplicationUserId;
        OccurredOnUtc = occurredOnUtc;
        Reason = reason;
        PreviousTermsSnapshotJson = previousTermsSnapshotJson;
        NewTermsSnapshotJson = newTermsSnapshotJson;
        MetadataJson = metadataJson;
        EventIdempotencyKey = eventIdempotencyKey;
        CreatedOnUtc = createdOnUtc;
    }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = "";
    public SubscriptionStatus? PreviousStatus { get; private set; }
    public SubscriptionStatus? NewStatus { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? Reason { get; private set; }
    public string? PreviousTermsSnapshotJson { get; private set; }
    public string? NewTermsSnapshotJson { get; private set; }
    public string? MetadataJson { get; private set; }
    public string EventIdempotencyKey { get; private set; } = "";
    public DateTime CreatedOnUtc { get; private set; }
}
