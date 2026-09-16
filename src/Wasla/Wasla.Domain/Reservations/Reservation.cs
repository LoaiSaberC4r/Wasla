using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Reservations;

public enum ReservationStatus
{
    Active = 1,
    Cancelled = 2,
    NoShow = 3,
    Expired = 4,
    ConvertedToTicket = 5
}

public enum ReservationBookingSource
{
    Patient = 1,
    FamilyMember = 2,
    Reception = 3
}

public enum ReservationHistoryEventType
{
    Created = 1,
    Rescheduled = 2,
    Cancelled = 3,
    MarkedNoShow = 4,
    RestoredFromNoShow = 5,
    Expired = 6,
    ConvertedToTicket = 7
}

public enum ReservationActionInitiator
{
    Patient = 1,
    Guardian = 2,
    Reception = 3,
    Doctor = 4,
    System = 5
}

public enum ReservationCancellationInitiator
{
    Patient = 1,
    Guardian = 2,
    Reception = 3,
    Doctor = 4,
    System = 5
}

public static class ReservationPolicy
{
    public const int MaximumAdvanceBookingDays = 30;
    public const int MaxPatientReschedulesPerReservation = 2;
    public const int BookingNoteMaxLength = 1000;
    public const int ReasonMaxLength = 1000;
}

public static class ReservationCancellationReasons
{
    public const string PatientChangedPlans = "PatientChangedPlans";
    public const string PatientUnavailable = "PatientUnavailable";
    public const string DuplicateBooking = "DuplicateBooking";
    public const string ProviderUnavailable = "ProviderUnavailable";
    public const string ScheduleChanged = "ScheduleChanged";
    public const string DoctorSuspended = "DoctorSuspended";
    public const string Other = "Other";

    public static readonly IReadOnlySet<string> PatientReasons = new HashSet<string>(
        [PatientChangedPlans, PatientUnavailable, DuplicateBooking, Other],
        StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> ProviderReasons = new HashSet<string>(
        [ProviderUnavailable, ScheduleChanged, DoctorSuspended, Other],
        StringComparer.Ordinal);
}

public sealed record ReservationCreationSnapshot(
    Guid Id,
    string ReservationReference,
    Guid DoctorId,
    Guid DoctorPracticeId,
    Guid PatientId,
    Guid SegmentId,
    Guid VisitTypeId,
    ReservationBookingSource BookingSource,
    Guid CreatedByApplicationUserId,
    string? BookedOnBehalfRelationshipSnapshot,
    DateTime ScheduledStartUtc,
    DateTime ScheduledLocalDateTime,
    DateOnly BusinessDate,
    string TimeZoneIdSnapshot,
    int SlotDurationMinutesSnapshot,
    string SegmentNameArSnapshot,
    string? SegmentNameEnSnapshot,
    int SegmentPrioritySnapshot,
    string VisitTypeCodeSnapshot,
    string VisitTypeNameArSnapshot,
    string? VisitTypeNameEnSnapshot,
    decimal PriceSnapshot,
    string? BookingNote,
    DateTime OccurredOnUtc);

public sealed class Reservation : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<ReservationHistory> _history = [];

    private Reservation()
    {
    }

    private Reservation(ReservationCreationSnapshot snapshot) : base(snapshot.Id)
    {
        ReservationReference = snapshot.ReservationReference;
        DoctorId = snapshot.DoctorId;
        DoctorPracticeId = snapshot.DoctorPracticeId;
        PatientId = snapshot.PatientId;
        SegmentId = snapshot.SegmentId;
        VisitTypeId = snapshot.VisitTypeId;
        BookingSource = snapshot.BookingSource;
        CreatedByApplicationUserId = snapshot.CreatedByApplicationUserId;
        BookedOnBehalfRelationshipSnapshot = snapshot.BookedOnBehalfRelationshipSnapshot;
        SetAppointment(
            snapshot.ScheduledStartUtc,
            snapshot.ScheduledLocalDateTime,
            snapshot.BusinessDate,
            snapshot.SlotDurationMinutesSnapshot);
        TimeZoneIdSnapshot = snapshot.TimeZoneIdSnapshot;
        SegmentNameArSnapshot = snapshot.SegmentNameArSnapshot;
        SegmentNameEnSnapshot = snapshot.SegmentNameEnSnapshot;
        SegmentPrioritySnapshot = snapshot.SegmentPrioritySnapshot;
        VisitTypeCodeSnapshot = snapshot.VisitTypeCodeSnapshot;
        VisitTypeNameArSnapshot = snapshot.VisitTypeNameArSnapshot;
        VisitTypeNameEnSnapshot = snapshot.VisitTypeNameEnSnapshot;
        PriceSnapshot = snapshot.PriceSnapshot;
        BookingNote = Normalize(snapshot.BookingNote);
        Status = ReservationStatus.Active;
        CreatedOnUtc = RequireUtc(snapshot.OccurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.Created,
            null,
            ReservationStatus.Active,
            ToInitiator(snapshot.BookingSource),
            snapshot.CreatedByApplicationUserId,
            snapshot.OccurredOnUtc);
    }

