using System.Text.Json;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionChangeRequest : Entity<Guid>
{
    private SubscriptionChangeRequest() { }
    public SubscriptionChangeRequest(Guid subscriptionId, Guid actor, SubscriptionChangeType type,
        SubscriptionTerms target, decimal difference, DateTime now) : base(Guid.NewGuid())
    {
        SubscriptionId = subscriptionId; RequestedByApplicationUserId = actor; ChangeType = type;
        TargetPlanCode = target.PlanCode; TargetPracticeCount = target.SelectedPracticeCount; TargetBillingCycle = target.BillingCycle;
        TargetPriceSnapshot = target.PriceAmount; DifferenceAmount = difference; TargetTermsSnapshotJson = JsonSerializer.Serialize(target);
        RequestedOnUtc = now; Status = SubscriptionChangeStatus.Pending;
    }
    public Guid SubscriptionId { get; private set; }
    public Guid RequestedByApplicationUserId { get; private set; }
    public SubscriptionChangeType ChangeType { get; private set; }
    public string? TargetPlanCode { get; private set; }
    public int? TargetPracticeCount { get; private set; }
    public BillingCycle? TargetBillingCycle { get; private set; }
    public SubscriptionChangeStatus Status { get; private set; }
    public DateTime RequestedOnUtc { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public DateTime? ReviewedOnUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public decimal TargetPriceSnapshot { get; private set; }
    public decimal DifferenceAmount { get; private set; }
    public DateTime? EffectiveAtUtc { get; private set; }
    public string TargetTermsSnapshotJson { get; private set; } = "";
    public byte[] RowVersion { get; private set; } = [];
    public string? RetainedAllocationsJson { get; private set; }
    public IReadOnlyList<RetainedPracticeAllocation>? RetainedAllocations => RetainedAllocationsJson is null ? null : JsonSerializer.Deserialize<RetainedPracticeAllocation[]>(RetainedAllocationsJson);
    public void SetRetainedAllocations(IReadOnlyList<RetainedPracticeAllocation>? selections) => RetainedAllocationsJson = selections is null ? null : JsonSerializer.Serialize(selections);
    public SubscriptionTerms TargetTerms => JsonSerializer.Deserialize<SubscriptionTerms>(TargetTermsSnapshotJson)!;

    public Result Review(bool approve, Guid actor, DateTime now, DateTime effective, string? reason)
    {
        if (Status != SubscriptionChangeStatus.Pending || !approve && string.IsNullOrWhiteSpace(reason))
            return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        ReviewedByApplicationUserId = actor; ReviewedOnUtc = now; RejectionReason = approve ? null : reason;
        Status = !approve ? SubscriptionChangeStatus.Rejected : ChangeType == SubscriptionChangeType.Upgrade
            ? SubscriptionChangeStatus.Applied : SubscriptionChangeStatus.Scheduled;
        EffectiveAtUtc = approve ? effective : null;
        return Result.Ok();
    }
    public void Apply() => Status = SubscriptionChangeStatus.Applied;
}
