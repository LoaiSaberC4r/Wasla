using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Tickets;

public enum TicketStatus
{
    Waiting = 1,
    Called = 2,
    InProgress = 3,
    NoShow = 4,
    Completed = 5,
    Cancelled = 6
}

public enum TicketSource
{
    Reservation = 1,
    WalkIn = 2
}

public enum TicketHistoryEventType
{
    TicketCreated = 1,
    ReservationCheckedIn = 2,
    ForceCheckIn = 3,
    WalkInCreated = 4,
    Called = 5,
    Recalled = 6,
    CallNoResponse = 7,
    ManualCalled = 8,
    AutoNoShow = 9,
    RestoredFromNoShow = 10,
    FastTrackGranted = 11,
    InProgress = 12,
    Completed = 13,
    Cancelled = 14,
    OperationalDayEnded = 15
}

public enum TicketCallAttemptOutcome
{
    Pending = 1,
    Responded = 2,
    NoResponse = 3
}

public enum CheckInMode
{
    Normal = 1,
    Force = 2,
    WalkIn = 3
}

public static class TicketPolicy
{
    public const int ReasonCodeMaxLength = 100;
    public const int ReasonMaxLength = 1000;
}

public static class TicketCancellationReasons
{
    public const string OperationalDayEnded = "OperationalDayEnded";
}

public sealed record TicketCreationSnapshot(
    Guid Id,
    Guid DoctorId,
    Guid DoctorPracticeId,
    Guid PatientId,
    Guid? ReservationId,
    DateOnly BusinessDate,
    int TicketNumber,
    TicketSource Source,
    Guid SegmentId,
    string SegmentNameAr,
    string? SegmentNameEn,
    int SegmentPriority,
    Guid VisitTypeId,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    decimal PriceSnapshot,
    string PracticeTimeZoneId,
    DateTime CheckInTimeUtc,
    DateTime QueueOrderTimeUtc,
    CheckInMode CheckInMode,
    Guid CreatedByApplicationUserId,
    string? Reason);