    public string ReservationReference { get; private set; } = string.Empty;
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid SegmentId { get; private set; }
    public Guid VisitTypeId { get; private set; }
    public ReservationBookingSource BookingSource { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public string? BookedOnBehalfRelationshipSnapshot { get; private set; }
    public DateTime ScheduledStartUtc { get; private set; }
    public DateTime ScheduledLocalDateTime { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string TimeZoneIdSnapshot { get; private set; } = string.Empty;
    public int SlotDurationMinutesSnapshot { get; private set; }
    public DateTime ScheduledEndUtc => ScheduledStartUtc.AddMinutes(SlotDurationMinutesSnapshot);
    public string SegmentNameArSnapshot { get; private set; } = string.Empty;
    public string? SegmentNameEnSnapshot { get; private set; }
    public int SegmentPrioritySnapshot { get; private set; }
    public string VisitTypeCodeSnapshot { get; private set; } = string.Empty;
    public string VisitTypeNameArSnapshot { get; private set; } = string.Empty;
    public string? VisitTypeNameEnSnapshot { get; private set; }
    public decimal PriceSnapshot { get; private set; }
    public string? BookingNote { get; private set; }
    public ReservationStatus Status { get; private set; }
    public int PatientInitiatedRescheduleCount { get; private set; }
    public DateTime? LastRescheduledOnUtc { get; private set; }
    public ReservationCancellationInitiator? CancellationInitiator { get; private set; }
    public string? CancellationReasonCode { get; private set; }
    public string? CancellationComment { get; private set; }
    public Guid? CancelledByApplicationUserId { get; private set; }
    public DateTime? CancelledOnUtc { get; private set; }
    public DateTime? LastNoShowOnUtc { get; private set; }
    public DateTime? LastNoShowRestoredOnUtc { get; private set; }
    public DateTime? ExpiredOnUtc { get; private set; }
    public DateTime? ConvertedToTicketOnUtc { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<ReservationHistory> History => _history.AsReadOnly();

    public bool ConsumesCapacity => Status is ReservationStatus.Active or ReservationStatus.ConvertedToTicket;

    public bool IsLate(DateTime nowUtc, int checkInGracePeriodMinutes)
        => Status == ReservationStatus.Active &&
           RequireUtc(nowUtc) > ScheduledEndFromStart(checkInGracePeriodMinutes);

    public static Result<Reservation> Create(ReservationCreationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Id == Guid.Empty || snapshot.DoctorId == Guid.Empty ||
            snapshot.DoctorPracticeId == Guid.Empty || snapshot.PatientId == Guid.Empty ||
            snapshot.SegmentId == Guid.Empty || snapshot.VisitTypeId == Guid.Empty ||
            snapshot.CreatedByApplicationUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.ReservationReference) || snapshot.ReservationReference.Length > 32 ||
            !Enum.IsDefined(snapshot.BookingSource) || snapshot.ScheduledStartUtc.Kind != DateTimeKind.Utc ||
            snapshot.ScheduledLocalDateTime.Kind == DateTimeKind.Utc || snapshot.BusinessDate == default ||
            string.IsNullOrWhiteSpace(snapshot.TimeZoneIdSnapshot) || snapshot.TimeZoneIdSnapshot.Length > 100 ||
            snapshot.SlotDurationMinutesSnapshot is < 5 or > 480 ||
            string.IsNullOrWhiteSpace(snapshot.SegmentNameArSnapshot) ||
            string.IsNullOrWhiteSpace(snapshot.VisitTypeCodeSnapshot) ||
            string.IsNullOrWhiteSpace(snapshot.VisitTypeNameArSnapshot) || snapshot.PriceSnapshot <= 0 ||
            Normalize(snapshot.BookingNote)?.Length > ReservationPolicy.BookingNoteMaxLength ||
            snapshot.BookingSource == ReservationBookingSource.FamilyMember &&
            string.IsNullOrWhiteSpace(snapshot.BookedOnBehalfRelationshipSnapshot))
        {
            return Result<Reservation>.Fail(ReservationErrors.InvalidState);
        }

        return Result<Reservation>.Ok(new Reservation(snapshot));
    }

    public Result Cancel(
        ReservationCancellationInitiator cancellationInitiator,
        ReservationActionInitiator actionInitiator,
        Guid? actorApplicationUserId,
        string reasonCode,
        string? comment,
        DateTime occurredOnUtc)
    {
        var normalizedReason = Normalize(reasonCode);
        var normalizedComment = Normalize(comment);
        if (Status != ReservationStatus.Active)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        if (!Enum.IsDefined(cancellationInitiator) || !Enum.IsDefined(actionInitiator) ||
            string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 100 ||
            normalizedComment?.Length > ReservationPolicy.ReasonMaxLength ||
            actionInitiator != ReservationActionInitiator.System && actorApplicationUserId is null)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        var from = Status;
        Status = ReservationStatus.Cancelled;
        CancellationInitiator = cancellationInitiator;
        CancellationReasonCode = normalizedReason;
        CancellationComment = normalizedComment;
        CancelledByApplicationUserId = actorApplicationUserId;
        CancelledOnUtc = RequireUtc(occurredOnUtc);
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.Cancelled,
            from,
            Status,
            actionInitiator,
            actorApplicationUserId,
            occurredOnUtc,
            normalizedReason,
            normalizedComment);
        return Result.Ok();
    }

