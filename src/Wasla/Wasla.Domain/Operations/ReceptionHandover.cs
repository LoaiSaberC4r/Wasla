using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Domain.Operations;

public enum HandoverStatus { Open = 1, Acknowledged = 2, Closed = 3 }
public sealed class ReceptionHandover : AggregateRoot<Guid>
{
    private readonly List<ReceptionHandoverHistory> _history = [];
    private ReceptionHandover() { }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? TicketId { get; private set; }
    public Guid? PreviousHandoverId { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public HandoverStatus Status { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public Guid? AcknowledgedByApplicationUserId { get; private set; }
    public DateTime? AcknowledgedOnUtc { get; private set; }
    public Guid? ClosedByApplicationUserId { get; private set; }
    public DateTime? ClosedOnUtc { get; private set; }
    public string? ClosureOutcome { get; private set; }
    public int HistorySequence { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<ReceptionHandoverHistory> History => _history.AsReadOnly();

    public static Result<ReceptionHandover> Create(Guid id, Guid doctorId, Guid practiceId, string summary,
        Guid? reservationId, Guid? ticketId, Guid? previousId, Guid actor, DateTime now)
    {
        var note = OperationalNotePolicy.Sanitize(summary);
        if (note.IsFailure) return Result<ReceptionHandover>.Fail(note.Errors);
        if (id == Guid.Empty || doctorId == Guid.Empty || practiceId == Guid.Empty || actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now))
            return Result<ReceptionHandover>.Fail(OperatingFinanceErrors.Invalid());
        var row = new ReceptionHandover { Id = id, DoctorId = doctorId, DoctorPracticeId = practiceId,
            Summary = note.Value, ReservationId = reservationId, TicketId = ticketId, PreviousHandoverId = previousId,
            CreatedByApplicationUserId = actor, CreatedOnUtc = now, Status = HandoverStatus.Open };
        row.Append("Created", actor, now, null); return Result<ReceptionHandover>.Ok(row);
    }
    public Result Acknowledge(Guid actor, DateTime now)
    {
        if (Status != HandoverStatus.Open || actor == CreatedByApplicationUserId)
            return Result.Fail(OperatingFinanceErrors.State("Handover.SelfAcknowledgementOrInvalidState"));
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        Status = HandoverStatus.Acknowledged; AcknowledgedByApplicationUserId = actor; AcknowledgedOnUtc = now;
        Append("Acknowledged", actor, now, null); return Result.Ok();
    }
    public Result Close(bool owningDoctor, string outcome, Guid actor, DateTime now)
    {
        if (Status == HandoverStatus.Closed || Status == HandoverStatus.Open && !owningDoctor)
            return Result.Fail(OperatingFinanceErrors.State("Handover.InvalidClosure"));
        var note = OperationalNotePolicy.Sanitize(outcome);
        if (note.IsFailure) return Result.Fail(note.Errors);
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        Status = HandoverStatus.Closed; ClosedByApplicationUserId = actor; ClosedOnUtc = now; ClosureOutcome = note.Value;
        Append(owningDoctor && AcknowledgedOnUtc == null ? "DoctorDirectClosure" : "Closed", actor, now, note.Value); return Result.Ok();
    }
    public Result Annotate(string annotation, Guid actor, DateTime now)
    {
        if (Status == HandoverStatus.Closed) return Result.Fail(OperatingFinanceErrors.State("Handover.ClosedIsTerminal"));
        var note = OperationalNotePolicy.Sanitize(annotation);
        if (note.IsFailure) return Result.Fail(note.Errors);
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        Append("Annotated", actor, now, note.Value); return Result.Ok();
    }
    private void Append(string operation, Guid actor, DateTime now, string? note)
    {
        ModifiedOnUtc = now; ++HistorySequence;
        _history.Add(new(Guid.NewGuid(), Id, operation, Status, actor, now, note, HistorySequence));
    }
}
public sealed class ReceptionHandoverHistory : Entity<Guid>
{
    private ReceptionHandoverHistory() { }
    internal ReceptionHandoverHistory(Guid id, Guid handoverId, string operation, HandoverStatus status,
        Guid actor, DateTime now, string? note, int sequence) : base(id)
    { HandoverId = handoverId; Operation = operation; Status = status; ActorApplicationUserId = actor; RecordedAtUtc = now; Note = note; SequenceNumber = sequence; }
    public Guid HandoverId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public HandoverStatus Status { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public string? Note { get; private set; }
    public int SequenceNumber { get; private set; }
}
