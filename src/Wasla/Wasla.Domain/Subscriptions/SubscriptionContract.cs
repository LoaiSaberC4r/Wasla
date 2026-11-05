using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

/// <summary>A stable commercial series. Prices and features belong to accepted periods.</summary>
public sealed class SubscriptionContract : AggregateRoot<Guid>
{
    private SubscriptionContract() { }
    public SubscriptionContract(Guid id, Guid doctorId, SubscriptionKind kind, DateTime createdOnUtc)
    {
        if (id == Guid.Empty || doctorId == Guid.Empty || !Enum.IsDefined(kind)) throw new ArgumentException("Invalid contract.");
        Id = id; DoctorId = doctorId; Kind = kind; CreatedOnUtc = createdOnUtc;
    }
    public Guid DoctorId { get; private set; }
    public SubscriptionKind Kind { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static bool PeriodFits(Guid contractId, DateTime start, DateTime end, IEnumerable<DoctorSubscription> periods) =>
        start < end && !periods.Any(p => p.SubscriptionContractId == contractId &&
            p.Status is SubscriptionStatus.Active or SubscriptionStatus.Scheduled && p.StartsAtUtc < end && start < p.EndsAtUtc);
}
