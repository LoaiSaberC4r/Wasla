using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.OperatingFinance;

public sealed class OperatingGenerationRun : Entity<Guid>
{
    private OperatingGenerationRun() { }
    public OperatingGenerationRun(Guid id, DateTime startedAtUtc) : base(id) { StartedAtUtc = startedAtUtc; Status = "Running"; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public int GeneratedCount { get; private set; }
    public string? FailureCode { get; private set; }
    public void Finish(DateTime now, int count, string? failureCode = null)
    { FinishedAtUtc = now; GeneratedCount = count; FailureCode = failureCode; Status = failureCode is null ? "Succeeded" : "Failed"; }
}
