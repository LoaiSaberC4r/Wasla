using System.Text.Json;
using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed record RetainedPracticeAllocation(Guid PracticeId, Guid AllocationId, IReadOnlyList<Guid> ReceptionIds);

/// <summary>Immutable accepted selection. Effective queries honor it even before the worker runs.</summary>
public sealed class SubscriptionAllocationSelection : AggregateRoot<Guid>
{
    private SubscriptionAllocationSelection() { }
    public SubscriptionAllocationSelection(Guid periodId, IReadOnlyList<RetainedPracticeAllocation> practices, Guid actorId, DateTime now)
    {
        Id = Guid.NewGuid(); SubscriptionId = periodId; SelectionJson = JsonSerializer.Serialize(practices);
        CreatedByApplicationUserId = actorId; CreatedOnUtc = now;
        RetainedPractices = practices.Select(p => new SubscriptionRetainedPractice(periodId, p.PracticeId, p.AllocationId)).ToList();
        RetainedReceptions = practices.SelectMany(p => p.ReceptionIds.Select(r => new SubscriptionRetainedReception(periodId, p.PracticeId, r))).ToList();
    }
    public Guid SubscriptionId { get; private set; }
    public string SelectionJson { get; private set; } = "";
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? AppliedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public List<SubscriptionRetainedPractice> RetainedPractices { get; private set; } = [];
    public List<SubscriptionRetainedReception> RetainedReceptions { get; private set; } = [];
    public IReadOnlyList<RetainedPracticeAllocation> Practices => JsonSerializer.Deserialize<RetainedPracticeAllocation[]>(SelectionJson)!;
    public void MarkApplied(DateTime now) => AppliedOnUtc ??= now;
}

public sealed class SubscriptionRetainedPractice
{
    private SubscriptionRetainedPractice() { }
    public SubscriptionRetainedPractice(Guid subscriptionId, Guid practiceId, Guid allocationId)
    { SubscriptionId = subscriptionId; DoctorPracticeId = practiceId; AllocationId = allocationId; }
    public Guid SubscriptionId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid AllocationId { get; private set; }
}
public sealed class SubscriptionRetainedReception
{
    private SubscriptionRetainedReception() { }
    public SubscriptionRetainedReception(Guid subscriptionId, Guid practiceId, Guid receptionId)
    { SubscriptionId = subscriptionId; DoctorPracticeId = practiceId; ReceptionId = receptionId; }
    public Guid SubscriptionId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid ReceptionId { get; private set; }
}
