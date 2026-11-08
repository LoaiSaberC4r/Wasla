namespace Wasla.Domain.Media;

public sealed class PrivateMediaReconciliationCheckpoint
{
    private PrivateMediaReconciliationCheckpoint() { }
    public PrivateMediaReconciliationCheckpoint(string module) => Module = module;
    public string Module { get; private set; } = "";
    public string? LastFileKey { get; private set; }
    public string? LastReferenceKey { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Advance(string? file, string? reference, DateTime now)
    { LastFileKey = file; LastReferenceKey = reference; UpdatedAtUtc = now; }
}

// Operational findings stay in private persistence, never in application logs or public URLs.
public sealed class PrivateMediaReconciliationRun
{
    private PrivateMediaReconciliationRun() { }
    public PrivateMediaReconciliationRun(string module, DateTime now, long files, long references, long orphans, long missing, bool hasMore, string findingsJson)
    { Id = Guid.NewGuid(); Module = module; InspectedAtUtc = now; ExaminedFiles = files; ExaminedReferences = references; OrphanCandidates = orphans; MissingFiles = missing; HasMore = hasMore; FindingsJson = findingsJson; }
    public Guid Id { get; private set; }
    public string Module { get; private set; } = "";
    public DateTime InspectedAtUtc { get; private set; }
    public long ExaminedFiles { get; private set; }
    public long ExaminedReferences { get; private set; }
    public long OrphanCandidates { get; private set; }
    public long MissingFiles { get; private set; }
    public bool HasMore { get; private set; }
    public string FindingsJson { get; private set; } = "";
}
