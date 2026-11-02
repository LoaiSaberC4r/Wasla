using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class LegacyTransitionEnrollmentBatch : Entity<Guid>
{
    private LegacyTransitionEnrollmentBatch() { }
    public LegacyTransitionEnrollmentBatch(Guid id, DateTime now, int durationDays, Guid actor) : base(id)
    { StartedAtUtc = now; EndsAtUtc = now.AddDays(durationDays); DurationDays = durationDays; InitiatedByApplicationUserId = actor; CreatedOnUtc = now; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public int DurationDays { get; private set; }
    public Guid InitiatedByApplicationUserId { get; private set; }
    public string Status { get; private set; } = "Running";
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Complete(DateTime now) { Status = "Completed"; CompletedAtUtc = now; }
}
