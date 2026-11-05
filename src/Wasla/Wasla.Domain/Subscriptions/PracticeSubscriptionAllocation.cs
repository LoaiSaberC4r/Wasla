using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Subscriptions;

public sealed class PracticeSubscriptionAllocation : AggregateRoot<Guid>
{
    private PracticeSubscriptionAllocation() { }
    public PracticeSubscriptionAllocation(Guid practiceId, Guid contractId, DateTime fromUtc, Guid actorId, bool legacyGrandfathered = false)
    {
        if (practiceId == Guid.Empty || contractId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Invalid allocation.");
        Id = Guid.NewGuid(); DoctorPracticeId = practiceId; SubscriptionContractId = contractId;
        AllocatedFromUtc = fromUtc; CreatedByApplicationUserId = actorId; LegacyGrandfathered = legacyGrandfathered;
    }
    public Guid DoctorPracticeId { get; private set; }
    public Guid SubscriptionContractId { get; private set; }
    public DateTime AllocatedFromUtc { get; private set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? ReleasedByApplicationUserId { get; private set; }
    public string? ReleaseReason { get; private set; }
    public bool LegacyGrandfathered { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void MarkCompliant() => LegacyGrandfathered = false;
    public Result Release(DateTime atUtc, Guid actorId, string? reason)
    {
        if (ReleasedAtUtc is not null || atUtc < AllocatedFromUtc || actorId == Guid.Empty)
            return Result.Fail(SubscriptionErrors.Conflict("AllocationConflict"));
        ReleasedAtUtc = atUtc; ReleasedByApplicationUserId = actorId; ReleaseReason = reason;
        return Result.Ok();
    }
}
