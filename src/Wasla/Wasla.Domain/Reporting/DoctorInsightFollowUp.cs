using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Domain.Reporting;

public enum DoctorInsightState { New = 1, Acknowledged = 2, Resolved = 3, Dismissed = 4 }
public sealed class DoctorInsightFollowUp : AggregateRoot<Guid>
{
    private readonly List<DoctorInsightHistory> _history = [];
    private DoctorInsightFollowUp() { }
    public DoctorInsightFollowUp(Guid id, Guid doctorId, Guid practiceId, string metricCode, string evidenceJson,
        Guid actor, DateTime now) : base(id)
    {
        DoctorId = doctorId; PracticeId = practiceId; MetricCode = metricCode; EvidenceJson = evidenceJson;
        State = DoctorInsightState.New; CreatedByApplicationUserId = actor; CreatedOnUtc = now;
        _history.Add(new(Guid.NewGuid(), id, "TrackingStarted", DoctorInsightState.New, actor, now, null, 1));
    }
    public Guid DoctorId { get; private set; }
    public Guid PracticeId { get; private set; }
    public string MetricCode { get; private set; } = string.Empty;
    public string EvidenceJson { get; private set; } = string.Empty;
    public DoctorInsightState State { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<DoctorInsightHistory> History => _history.AsReadOnly();
    public Result Transition(DoctorInsightState target, string? reason, Guid actor, DateTime now)
    {
        var allowed = State == DoctorInsightState.New && target is DoctorInsightState.Acknowledged or DoctorInsightState.Dismissed ||
            State == DoctorInsightState.Acknowledged && target == DoctorInsightState.Resolved;
        if (!allowed) return Result.Fail(OperatingFinanceErrors.State("Insight.InvalidTransition"));
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            target is DoctorInsightState.Dismissed or DoctorInsightState.Resolved && !OperatingFinancialPolicy.HasReason(reason))
            return Result.Fail(OperatingFinanceErrors.Invalid("Insight.ReasonRequired"));
        State = target; ModifiedOnUtc = now;
        _history.Add(new(Guid.NewGuid(), Id, target.ToString(), target, actor, now, reason, _history.Count + 1));
        return Result.Ok();
    }
}
public sealed class DoctorInsightHistory : Entity<Guid>
{
    private DoctorInsightHistory() { }
    internal DoctorInsightHistory(Guid id, Guid insightId, string operation, DoctorInsightState state,
        Guid actor, DateTime now, string? reason, int sequenceNumber) : base(id)
    { InsightId = insightId; Operation = operation; State = state; ActorApplicationUserId = actor; RecordedAtUtc = now; Reason = reason?.Trim(); SequenceNumber = sequenceNumber; }
    public Guid InsightId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public DoctorInsightState State { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public string? Reason { get; private set; }
    public int SequenceNumber { get; private set; }
}
