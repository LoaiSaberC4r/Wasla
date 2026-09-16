namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class ReservationProjectionInvalidation
{
    private ReservationProjectionInvalidation()
    {
    }

    internal ReservationProjectionInvalidation(
        Guid id,
        string idempotencyKey,
        Guid practiceId,
        Guid doctorId,
        bool refreshAvailability,
        bool refreshPopularity,
        DateTime createdOnUtc)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        PracticeId = practiceId;
        DoctorId = doctorId;
        RefreshAvailability = refreshAvailability;
        RefreshPopularity = refreshPopularity;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid Id { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid PracticeId { get; private set; }
    public Guid DoctorId { get; private set; }
    public bool RefreshAvailability { get; private set; }
    public bool RefreshPopularity { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptOnUtc { get; private set; }
    public string? LastError { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
