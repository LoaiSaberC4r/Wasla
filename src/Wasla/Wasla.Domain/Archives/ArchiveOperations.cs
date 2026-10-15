namespace Wasla.Domain.Archives;

public sealed class ArchiveControlState
{
    public static readonly Guid SingletonId = Guid.Parse("18020000-0000-0000-0000-000000000001");
    public Guid Id { get; private set; } = SingletonId;
    public bool Paused { get; private set; }
    public DateTime? PausedAtUtc { get; private set; }
    public Guid? PausedByActorId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public byte[] EventKeySecret { get; private set; } = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    public void SetPaused(bool paused, Guid actor, DateTime now)
    { Paused = paused; PausedAtUtc = paused ? now : null; PausedByActorId = paused ? actor : null; }
}

public sealed class ArchiveRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Mode { get; private set; } = "HistoricalBackfill";
    public string Outcome { get; private set; } = "Running";
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public int Scanned { get; private set; }
    public int ArchivedTickets { get; private set; }
    public int ArchivedReservations { get; private set; }
    public int Excluded { get; private set; }
    public string CorrelationId { get; private set; } = Guid.NewGuid().ToString("N");
    public ArchiveRun(DateTime startedAtUtc, string mode = "HistoricalBackfill") { StartedAtUtc = startedAtUtc; Mode = mode; CorrelationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Id.ToString("N"); }
    private ArchiveRun() { }
    public void Count(bool ticket, bool reservation, bool excluded)
    { Scanned++; if (ticket) ArchivedTickets++; if (reservation) ArchivedReservations++; if (excluded) Excluded++; }
    public void Finish(DateTime now, string? outcome = null)
    { EndedAtUtc = now; Outcome = outcome ?? (Excluded > 0 ? "CompletedWithExceptions" : "Succeeded"); }
}

public sealed class ArchiveProgress
{
    public string Id { get; private set; } = string.Empty;
    public DateOnly? LastDate { get; private set; }
    public Guid? LastId { get; private set; }
    public string? Mode { get; private set; }
    public long Sequence { get; private set; }
    private ArchiveProgress() { }
    public ArchiveProgress(string id) => Id = id;
    public void Advance(DateOnly? businessDate, Guid? id) { LastDate = businessDate; LastId = id; }
    public void SetMode(string mode) => Mode = mode;
    public long NextSequence() => ++Sequence;
}

public sealed class ArchiveIntegrityIssue
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EntityId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid RunId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string FailureCode { get; private set; } = string.Empty;
    public string StateFingerprint { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public ArchiveIntegrityIssue(Guid entity, Guid practice, Guid run, string type, string code, string fingerprint, DateTime utc)
    { EntityId = entity; DoctorPracticeId = practice; RunId = run; EntityType = type; FailureCode = code; StateFingerprint = fingerprint; OccurredAtUtc = utc; }
    private ArchiveIntegrityIssue() { }
}

public sealed class ArchiveAuditEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? EntityId { get; private set; }
    public Guid? DoctorPracticeId { get; private set; }
    public Guid? ActorId { get; private set; }
    public Guid? RunId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = "Succeeded";
    public string? FailureCode { get; private set; }
    public string CorrelationId { get; private set; } = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    public string? ReasonHash { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public ArchiveAuditEvent(string action, string type, Guid? entity, Guid? practice, Guid? actor, DateTime utc,
        Guid? run = null, string? reasonHash = null, string outcome = "Succeeded", string? failureCode = null)
    { Action = action; EntityType = type; EntityId = entity; DoctorPracticeId = practice; ActorId = actor; OccurredAtUtc = utc; RunId = run; ReasonHash = reasonHash; Outcome = outcome; FailureCode = failureCode; CorrelationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? run?.ToString("N") ?? Id.ToString("N"); }
    private ArchiveAuditEvent() { }
}

public sealed class ArchiveControlRequest
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ActorId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public bool CanPause { get; private set; }
    public bool CanResume { get; private set; }
    public bool Paused { get; private set; }
    public DateTime? PausedAtUtc { get; private set; }
    public Guid? PausedByActorId { get; private set; }
    public string ResultRowVersion { get; private set; } = string.Empty;
    private ArchiveControlRequest() { }
    public ArchiveControlRequest(Guid actor, string key, string fingerprint, ArchiveControlState state, bool canPause, bool canResume)
    { ActorId = actor; IdempotencyKey = key; Fingerprint = fingerprint; Operation = state.Paused ? "Pause" : "Resume"; CanPause = canPause; CanResume = canResume; Paused = state.Paused; PausedAtUtc = state.PausedAtUtc;
        PausedByActorId = state.PausedByActorId; ResultRowVersion = Convert.ToBase64String(state.RowVersion); }
}
