using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Tickets;

namespace Wasla.Domain.Clinical;

public enum HiddenAccessRequestStatus { Pending = 1, Approved = 2, Rejected = 3, Revoked = 4, Expired = 5 }
public sealed class HiddenEncounterAccessRequest : AggregateRoot<Guid>
{
    private readonly List<HiddenEncounterAccessRequestHistory> _history = [];
    private HiddenEncounterAccessRequest() { }
    public Guid HiddenEncounterId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid OwningDoctorId { get; private set; }
    public Guid RequestingDoctorId { get; private set; }
    public Guid RequestingPracticeId { get; private set; }
    public Guid RequestingTicketId { get; private set; }
    public Guid RequestingEncounterId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public HiddenAccessRequestStatus Status { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? DecidedByApplicationUserId { get; private set; }
    public Guid? RevokedByApplicationUserId { get; private set; }
    public int VisibilityRevisionAtRequest { get; private set; }
    public int? VisibilityRevisionAtApproval { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<HiddenEncounterAccessRequestHistory> History => _history.AsReadOnly();

    public static bool IsLiveTreatment(Ticket t, MedicalEncounter e, Guid doctor, Guid practice)
        => t.Status == TicketStatus.InProgress && e.Status == EncounterStatus.InProgress && t.Id == e.TicketId &&
            t.PatientId == e.PatientId && t.DoctorId == doctor && e.DoctorId == doctor &&
            t.DoctorPracticeId == practice && e.DoctorPracticeId == practice;
    public static Result<HiddenEncounterAccessRequest> Create(MedicalEncounter source, Ticket ticket, MedicalEncounter treating,
        Guid doctor, Guid practice, string reason, Guid actor, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(treating);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            return Result<HiddenEncounterAccessRequest>.Fail(MedicalRecordErrors.InvalidInput);
        if (!IsLiveTreatment(ticket, treating, doctor, practice) || source.Status != EncounterStatus.Completed ||
            source.Visibility != EncounterVisibility.Hidden || source.DoctorId == doctor || source.PatientId != ticket.PatientId)
            return Result<HiddenEncounterAccessRequest>.Fail(MedicalRecordErrors.Denied);
        var r = new HiddenEncounterAccessRequest { Id = Guid.NewGuid(), HiddenEncounterId = source.Id, PatientId = source.PatientId,
            OwningDoctorId = source.DoctorId, RequestingDoctorId = doctor, RequestingPracticeId = practice,
            RequestingTicketId = ticket.Id, RequestingEncounterId = treating.Id, Reason = reason.Trim(),
            Status = HiddenAccessRequestStatus.Pending, RequestedAtUtc = now, VisibilityRevisionAtRequest = source.VisibilityRevision };
        r.Record("Requested", actor, now); return Result<HiddenEncounterAccessRequest>.Ok(r);
    }
    public bool ContextLive(MedicalEncounter source, Ticket ticket, MedicalEncounter treating)
        => source.Id == HiddenEncounterId && source.DoctorId == OwningDoctorId && source.PatientId == PatientId &&
            source.Status == EncounterStatus.Completed && source.Visibility == EncounterVisibility.Hidden &&
            source.VisibilityRevision == (VisibilityRevisionAtApproval ?? VisibilityRevisionAtRequest) &&
            ticket.Id == RequestingTicketId && ticket.PatientId == PatientId && treating.Id == RequestingEncounterId &&
            IsLiveTreatment(ticket, treating, RequestingDoctorId, RequestingPracticeId);
    public HiddenAccessRequestStatus EffectiveStatus(bool live) => !live && Status is HiddenAccessRequestStatus.Pending or HiddenAccessRequestStatus.Approved
        ? HiddenAccessRequestStatus.Expired : Status;
    public Result Decide(bool approve, MedicalEncounter source, Ticket ticket, MedicalEncounter treating, Guid doctor, Guid actor, DateTime now)
    {
        if (doctor != OwningDoctorId) return Result.Fail(MedicalRecordErrors.Denied);
        if (!ContextLive(source, ticket, treating)) return Result.Fail(MedicalRecordErrors.Expired);
        if (Status != HiddenAccessRequestStatus.Pending) return Result.Fail(MedicalRecordErrors.InvalidState);
        Status = approve ? HiddenAccessRequestStatus.Approved : HiddenAccessRequestStatus.Rejected;
        if (approve) VisibilityRevisionAtApproval = source.VisibilityRevision;
        DecidedAtUtc = now; DecidedByApplicationUserId = actor; Record(approve ? "Approved" : "Rejected", actor, now); return Result.Ok();
    }
    public Result Revoke(Guid doctor, Guid actor, DateTime now)
    {
        if (doctor != OwningDoctorId) return Result.Fail(MedicalRecordErrors.Denied);
        if (Status != HiddenAccessRequestStatus.Approved) return Result.Fail(MedicalRecordErrors.InvalidState);
        Status = HiddenAccessRequestStatus.Revoked; RevokedAtUtc = now; RevokedByApplicationUserId = actor; Record("Revoked", actor, now); return Result.Ok();
    }
    public void Expire(Guid actor, DateTime now)
    {
        if (Status is not (HiddenAccessRequestStatus.Pending or HiddenAccessRequestStatus.Approved)) return;
        Status = HiddenAccessRequestStatus.Expired; Record("Expired", actor, now);
    }
    private void Record(string action, Guid actor, DateTime now) => _history.Add(HiddenEncounterAccessRequestHistory.Create(Id, action, actor, now));
}
public sealed class HiddenEncounterAccessRequestHistory : Entity<Guid>
{
    private HiddenEncounterAccessRequestHistory() { }
    public Guid RequestId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    internal static HiddenEncounterAccessRequestHistory Create(Guid request, string action, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), RequestId = request, Action = action, ActorApplicationUserId = actor, OccurredAtUtc = now };
}
