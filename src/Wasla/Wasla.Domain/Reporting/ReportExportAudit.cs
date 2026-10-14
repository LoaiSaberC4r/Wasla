using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Reporting;

public sealed class ReportExportAudit : Entity<Guid>
{
    private ReportExportAudit() { }
    public ReportExportAudit(Guid id, Guid actorId, string actorRole, string reportCode, string format, string language,
        string appliedFiltersJson, string outcome, string? failureCode, int? byteCount, DateTime recordedAtUtc, string scopeSnapshotJson = "{}") : base(id)
    {
        ActorApplicationUserId = actorId; ActorRole = actorRole; ReportCode = reportCode; Format = format; Language = language;
        AppliedFiltersJson = appliedFiltersJson; Outcome = outcome; FailureCode = failureCode; ByteCount = byteCount; RecordedAtUtc = recordedAtUtc;
        ScopeSnapshotJson = scopeSnapshotJson;
    }
    public Guid ActorApplicationUserId { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public string ReportCode { get; private set; } = string.Empty;
    public string Format { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string AppliedFiltersJson { get; private set; } = string.Empty;
    public string ScopeSnapshotJson { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? FailureCode { get; private set; }
    public int? ByteCount { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
}
