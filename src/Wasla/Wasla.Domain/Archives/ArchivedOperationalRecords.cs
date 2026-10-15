using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;

namespace Wasla.Domain.Archives;

// Immutable original scalar snapshot; public API never accepts this type.
public sealed class ArchivedTicket
{
    public Guid Id { get; private set; }
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
    public Guid? FollowUpEligibilityId { get; private set; }
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
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}

// Immutable original scalar snapshot; public API never accepts this type.
public sealed class ArchivedTicketHistory
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public TicketHistoryEventType EventType { get; private set; }
    public TicketStatus? FromStatus { get; private set; }
    public TicketStatus? ToStatus { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? Reason { get; private set; }
}

// Immutable original scalar snapshot; public API never accepts this type.
public sealed class ArchivedTicketCallAttempt
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public int CallCycle { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTime CalledOnUtc { get; private set; }
    public TicketCallAttemptOutcome Outcome { get; private set; }
    public DateTime? OutcomeRecordedOnUtc { get; private set; }
}

// Immutable original scalar snapshot; public API never accepts this type.
public sealed class ArchivedReservation
{
    public Guid Id { get; private set; }
    public string ReservationReference { get; private set; } = string.Empty;
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid SegmentId { get; private set; }
    public Guid VisitTypeId { get; private set; }
    public Guid? FollowUpEligibilityId { get; private set; }
    public ReservationBookingSource BookingSource { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public string? BookedOnBehalfRelationshipSnapshot { get; private set; }
    public DateTime ScheduledStartUtc { get; private set; }
    public DateTime ScheduledLocalDateTime { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string TimeZoneIdSnapshot { get; private set; } = string.Empty;
    public int SlotDurationMinutesSnapshot { get; private set; }
    public string SegmentNameArSnapshot { get; private set; } = string.Empty;
    public string? SegmentNameEnSnapshot { get; private set; }
    public int SegmentPrioritySnapshot { get; private set; }
    public string VisitTypeCodeSnapshot { get; private set; } = string.Empty;
    public string VisitTypeNameArSnapshot { get; private set; } = string.Empty;
    public string? VisitTypeNameEnSnapshot { get; private set; }
    public decimal PriceSnapshot { get; private set; }
    public int? CheckInGracePeriodMinutesSnapshot { get; private set; }
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
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}

// Immutable original scalar snapshot; public API never accepts this type.
public sealed class ArchivedReservationHistory
{
    public Guid Id { get; private set; }
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
}