public sealed class Ticket : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<TicketHistory> _history = [];
    private readonly List<TicketCallAttempt> _callAttempts = [];

    private Ticket()
    {
    }

    private Ticket(TicketCreationSnapshot snapshot) : base(snapshot.Id)
    {
        DoctorId = snapshot.DoctorId;
        DoctorPracticeId = snapshot.DoctorPracticeId;
        PatientId = snapshot.PatientId;
        ReservationId = snapshot.ReservationId;
        BusinessDate = snapshot.BusinessDate;
        TicketNumber = snapshot.TicketNumber;
        Source = snapshot.Source;
        SegmentId = snapshot.SegmentId;
        SegmentNameArSnapshot = snapshot.SegmentNameAr.Trim();
        SegmentNameEnSnapshot = Normalize(snapshot.SegmentNameEn);
        SegmentPrioritySnapshot = snapshot.SegmentPriority;
        VisitTypeId = snapshot.VisitTypeId;
        VisitTypeCodeSnapshot = snapshot.VisitTypeCode.Trim();
        VisitTypeNameArSnapshot = snapshot.VisitTypeNameAr.Trim();
        VisitTypeNameEnSnapshot = Normalize(snapshot.VisitTypeNameEn);
        PriceSnapshot = snapshot.PriceSnapshot;
        PracticeTimeZoneIdSnapshot = snapshot.PracticeTimeZoneId.Trim();
        CheckInTimeUtc = EnsureUtc(snapshot.CheckInTimeUtc);
        QueueOrderTimeUtc = EnsureUtc(snapshot.QueueOrderTimeUtc);
        CheckInMode = snapshot.CheckInMode;
        Status = TicketStatus.Waiting;
        CallCycle = 1;
        CreatedByApplicationUserId = snapshot.CreatedByApplicationUserId;
        CreatedOnUtc = CheckInTimeUtc;
        LastUpdatedOnUtc = CheckInTimeUtc;

        AppendHistory(TicketHistoryEventType.TicketCreated, null, TicketStatus.Waiting,
            snapshot.CreatedByApplicationUserId, CheckInTimeUtc, null, null);
        AppendHistory(
            snapshot.CheckInMode switch
            {
                CheckInMode.Normal => TicketHistoryEventType.ReservationCheckedIn,
                CheckInMode.Force => TicketHistoryEventType.ForceCheckIn,
                CheckInMode.WalkIn => TicketHistoryEventType.WalkInCreated,
                _ => throw new InvalidOperationException("Unsupported check-in mode.")
            },
            TicketStatus.Waiting,
            TicketStatus.Waiting,
            snapshot.CreatedByApplicationUserId,
            CheckInTimeUtc,
            snapshot.CheckInMode == CheckInMode.Force ? "ForceCheckIn" : null,
            snapshot.Reason);
    }

    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public int TicketNumber { get; private set; }
    public TicketSource Source { get; private set; }
    public Guid SegmentId { get; private set; }
    public string SegmentNameArSnapshot { get; private set; } = string.Empty;
    public string? SegmentNameEnSnapshot { get; private set; }
    public int SegmentPrioritySnapshot { get; private set; }
    public Guid VisitTypeId { get; private set; }
    public string VisitTypeCodeSnapshot { get; private set; } = string.Empty;
    public string VisitTypeNameArSnapshot { get; private set; } = string.Empty;
    public string? VisitTypeNameEnSnapshot { get; private set; }
    public decimal PriceSnapshot { get; private set; }
    public string PracticeTimeZoneIdSnapshot { get; private set; } = string.Empty;
    public DateTime CheckInTimeUtc { get; private set; }
    public DateTime QueueOrderTimeUtc { get; private set; }
    public CheckInMode CheckInMode { get; private set; }
    public TicketStatus Status { get; private set; }
    public bool IsFastTrack { get; private set; }
    public DateTime? FastTrackGrantedOnUtc { get; private set; }
    public int CallCycle { get; private set; }
    public DateTime? CalledOnUtc { get; private set; }
    public DateTime? NoShowOnUtc { get; private set; }
    public DateTime? InProgressOnUtc { get; private set; }
    public DateTime? CompletedOnUtc { get; private set; }
    public DateTime? CancelledOnUtc { get; private set; }
    public string? CancellationReasonCode { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime LastUpdatedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<TicketHistory> History => _history.AsReadOnly();
    public IReadOnlyCollection<TicketCallAttempt> CallAttempts => _callAttempts.AsReadOnly();
    public bool IsOpen => Status is TicketStatus.Waiting or TicketStatus.Called or
        TicketStatus.InProgress or TicketStatus.NoShow;

    public static Result<Ticket> CreateFromReservation(TicketCreationSnapshot snapshot)
        => Create(snapshot, TicketSource.Reservation);

    public static Result<Ticket> CreateWalkIn(TicketCreationSnapshot snapshot)
        => Create(snapshot, TicketSource.WalkIn);

    public Result Call(Guid actorApplicationUserId, DateTime occurredOnUtc)
        => BeginCall(actorApplicationUserId, occurredOnUtc, manual: false, null);

    public Result ManualCall(Guid actorApplicationUserId, string reason, DateTime occurredOnUtc)
        => BeginCall(actorApplicationUserId, occurredOnUtc, manual: true, reason);

    public Result Recall(Guid actorApplicationUserId, int maximumAttempts, DateTime occurredOnUtc)
    {
        if (Status != TicketStatus.Called || actorApplicationUserId == Guid.Empty ||
            maximumAttempts < 1 || CurrentCycleAttempts() >= maximumAttempts ||
            _callAttempts.LastOrDefault()?.Outcome != TicketCallAttemptOutcome.NoResponse)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        AddCallAttempt(when);
        Touch(actorApplicationUserId, when);
        AppendHistory(TicketHistoryEventType.Recalled, Status, Status,
            actorApplicationUserId, when, null, null);
        return Result.Ok();
    }

    public Result ConfirmNoResponse(
        Guid actorApplicationUserId,
        int maximumAttempts,
        DateTime occurredOnUtc)
    {
        var current = _callAttempts.LastOrDefault();
        if (Status != TicketStatus.Called || actorApplicationUserId == Guid.Empty ||
            maximumAttempts < 1 || current is null || current.CallCycle != CallCycle ||
            current.Outcome != TicketCallAttemptOutcome.Pending)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        current.ConfirmNoResponse(when);
        AppendHistory(TicketHistoryEventType.CallNoResponse, Status, Status,
            actorApplicationUserId, when, null, null);

        if (CurrentCycleAttempts() >= maximumAttempts)
        {
            var previous = Status;
            Status = TicketStatus.NoShow;
            NoShowOnUtc = when;
            AppendHistory(TicketHistoryEventType.AutoNoShow, previous, Status,
                null, when, "MaximumCallAttemptsReached", null);
        }

        Touch(actorApplicationUserId, when);
        return Result.Ok();
    }

    public Result RestoreNoShow(
        Guid actorApplicationUserId,
        bool grantFastTrack,
        DateTime occurredOnUtc)
    {
        if (Status != TicketStatus.NoShow || actorApplicationUserId == Guid.Empty)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        var previous = Status;
        Status = TicketStatus.Waiting;
        QueueOrderTimeUtc = when;
        CallCycle++;
        IsFastTrack = grantFastTrack;
        FastTrackGrantedOnUtc = grantFastTrack ? when : null;
        CalledOnUtc = null;
        Touch(actorApplicationUserId, when);
        AppendHistory(TicketHistoryEventType.RestoredFromNoShow, previous, Status,
            actorApplicationUserId, when, grantFastTrack ? "FastTrack" : "NormalQueue", null);
        if (grantFastTrack)
        {
            AppendHistory(TicketHistoryEventType.FastTrackGranted, Status, Status,
                actorApplicationUserId, when, null, null);
        }

        return Result.Ok();
    }

    public Result StartVisit(Guid doctorApplicationUserId, DateTime occurredOnUtc)
    {
        var current = _callAttempts.LastOrDefault();
        if (Status != TicketStatus.Called || doctorApplicationUserId == Guid.Empty ||
            current is null || current.CallCycle != CallCycle)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        if (current.Outcome == TicketCallAttemptOutcome.Pending)
        {
            current.MarkResponded(when);
        }

        var previous = Status;
        Status = TicketStatus.InProgress;
        InProgressOnUtc = when;
        Touch(doctorApplicationUserId, when);
        AppendHistory(TicketHistoryEventType.InProgress, previous, Status,
            doctorApplicationUserId, when, null, null);
        return Result.Ok();
    }

    public Result Complete(Guid doctorApplicationUserId, DateTime occurredOnUtc)
    {
        if (Status != TicketStatus.InProgress || doctorApplicationUserId == Guid.Empty)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        var previous = Status;
        Status = TicketStatus.Completed;
        CompletedOnUtc = when;
        Touch(doctorApplicationUserId, when);
        AppendHistory(TicketHistoryEventType.Completed, previous, Status,
            doctorApplicationUserId, when, null, null);
        return Result.Ok();
    }

    public Result Cancel(
        Guid actorApplicationUserId,
        string reason,
        DateTime occurredOnUtc)
    {
        var normalizedReason = Normalize(reason);
        if (Status is not TicketStatus.Waiting and not TicketStatus.Called ||
            actorApplicationUserId == Guid.Empty || normalizedReason is null ||
            normalizedReason.Length > TicketPolicy.ReasonMaxLength)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        return CancelCore(actorApplicationUserId, "ActorCancelled", normalizedReason,
            EnsureUtc(occurredOnUtc), TicketHistoryEventType.Cancelled);
    }

    public Result CancelForOperationalDay(DateTime occurredOnUtc)
    {
        if (Status is not TicketStatus.Waiting and not TicketStatus.Called)
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        return CancelCore(null, TicketCancellationReasons.OperationalDayEnded, null,
            EnsureUtc(occurredOnUtc), TicketHistoryEventType.OperationalDayEnded);
    }

    private static Result<Ticket> Create(TicketCreationSnapshot snapshot, TicketSource expectedSource)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var validReservation = expectedSource == TicketSource.Reservation &&
            snapshot.ReservationId.HasValue && snapshot.ReservationId != Guid.Empty &&
            snapshot.CheckInMode is CheckInMode.Normal or CheckInMode.Force;
        var validWalkIn = expectedSource == TicketSource.WalkIn &&
            snapshot.ReservationId is null && snapshot.CheckInMode == CheckInMode.WalkIn;
        var normalizedReason = Normalize(snapshot.Reason);
        if (snapshot.Id == Guid.Empty || snapshot.DoctorId == Guid.Empty ||
            snapshot.DoctorPracticeId == Guid.Empty || snapshot.PatientId == Guid.Empty ||
            snapshot.BusinessDate == default || snapshot.TicketNumber <= 0 ||
            snapshot.Source != expectedSource || (!validReservation && !validWalkIn) ||
            snapshot.SegmentId == Guid.Empty || snapshot.VisitTypeId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.SegmentNameAr) ||
            string.IsNullOrWhiteSpace(snapshot.VisitTypeCode) ||
            string.IsNullOrWhiteSpace(snapshot.VisitTypeNameAr) ||
            snapshot.PriceSnapshot <= 0 || string.IsNullOrWhiteSpace(snapshot.PracticeTimeZoneId) ||
            snapshot.CreatedByApplicationUserId == Guid.Empty ||
            snapshot.CheckInTimeUtc.Kind != DateTimeKind.Utc ||
            snapshot.QueueOrderTimeUtc.Kind != DateTimeKind.Utc ||
            snapshot.CheckInMode == CheckInMode.Force && normalizedReason is null ||
            normalizedReason?.Length > TicketPolicy.ReasonMaxLength)
        {
            return Result<Ticket>.Fail(TicketErrors.InvalidCreation);
        }

        return Result<Ticket>.Ok(new Ticket(snapshot));
    }

    private Result BeginCall(
        Guid actorApplicationUserId,
        DateTime occurredOnUtc,
        bool manual,
        string? reason)
    {
        var normalizedReason = Normalize(reason);
        if (Status != TicketStatus.Waiting || actorApplicationUserId == Guid.Empty ||
            manual && (normalizedReason is null || normalizedReason.Length > TicketPolicy.ReasonMaxLength))
        {
            return Result.Fail(TicketErrors.InvalidTransition);
        }

        var when = EnsureUtc(occurredOnUtc);
        var previous = Status;
        Status = TicketStatus.Called;
        CalledOnUtc = when;
        IsFastTrack = false;
        AddCallAttempt(when);
        Touch(actorApplicationUserId, when);
        AppendHistory(
            manual ? TicketHistoryEventType.ManualCalled : TicketHistoryEventType.Called,
            previous,
            Status,
            actorApplicationUserId,
            when,
            manual ? "ManualCall" : null,
            normalizedReason);
        return Result.Ok();
    }

    private Result CancelCore(
        Guid? actorApplicationUserId,
        string reasonCode,
        string? reason,
        DateTime occurredOnUtc,
        TicketHistoryEventType eventType)
    {
        var previous = Status;
        Status = TicketStatus.Cancelled;
        CancelledOnUtc = occurredOnUtc;
        CancellationReasonCode = reasonCode;
        CancellationReason = reason;
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(eventType, previous, Status, actorApplicationUserId,
            occurredOnUtc, reasonCode, reason);
        return Result.Ok();
    }

    private void AddCallAttempt(DateTime occurredOnUtc)
        => _callAttempts.Add(TicketCallAttempt.Create(
            Guid.NewGuid(), Id, CallCycle, CurrentCycleAttempts() + 1, occurredOnUtc));

    private int CurrentCycleAttempts()
        => _callAttempts.Count(item => item.CallCycle == CallCycle);

    private void Touch(Guid? actorApplicationUserId, DateTime occurredOnUtc)
    {
        ModifiedByApplicationUserId = actorApplicationUserId;
        ModifiedOnUtc = occurredOnUtc;
        LastUpdatedOnUtc = occurredOnUtc;
    }

    private void AppendHistory(
        TicketHistoryEventType eventType,
        TicketStatus? fromStatus,
        TicketStatus? toStatus,
        Guid? actorApplicationUserId,
        DateTime occurredOnUtc,
        string? reasonCode,
        string? reason)
        => _history.Add(TicketHistory.Create(
            Guid.NewGuid(), Id, eventType, fromStatus, toStatus,
            actorApplicationUserId, occurredOnUtc, reasonCode, reason));

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class TicketHistory : Entity<Guid>
{
    private TicketHistory()
    {
    }

    private TicketHistory(
        Guid id,
        Guid ticketId,
        TicketHistoryEventType eventType,
        TicketStatus? fromStatus,
        TicketStatus? toStatus,
        Guid? actorApplicationUserId,
        DateTime occurredOnUtc,
        string? reasonCode,
        string? reason) : base(id)
    {
        TicketId = ticketId;
        EventType = eventType;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorApplicationUserId = actorApplicationUserId;
        OccurredOnUtc = occurredOnUtc.Kind == DateTimeKind.Utc
            ? occurredOnUtc
            : occurredOnUtc.ToUniversalTime();
        ReasonCode = Normalize(reasonCode, TicketPolicy.ReasonCodeMaxLength);
        Reason = Normalize(reason, TicketPolicy.ReasonMaxLength);
    }

    public Guid TicketId { get; private set; }
    public TicketHistoryEventType EventType { get; private set; }
    public TicketStatus? FromStatus { get; private set; }
    public TicketStatus? ToStatus { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? Reason { get; private set; }

    internal static TicketHistory Create(
        Guid id,
        Guid ticketId,
        TicketHistoryEventType eventType,
        TicketStatus? fromStatus,
        TicketStatus? toStatus,
        Guid? actorApplicationUserId,
        DateTime occurredOnUtc,
        string? reasonCode,
        string? reason)
        => new(id, ticketId, eventType, fromStatus, toStatus,
            actorApplicationUserId, occurredOnUtc, reasonCode, reason);

    private static string? Normalize(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized?.Length > maximumLength ? normalized[..maximumLength] : normalized;
    }
}

public sealed class TicketCallAttempt : Entity<Guid>
{
    private TicketCallAttempt()
    {
    }

    private TicketCallAttempt(
        Guid id,
        Guid ticketId,
        int callCycle,
        int attemptNumber,
        DateTime calledOnUtc) : base(id)
    {
        TicketId = ticketId;
        CallCycle = callCycle;
        AttemptNumber = attemptNumber;
        CalledOnUtc = calledOnUtc;
        Outcome = TicketCallAttemptOutcome.Pending;
    }

    public Guid TicketId { get; private set; }
    public int CallCycle { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTime CalledOnUtc { get; private set; }
    public TicketCallAttemptOutcome Outcome { get; private set; }
    public DateTime? OutcomeRecordedOnUtc { get; private set; }

    internal static TicketCallAttempt Create(
        Guid id,
        Guid ticketId,
        int callCycle,
        int attemptNumber,
        DateTime calledOnUtc)
        => new(id, ticketId, callCycle, attemptNumber, calledOnUtc);

    internal void ConfirmNoResponse(DateTime occurredOnUtc)
    {
        if (Outcome != TicketCallAttemptOutcome.Pending)
        {
            return;
        }

        Outcome = TicketCallAttemptOutcome.NoResponse;
        OutcomeRecordedOnUtc = occurredOnUtc;
    }

    internal void MarkResponded(DateTime occurredOnUtc)
    {
        if (Outcome != TicketCallAttemptOutcome.Pending)
        {
            return;
        }

        Outcome = TicketCallAttemptOutcome.Responded;
        OutcomeRecordedOnUtc = occurredOnUtc;
    }
}

public sealed class TicketDailyCounter : AggregateRoot<Guid>
{
    private TicketDailyCounter()
    {
    }

    public TicketDailyCounter(Guid id, Guid doctorPracticeId, DateOnly businessDate, int lastNumber)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        BusinessDate = businessDate;
        LastNumber = lastNumber;
    }

    public Guid DoctorPracticeId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public int LastNumber { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public int AllocateNext()
    {
        LastNumber++;
        return LastNumber;
    }
}

public sealed class TicketIdempotencyRecord : AggregateRoot<Guid>
{
    private TicketIdempotencyRecord()
    {
    }

    public TicketIdempotencyRecord(
        Guid id,
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        string requestFingerprint,
        DateTime createdOnUtc) : base(id)
    {
        ActorApplicationUserId = actorApplicationUserId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid ActorApplicationUserId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid? TicketId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? CompletedOnUtc { get; private set; }

    public void Complete(Guid ticketId, DateTime completedOnUtc)
    {
        if (CompletedOnUtc.HasValue)
        {
            return;
        }

        TicketId = ticketId;
        CompletedOnUtc = completedOnUtc;
    }
}

public static class TicketErrors
{
    public static Error InvalidCreation => Error.Validation(
        "Ticket.InvalidCreation", "Ticket creation data is invalid.");
    public static Error InvalidTransition => Error.Conflict(
        "Ticket.InvalidTransition", "The requested ticket transition is not allowed.");
    public static Error NotFound => Error.NotFound(
        "Ticket.NotFound", "Ticket was not found.");
    public static Error AccessDenied => Error.Security(
        "Ticket.AccessDenied", "You are not authorized to perform this ticket operation.");
    public static Error PracticeUnavailable => Error.Conflict(
        "Ticket.PracticeUnavailable", "The doctor practice is not operationally available.");
    public static Error PaymentRequired => Error.Conflict(
        "Ticket.PaymentRequired", "Full payment is required before queue admission.");
    public static Error CheckInWindowNotOpen => Error.Conflict(
        "Ticket.CheckInWindowNotOpen", "The reservation check-in window is not open.");
    public static Error BusinessDateMismatch => Error.Conflict(
        "Ticket.BusinessDateMismatch", "The ticket can only be created on the current practice business date.");
    public static Error DuplicateReservation => Error.Conflict(
        "Ticket.DuplicateReservation", "A ticket has already been created from this reservation.");
    public static Error OpenTicketExists => Error.Conflict(
        "Ticket.OpenTicketExists", "The patient already has an open ticket in this practice.");
    public static Error QueueBusy => Error.Conflict(
        "Ticket.QueueBusy", "The practice queue already has a called or in-progress ticket.");
    public static Error IdempotencyKeyRequired => Error.Validation(
        "Ticket.IdempotencyKeyRequired", "Idempotency-Key is required.");
    public static Error IdempotencyKeyReused => Error.Conflict(
        "Ticket.IdempotencyKeyReused", "The idempotency key was already used with a different request.");
    public static Error ConcurrencyConflict => Error.Conflict(
        "Ticket.ConcurrencyConflict", "The ticket changed while the request was being processed.");
    public static Error WalkInNotAllowed => Error.Conflict(
        "Ticket.WalkInNotAllowed", "Walk-in tickets are not enabled for this practice.");
    public static Error InvalidCatalog => Error.Conflict(
        "Ticket.InvalidCatalog", "The selected segment, visit type, or price is not active for this practice.");
}
