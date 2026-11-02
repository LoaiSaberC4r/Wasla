using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionReminderSchedule : Entity<Guid>
{
    private SubscriptionReminderSchedule() { }
    public SubscriptionReminderSchedule(Guid subscriptionId, string code, DateTime due, int revision, DateTime now) : base(Guid.NewGuid())
    { SubscriptionId = subscriptionId; ReminderCode = code; DueAtUtc = due; TrialRevision = revision; CreatedOnUtc = now; }
    public Guid SubscriptionId { get; private set; }
    public string ReminderCode { get; private set; } = "";
    public DateTime DueAtUtc { get; private set; }
    public int TrialRevision { get; private set; }
    public SubscriptionReminderStatus Status { get; private set; } = SubscriptionReminderStatus.Pending;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Queue(DateTime now) { Status = SubscriptionReminderStatus.Queued; ProcessedOnUtc = now; }
    public void Cancel() { if (Status == SubscriptionReminderStatus.Pending) Status = SubscriptionReminderStatus.Cancelled; }
}