    public Result Reschedule(
        DateTime scheduledStartUtc,
        DateTime scheduledLocalDateTime,
        DateOnly businessDate,
        int slotDurationMinutes,
        ReservationActionInitiator initiator,
        Guid actorApplicationUserId,
        bool patientConsentConfirmed,
        string? reason,
        DateTime occurredOnUtc)
    {
        if (Status != ReservationStatus.Active)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        var isPatientAction = initiator is ReservationActionInitiator.Patient or ReservationActionInitiator.Guardian;
        if (!isPatientAction && !patientConsentConfirmed)
        {
            return Result.Fail(ReservationErrors.PatientConsentRequired);
        }

        if (isPatientAction && PatientInitiatedRescheduleCount >= ReservationPolicy.MaxPatientReschedulesPerReservation)
        {
            return Result.Fail(ReservationErrors.RescheduleLimitReached);
        }

        var normalizedReason = Normalize(reason);
        if (!isPatientAction && string.IsNullOrWhiteSpace(normalizedReason))
        {
            return Result.Fail(ReservationErrors.PatientConsentRequired);
        }

        if (actorApplicationUserId == Guid.Empty || !Enum.IsDefined(initiator) ||
            scheduledStartUtc.Kind != DateTimeKind.Utc || scheduledLocalDateTime.Kind == DateTimeKind.Utc ||
            businessDate == default || slotDurationMinutes is < 5 or > 480 ||
            normalizedReason?.Length > ReservationPolicy.ReasonMaxLength)
        {
            return Result.Fail(ReservationErrors.InvalidSlot);
        }

        var oldStartUtc = ScheduledStartUtc;
        var oldLocal = ScheduledLocalDateTime;
        var oldBusinessDate = BusinessDate;
        var oldDuration = SlotDurationMinutesSnapshot;
        SetAppointment(scheduledStartUtc, scheduledLocalDateTime, businessDate, slotDurationMinutes);
        if (isPatientAction)
        {
            PatientInitiatedRescheduleCount++;
        }

        LastRescheduledOnUtc = RequireUtc(occurredOnUtc);
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.Rescheduled,
            ReservationStatus.Active,
            ReservationStatus.Active,
            initiator,
            actorApplicationUserId,
            occurredOnUtc,
            reason: normalizedReason,
            oldBusinessDate: oldBusinessDate,
            oldScheduledLocalDateTime: oldLocal,
            oldScheduledStartUtc: oldStartUtc,
            oldSlotDurationMinutes: oldDuration,
            newBusinessDate: BusinessDate,
            newScheduledLocalDateTime: ScheduledLocalDateTime,
            newScheduledStartUtc: ScheduledStartUtc,
            newSlotDurationMinutes: SlotDurationMinutesSnapshot,
            patientConsentConfirmed: !isPatientAction && patientConsentConfirmed);
        return Result.Ok();
    }

    public Result MarkNoShow(Guid? actorApplicationUserId, DateTime occurredOnUtc)
    {
        if (Status != ReservationStatus.Active)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        Status = ReservationStatus.NoShow;
        LastNoShowOnUtc = RequireUtc(occurredOnUtc);
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.MarkedNoShow,
            ReservationStatus.Active,
            Status,
            actorApplicationUserId.HasValue ? ReservationActionInitiator.Reception : ReservationActionInitiator.System,
            actorApplicationUserId,
            occurredOnUtc);
        return Result.Ok();
    }

    public Result RestoreFromNoShow(Guid actorApplicationUserId, DateTime occurredOnUtc)
    {
        if (Status != ReservationStatus.NoShow || actorApplicationUserId == Guid.Empty)
        {
            return Result.Fail(ReservationErrors.NoShowRestoreNotAllowed);
        }

        Status = ReservationStatus.Active;
        LastNoShowRestoredOnUtc = RequireUtc(occurredOnUtc);
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.RestoredFromNoShow,
            ReservationStatus.NoShow,
            Status,
            ReservationActionInitiator.Reception,
            actorApplicationUserId,
            occurredOnUtc);
        return Result.Ok();
    }

    public Result Expire(DateTime occurredOnUtc)
    {
        if (Status != ReservationStatus.Active)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        Status = ReservationStatus.Expired;
        ExpiredOnUtc = RequireUtc(occurredOnUtc);
        Touch(null, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.Expired,
            ReservationStatus.Active,
            Status,
            ReservationActionInitiator.System,
            null,
            occurredOnUtc);
        return Result.Ok();
    }

    public Result ConvertToTicket(Guid? actorApplicationUserId, DateTime occurredOnUtc)
    {
        if (Status != ReservationStatus.Active)
        {
            return Result.Fail(ReservationErrors.InvalidState);
        }

        Status = ReservationStatus.ConvertedToTicket;
        ConvertedToTicketOnUtc = RequireUtc(occurredOnUtc);
        Touch(actorApplicationUserId, occurredOnUtc);
        AppendHistory(
            ReservationHistoryEventType.ConvertedToTicket,
            ReservationStatus.Active,
            Status,
            actorApplicationUserId.HasValue ? ReservationActionInitiator.Reception : ReservationActionInitiator.System,
            actorApplicationUserId,
            occurredOnUtc);
        return Result.Ok();
    }

    private void SetAppointment(
        DateTime scheduledStartUtc,
        DateTime scheduledLocalDateTime,
        DateOnly businessDate,
        int slotDurationMinutes)
    {
        ScheduledStartUtc = RequireUtc(scheduledStartUtc);
        ScheduledLocalDateTime = DateTime.SpecifyKind(scheduledLocalDateTime, DateTimeKind.Unspecified);
        BusinessDate = businessDate;
        SlotDurationMinutesSnapshot = slotDurationMinutes;
    }

    private void Touch(Guid? actorApplicationUserId, DateTime occurredOnUtc)
    {
        ModifiedByApplicationUserId = actorApplicationUserId;
        ModifiedOnUtc = RequireUtc(occurredOnUtc);
    }

    private DateTime ScheduledEndFromStart(int minutes) => ScheduledStartUtc.AddMinutes(minutes);

    private void AppendHistory(
        ReservationHistoryEventType eventType,
        ReservationStatus? fromStatus,
        ReservationStatus? toStatus,
        ReservationActionInitiator initiator,
        Guid? actorApplicationUserId,
        DateTime occurredOnUtc,
        string? reasonCode = null,
        string? reason = null,
        DateOnly? oldBusinessDate = null,
        DateTime? oldScheduledLocalDateTime = null,
        DateTime? oldScheduledStartUtc = null,
        int? oldSlotDurationMinutes = null,
        DateOnly? newBusinessDate = null,
        DateTime? newScheduledLocalDateTime = null,
        DateTime? newScheduledStartUtc = null,
        int? newSlotDurationMinutes = null,
        bool? patientConsentConfirmed = null)
        => _history.Add(ReservationHistory.Create(
            Guid.NewGuid(),
            Id,
            eventType,
            fromStatus,
            toStatus,
            initiator,
            actorApplicationUserId,
            occurredOnUtc,
            reasonCode,
            reason,
            oldBusinessDate,
            oldScheduledLocalDateTime,
            oldScheduledStartUtc,
            oldSlotDurationMinutes,
            newBusinessDate,
            newScheduledLocalDateTime,
            newScheduledStartUtc,
            newSlotDurationMinutes,
            patientConsentConfirmed));

    private static ReservationActionInitiator ToInitiator(ReservationBookingSource source)
        => source switch
        {
            ReservationBookingSource.Patient => ReservationActionInitiator.Patient,
            ReservationBookingSource.FamilyMember => ReservationActionInitiator.Guardian,
            ReservationBookingSource.Reception => ReservationActionInitiator.Reception,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ReservationHistory : Entity<Guid>
{
    private ReservationHistory()
    {
    }

    private ReservationHistory(
        Guid id,
        Guid reservationId,
        ReservationHistoryEventType eventType,
        ReservationStatus? fromStatus,
        ReservationStatus? toStatus,
        ReservationActionInitiator actionInitiator,
        Guid? performedByApplicationUserId,
        DateTime occurredOnUtc) : base(id)
    {
        ReservationId = reservationId;
        EventType = eventType;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActionInitiator = actionInitiator;
        PerformedByApplicationUserId = performedByApplicationUserId;
        OccurredOnUtc = occurredOnUtc.Kind == DateTimeKind.Utc ? occurredOnUtc : occurredOnUtc.ToUniversalTime();
    }

    public Guid ReservationId { get; private set; }
    public ReservationHistoryEventType EventType { get; private set; }
    public ReservationStatus? FromStatus { get; private set; }
    public ReservationStatus? ToStatus { get; private set; }
    public ReservationActionInitiator ActionInitiator { get; private set; }
    public Guid? PerformedByApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? Reason { get; private set; }
    public DateOnly? OldBusinessDate { get; private set; }
    public DateTime? OldScheduledLocalDateTime { get; private set; }
    public DateTime? OldScheduledStartUtc { get; private set; }
    public int? OldSlotDurationMinutes { get; private set; }
    public DateOnly? NewBusinessDate { get; private set; }
    public DateTime? NewScheduledLocalDateTime { get; private set; }
    public DateTime? NewScheduledStartUtc { get; private set; }
    public int? NewSlotDurationMinutes { get; private set; }
    public bool? PatientConsentConfirmed { get; private set; }

    internal static ReservationHistory Create(
        Guid id,
        Guid reservationId,
        ReservationHistoryEventType eventType,
        ReservationStatus? fromStatus,
        ReservationStatus? toStatus,
        ReservationActionInitiator actionInitiator,
        Guid? performedByApplicationUserId,
        DateTime occurredOnUtc,
        string? reasonCode,
        string? reason,
        DateOnly? oldBusinessDate,
        DateTime? oldScheduledLocalDateTime,
        DateTime? oldScheduledStartUtc,
        int? oldSlotDurationMinutes,
        DateOnly? newBusinessDate,
        DateTime? newScheduledLocalDateTime,
        DateTime? newScheduledStartUtc,
        int? newSlotDurationMinutes,
        bool? patientConsentConfirmed)
        => new(id, reservationId, eventType, fromStatus, toStatus, actionInitiator,
            performedByApplicationUserId, occurredOnUtc)
        {
            ReasonCode = Normalize(reasonCode, 100),
            Reason = Normalize(reason, ReservationPolicy.ReasonMaxLength),
            OldBusinessDate = oldBusinessDate,
            OldScheduledLocalDateTime = oldScheduledLocalDateTime,
            OldScheduledStartUtc = oldScheduledStartUtc,
            OldSlotDurationMinutes = oldSlotDurationMinutes,
            NewBusinessDate = newBusinessDate,
            NewScheduledLocalDateTime = newScheduledLocalDateTime,
            NewScheduledStartUtc = newScheduledStartUtc,
            NewSlotDurationMinutes = newSlotDurationMinutes,
            PatientConsentConfirmed = patientConsentConfirmed
        };

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized?.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}

public sealed class ReservationIdempotencyRecord : Entity<Guid>
{
    private ReservationIdempotencyRecord()
    {
    }

    public ReservationIdempotencyRecord(
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
        CreatedOnUtc = createdOnUtc.Kind == DateTimeKind.Utc ? createdOnUtc : createdOnUtc.ToUniversalTime();
    }

    public Guid ActorApplicationUserId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid? ReservationId { get; private set; }
    public string? ResultReference { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? CompletedOnUtc { get; private set; }

    public void Complete(Guid reservationId, string resultReference, DateTime completedOnUtc)
    {
        if (CompletedOnUtc.HasValue)
        {
            return;
        }

        ReservationId = reservationId;
        ResultReference = resultReference;
        CompletedOnUtc = completedOnUtc.Kind == DateTimeKind.Utc ? completedOnUtc : completedOnUtc.ToUniversalTime();
    }
}

public static class ReservationErrors
{
    private static string Text(string key) => ErrorMessage.GetString(key);

    public static Error NotFound => Error.NotFound("Reservation.NotFound", Text("ReservationNotFound"));
    public static Error InvalidState => Error.Conflict("Reservation.InvalidState", Text("ReservationInvalidState"));
    public static Error InvalidSlot => Error.Validation("Reservation.InvalidSlot", Text("ReservationInvalidSlot"));
    public static Error SlotNotAvailable => Error.Conflict("Reservation.SlotNotAvailable", Text("ReservationSlotNotAvailable"));
    public static Error BookingHorizonExceeded => Error.Validation("Reservation.BookingHorizonExceeded", Text("ReservationBookingHorizonExceeded"));
    public static Error OnlineBookingDisabled => Error.Conflict("Reservation.OnlineBookingDisabled", Text("ReservationOnlineBookingDisabled"));
    public static Error DailyCapacityExceeded => Error.Conflict("Reservation.DailyCapacityExceeded", Text("ReservationDailyCapacityExceeded"));
    public static Error SegmentCapacityExceeded => Error.Conflict("Reservation.SegmentCapacityExceeded", Text("ReservationSegmentCapacityExceeded"));
    public static Error PatientPracticeDateConflict => Error.Conflict("Reservation.PatientPracticeDateConflict", Text("ReservationPatientPracticeDateConflict"));
    public static Error PatientAppointmentOverlap => Error.Conflict("Reservation.PatientAppointmentOverlap", Text("ReservationPatientAppointmentOverlap"));
    public static Error FutureConsultationAlreadyExists => Error.Conflict("Reservation.FutureConsultationAlreadyExists", Text("ReservationFutureConsultationAlreadyExists"));
    public static Error SameDayNoShowBookingBlocked => Error.Conflict("Reservation.SameDayNoShowBookingBlocked", Text("ReservationSameDayNoShowBookingBlocked"));
    public static Error CancellationCutoffReached => Error.Conflict("Reservation.CancellationCutoffReached", Text("ReservationCancellationCutoffReached"));
    public static Error RescheduleCutoffReached => Error.Conflict("Reservation.RescheduleCutoffReached", Text("ReservationRescheduleCutoffReached"));
    public static Error RescheduleLimitReached => Error.Conflict("Reservation.RescheduleLimitReached", Text("ReservationRescheduleLimitReached"));
    public static Error PatientConsentRequired => Error.Validation("Reservation.PatientConsentRequired", Text("ReservationPatientConsentRequired"));
    public static Error NoShowRestoreNotAllowed => Error.Conflict("Reservation.NoShowRestoreNotAllowed", Text("ReservationNoShowRestoreNotAllowed"));
    public static Error NoShowRestoreCapacityUnavailable => Error.Conflict("Reservation.NoShowRestoreCapacityUnavailable", Text("ReservationNoShowRestoreCapacityUnavailable"));
    public static Error FollowUpNotBookable => Error.Conflict("Reservation.FollowUpNotBookable", Text("ReservationFollowUpNotBookable"));
    public static Error ConcurrencyConflict => Error.Conflict("Reservation.ConcurrencyConflict", Text("ReservationConcurrencyConflict"));
    public static Error ReferenceConflict => Error.Conflict("Reservation.ReferenceConflict", Text("ReservationReferenceConflict"));
    public static Error IdempotencyKeyReused => Error.Conflict("Reservation.IdempotencyKeyReused", Text("ReservationIdempotencyKeyReused"));
    public static Error AccessDenied => Error.Security("Reservation.AccessDenied", Text("ReservationAccessDenied"));
}
