using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Payments;

public sealed class FinancialDailyCounter : AggregateRoot<Guid>
{
    private FinancialDailyCounter() { }

    public FinancialDailyCounter(
        Guid id, Guid doctorPracticeId, DateOnly businessDate,
        FinancialTransactionType transactionType, int lastNumber) : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        BusinessDate = businessDate;
        TransactionType = transactionType;
        LastNumber = lastNumber;
    }

    public Guid DoctorPracticeId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public FinancialTransactionType TransactionType { get; private set; }
    public int LastNumber { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public int AllocateNext() => ++LastNumber;
}

public sealed class FinancialIdempotencyRecord : AggregateRoot<Guid>
{
    private FinancialIdempotencyRecord() { }

    public FinancialIdempotencyRecord(
        Guid id, Guid actorApplicationUserId, string operation,
        string idempotencyKey, string requestFingerprint, DateTime createdOnUtc) : base(id)
    {
        ActorApplicationUserId = actorApplicationUserId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        CreatedOnUtc = FinancialPolicy.EnsureUtc(createdOnUtc);
    }

    public Guid ActorApplicationUserId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid? ResultId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? CompletedOnUtc { get; private set; }

    public void Complete(Guid resultId, DateTime completedOnUtc)
    {
        if (CompletedOnUtc.HasValue)
        {
            return;
        }

        ResultId = resultId;
        CompletedOnUtc = FinancialPolicy.EnsureUtc(completedOnUtc);
    }
}
