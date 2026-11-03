using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class SubscriptionPayment : Entity<Guid>
{
    private SubscriptionPayment() { }
    public SubscriptionPayment(Guid id, Guid subscriptionId, SubscriptionPaymentType paymentType, SubscriptionPaymentMethod paymentMethod, decimal amount, string currency, DateTime paidAtUtc, Guid recordedByApplicationUserId, string? reference, string? notes, string idempotencyKey, DateTime createdOnUtc) : base(id)
    {
        SubscriptionId = subscriptionId;
        PaymentType = paymentType;
        PaymentMethod = paymentMethod;
        Amount = amount;
        Currency = currency;
        PaidAtUtc = paidAtUtc;
        RecordedByApplicationUserId = recordedByApplicationUserId;
        Reference = reference;
        Notes = notes;
        IdempotencyKey = idempotencyKey;
        CreatedOnUtc = createdOnUtc;
    }
    public Guid SubscriptionId { get; private set; }
    public SubscriptionPaymentType PaymentType { get; private set; }
    public SubscriptionPaymentMethod PaymentMethod { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public DateTime PaidAtUtc { get; private set; }
    public Guid RecordedByApplicationUserId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public DateTime CreatedOnUtc { get; private set; }
}
