using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Clinical;

public enum FollowUpEligibilityStatus { Available = 1, Reserved = 2, Consumed = 3, Expired = 4 }

public sealed class FollowUpEligibility : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<FollowUpEligibilityHistory> _history = [];
    private FollowUpEligibility() { }
    public Guid SourceMedicalEncounterId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PatientId { get; private set; }
    public FollowUpEligibilityStatus Status { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public Guid? ReservedReservationId { get; private set; }
    public Guid? ReservedTicketId { get; private set; }
    public Guid? ConsumedEncounterId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<FollowUpEligibilityHistory> History => _history.AsReadOnly();

    public static Result<FollowUpEligibility> Create(MedicalEncounter source, DateOnly validUntil,
        DateOnly businessToday, Guid actor, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Status != EncounterStatus.Completed) return Result<FollowUpEligibility>.Fail(ClinicalErrors.InvalidState);
        if (validUntil == default || businessToday == default || validUntil < businessToday)
            return Result<FollowUpEligibility>.Fail(FollowUpErrors.InvalidDate);
        var eligibility = new FollowUpEligibility { Id = Guid.NewGuid(), SourceMedicalEncounterId = source.Id,
            DoctorPracticeId = source.DoctorPracticeId, DoctorId = source.DoctorId, PatientId = source.PatientId,
            Status = FollowUpEligibilityStatus.Available, ValidUntil = validUntil,
            CreatedOnUtc = nowUtc, CreatedByApplicationUserId = actor };
        eligibility.Record(null, "Created", actor, nowUtc);
        return Result<FollowUpEligibility>.Ok(eligibility);
    }

    public FollowUpEligibilityStatus EffectiveStatus(DateOnly today)
        => Status == FollowUpEligibilityStatus.Available && today > ValidUntil ? FollowUpEligibilityStatus.Expired : Status;

    public Result ValidateScope(Guid patientId, Guid practiceId, Guid doctorId)
    {
        if (patientId != PatientId) return Result.Fail(FollowUpErrors.InvalidPatient);
        if (practiceId != DoctorPracticeId) return Result.Fail(FollowUpErrors.InvalidPractice);
        return doctorId == DoctorId ? Result.Ok() : Result.Fail(FollowUpErrors.InvalidDoctor);
    }

    public Result ValidateAvailable(DateOnly today, DateOnly visitDate)
    {
        if (Status == FollowUpEligibilityStatus.Consumed) return Result.Fail(FollowUpErrors.AlreadyConsumed);
        if (Status == FollowUpEligibilityStatus.Expired || today > ValidUntil) return Result.Fail(FollowUpErrors.Expired);
        if (Status == FollowUpEligibilityStatus.Reserved) return Result.Fail(FollowUpErrors.AlreadyReserved);
        if (visitDate == default || visitDate > ValidUntil) return Result.Fail(FollowUpErrors.DateOutsideEligibility);
        return Result.Ok();
    }

    public Result Reserve(Guid? reservationId, Guid? ticketId, DateOnly today, DateOnly visitDate, Guid? actor, DateTime nowUtc)
    {
        if (reservationId.HasValue == ticketId.HasValue || reservationId == Guid.Empty || ticketId == Guid.Empty)
            return Result.Fail(FollowUpErrors.NotAvailable);
        var valid = ValidateAvailable(today, visitDate);
        if (valid.IsFailure) return valid;
        var previous = Status;
        Status = FollowUpEligibilityStatus.Reserved;
        ReservedReservationId = reservationId;
        ReservedTicketId = ticketId;
        Record(previous, "Reserved", actor, nowUtc);
        return Result.Ok();
    }

    public Result ValidateReservation(Guid reservationId, DateOnly today, DateOnly visitDate)
    {
        if (Status != FollowUpEligibilityStatus.Reserved || ReservedReservationId != reservationId || ReservedTicketId is not null)
            return Result.Fail(FollowUpErrors.NotAvailable);
        if (today > ValidUntil) return Result.Fail(FollowUpErrors.Expired);
        return visitDate <= ValidUntil ? Result.Ok() : Result.Fail(FollowUpErrors.DateOutsideEligibility);
    }

    public Result TransferToTicket(Guid reservationId, Guid ticketId, DateOnly today, Guid actor, DateTime nowUtc)
    {
        var valid = ValidateReservation(reservationId, today, today);
        if (valid.IsFailure) return valid;
        if (ticketId == Guid.Empty) return Result.Fail(FollowUpErrors.NotAvailable);
        // DEC-063: a single claim switches from the reservation to its resulting ticket.
        ReservedReservationId = null;
        ReservedTicketId = ticketId;
        Record(Status, "TransferredToTicket", actor, nowUtc);
        return Result.Ok();
    }

    public Result Release(Guid? reservationId, Guid? ticketId, DateOnly today, Guid? actor, DateTime nowUtc)
    {
        // An already released NoShow may subsequently be cancelled. Never release someone else's new claim.
        if (Status is FollowUpEligibilityStatus.Available or FollowUpEligibilityStatus.Expired) return Result.Ok();
        if (Status != FollowUpEligibilityStatus.Reserved ||
            ReservedReservationId != reservationId || ReservedTicketId != ticketId) return Result.Fail(FollowUpErrors.NotAvailable);
        var previous = Status;
        Status = today <= ValidUntil ? FollowUpEligibilityStatus.Available : FollowUpEligibilityStatus.Expired;
        Record(previous, "Released", actor, nowUtc);
        ReservedReservationId = null;
        ReservedTicketId = null;
        return Result.Ok();
    }

    public Result Consume(MedicalEncounter encounter, DateOnly today, Guid actor, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        var scope = ValidateScope(encounter.PatientId, encounter.DoctorPracticeId, encounter.DoctorId);
        if (scope.IsFailure) return scope;
        if (Status != FollowUpEligibilityStatus.Reserved || ReservedTicketId != encounter.TicketId ||
            ReservedReservationId is not null || encounter.Status != EncounterStatus.Completed)
            return Result.Fail(FollowUpErrors.NotAvailable);
        if (today > ValidUntil) return Result.Fail(FollowUpErrors.Expired);
        var previous = Status;
        Status = FollowUpEligibilityStatus.Consumed;
        ConsumedEncounterId = encounter.Id;
        Record(previous, "Consumed", actor, nowUtc);
        ReservedTicketId = null;
        return Result.Ok();
    }

    private void Record(FollowUpEligibilityStatus? previous, string action, Guid? actor, DateTime nowUtc)
    {
        ModifiedOnUtc = nowUtc;
        ModifiedByApplicationUserId = actor;
        _history.Add(FollowUpEligibilityHistory.Create(Id, previous, Status, action,
            ReservedReservationId, ReservedTicketId, ConsumedEncounterId, actor, nowUtc));
    }
}

public sealed class FollowUpEligibilityHistory : Entity<Guid>
{
    private FollowUpEligibilityHistory() { }
    public Guid FollowUpEligibilityId { get; private set; }
    public FollowUpEligibilityStatus? FromStatus { get; private set; }
    public FollowUpEligibilityStatus ToStatus { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid? ReservationId { get; private set; }
    public Guid? TicketId { get; private set; }
    public Guid? ConsumedEncounterId { get; private set; }
    public Guid? ActorApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    internal static FollowUpEligibilityHistory Create(Guid eligibilityId, FollowUpEligibilityStatus? from,
        FollowUpEligibilityStatus to, string action, Guid? reservationId, Guid? ticketId, Guid? encounterId, Guid? actor, DateTime nowUtc)
        => new() { Id = Guid.NewGuid(), FollowUpEligibilityId = eligibilityId, FromStatus = from, ToStatus = to,
            Action = action, ReservationId = reservationId, TicketId = ticketId, ConsumedEncounterId = encounterId,
            ActorApplicationUserId = actor, OccurredAtUtc = nowUtc };
}
