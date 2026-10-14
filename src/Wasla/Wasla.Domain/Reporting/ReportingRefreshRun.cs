using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Reporting;

public sealed class ReportingRefreshRun : Entity<Guid>
{
    private ReportingRefreshRun() { }
    public ReportingRefreshRun(Guid id, DateTime now) : base(id) { StartedAtUtc = now; Status = "Running"; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid? LastPracticeId { get; private set; }
    public int PracticesObserved { get; private set; }
    public int SnapshotsWritten { get; private set; }
    public string? FailureCode { get; private set; }
    public void Progress(Guid practiceId, int observed, int written) { LastPracticeId = practiceId; PracticesObserved += observed; SnapshotsWritten += written; }
    public void Finish(DateTime now, string? failure = null) { FinishedAtUtc = now; FailureCode = failure; Status = failure == null ? "Succeeded" : "Failed"; }
}
