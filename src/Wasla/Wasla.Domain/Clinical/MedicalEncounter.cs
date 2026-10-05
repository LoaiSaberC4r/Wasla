using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Tickets;

namespace Wasla.Domain.Clinical;

public enum EncounterStatus { InProgress = 1, Completed = 2 }
public enum DiagnosisType { Primary = 1, Secondary = 2 }
public enum AmendmentTarget { ClinicalNotes = 1, Diagnosis = 2 }
public enum AmendmentChangeType { Added = 1, Updated = 2, Removed = 3 }

public sealed record DiagnosisSnapshot(Guid Id, DiagnosisType Type, string DisplayText, string? Notes, bool IsVoided);
public sealed record EncounterCorrection(
    AmendmentTarget Target, AmendmentChangeType ChangeType, Guid? DiagnosisId = null,
    DiagnosisType? Type = null, string? DisplayText = null, string? Notes = null, string? ClinicalNotes = null);

public sealed class MedicalEncounter : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<Diagnosis> _diagnoses = [];
    private readonly List<EncounterAmendment> _amendments = [];
    private readonly List<EncounterAuditEvent> _auditEvents = [];
    private MedicalEncounter() { }
    public Guid TicketId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PatientId { get; private set; }
    public EncounterStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ClinicalNotes { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<Diagnosis> Diagnoses => _diagnoses.AsReadOnly();
    public IReadOnlyCollection<EncounterAmendment> Amendments => _amendments.AsReadOnly();
    public IReadOnlyCollection<EncounterAuditEvent> AuditEvents => _auditEvents.AsReadOnly();

    // Only the authoritative Start Visit workflow supplies the just-started Ticket.
    public static Result<MedicalEncounter> Start(Ticket ticket, Guid actor, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (ticket.Status != TicketStatus.InProgress || ticket.InProgressOnUtc is null ||
            actor == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
            return Result<MedicalEncounter>.Fail(ClinicalErrors.InvalidState);
        var encounter = new MedicalEncounter
        {
            Id = Guid.NewGuid(), TicketId = ticket.Id, DoctorId = ticket.DoctorId,
            DoctorPracticeId = ticket.DoctorPracticeId, PatientId = ticket.PatientId,
            Status = EncounterStatus.InProgress, StartedAtUtc = ticket.InProgressOnUtc.Value,
            CreatedOnUtc = nowUtc, CreatedByApplicationUserId = actor
        };
        encounter.Audit("Started", null, actor, nowUtc);
        return Result<MedicalEncounter>.Ok(encounter);
    }

    public Result UpdateClinicalNotes(string? notes, Guid actor, DateTime nowUtc)
    {
        if (Status != EncounterStatus.InProgress) return Result.Fail(ClinicalErrors.InvalidState);
        if (notes?.Length > 8000) return Result.Fail(ClinicalErrors.NotesTooLong);
        ClinicalNotes = Normalize(notes);
        Touch("ClinicalNotesUpdated", null, actor, nowUtc);
        return Result.Ok();
    }

    public Result AddDiagnosis(Guid id, DiagnosisType type, string displayText, string? notes, Guid actor, DateTime nowUtc)
    {
        if (Status != EncounterStatus.InProgress) return Result.Fail(ClinicalErrors.InvalidState);
        if (id == Guid.Empty || _diagnoses.Any(d => d.Id == id)) return Result.Fail(ClinicalErrors.DiagnosisInvalid);
        var state = new DiagnosisSnapshot(id, type, displayText?.Trim() ?? string.Empty, Normalize(notes), false);
        var valid = ValidateDiagnoses(_diagnoses.Where(d => !d.IsVoided).Select(d => d.Snapshot()).Append(state));
        if (valid.IsFailure) return valid;
        _diagnoses.Add(Diagnosis.Create(Id, state, actor, nowUtc));
        Touch("DiagnosisAdded", id, actor, nowUtc);
        return Result.Ok();
    }

    public Result UpdateDiagnosis(Guid id, DiagnosisType type, string displayText, string? notes, Guid actor, DateTime nowUtc)
    {
        if (Status != EncounterStatus.InProgress) return Result.Fail(ClinicalErrors.InvalidState);
        var diagnosis = _diagnoses.SingleOrDefault(d => d.Id == id && !d.IsVoided);
        if (diagnosis is null) return Result.Fail(ClinicalErrors.DiagnosisNotFound);
        var state = new DiagnosisSnapshot(id, type, displayText?.Trim() ?? string.Empty, Normalize(notes), false);
        var valid = ValidateDiagnoses(_diagnoses.Where(d => !d.IsVoided && d.Id != id).Select(d => d.Snapshot()).Append(state));
        if (valid.IsFailure) return valid;
        diagnosis.Correct(state, actor, nowUtc);
        Touch("DiagnosisUpdated", id, actor, nowUtc);
        return Result.Ok();
    }

    public Result RemoveDiagnosis(Guid id, Guid actor, DateTime nowUtc)
    {
        if (Status != EncounterStatus.InProgress) return Result.Fail(ClinicalErrors.InvalidState);
        var diagnosis = _diagnoses.SingleOrDefault(d => d.Id == id && !d.IsVoided);
        if (diagnosis is null) return Result.Fail(ClinicalErrors.DiagnosisNotFound);
        var valid = ValidateDiagnoses(_diagnoses.Where(d => !d.IsVoided && d.Id != id).Select(d => d.Snapshot()));
        if (valid.IsFailure) return valid;
        _diagnoses.Remove(diagnosis);
        Touch("DiagnosisRemoved", id, actor, nowUtc);
        return Result.Ok();
    }

    public Result Complete(Ticket ticket, Guid actor, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (Status != EncounterStatus.InProgress || ticket.Status != TicketStatus.InProgress ||
            ticket.Id != TicketId || ticket.PatientId != PatientId || ticket.DoctorId != DoctorId ||
            ticket.DoctorPracticeId != DoctorPracticeId) return Result.Fail(ClinicalErrors.InvalidState);
        if (string.IsNullOrWhiteSpace(ClinicalNotes)) return Result.Fail(ClinicalErrors.NotesRequired);
        var valid = ValidateDiagnoses(_diagnoses.Where(d => !d.IsVoided).Select(d => d.Snapshot()));
        if (valid.IsFailure) return valid;
        Status = EncounterStatus.Completed;
        CompletedAtUtc = nowUtc;
        Touch("Completed", null, actor, nowUtc);
        return Result.Ok();
    }

    public Result Amend(string reason, IReadOnlyList<EncounterCorrection> corrections, Guid doctorId,
        Guid actor, DateTime nowUtc, Func<object, string> serializeSnapshot)
    {
        ArgumentNullException.ThrowIfNull(corrections);
        ArgumentNullException.ThrowIfNull(serializeSnapshot);
        if (Status != EncounterStatus.Completed || doctorId != DoctorId) return Result.Fail(ClinicalErrors.AmendmentInvalidState);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return Result.Fail(ClinicalErrors.AmendmentReasonRequired);
        if (corrections.Count is < 1 or > 100) return Result.Fail(ClinicalErrors.AmendmentInvalidChange);
        // Stage the entire corrected state before touching the persisted aggregate.
        var states = _diagnoses.ToDictionary(d => d.Id, d => d.Snapshot());
        var notes = ClinicalNotes;
        var changes = new List<EncounterAmendmentChange>();
        var amendmentId = Guid.NewGuid();
        foreach (var correction in corrections)
        {
            if (correction.Target == AmendmentTarget.ClinicalNotes)
            {
                if (correction.ChangeType != AmendmentChangeType.Updated || correction.DiagnosisId is not null ||
                    string.IsNullOrWhiteSpace(correction.ClinicalNotes) || correction.ClinicalNotes.Length > 8000 ||
                    notes == correction.ClinicalNotes.Trim()) return Result.Fail(ClinicalErrors.AmendmentInvalidChange);
                changes.Add(EncounterAmendmentChange.Create(amendmentId, correction.Target, null, correction.ChangeType,
                    serializeSnapshot(new { ClinicalNotes = notes }), serializeSnapshot(new { ClinicalNotes = correction.ClinicalNotes.Trim() })));
                notes = correction.ClinicalNotes.Trim();
                continue;
            }
            if (correction.Target != AmendmentTarget.Diagnosis || !Enum.IsDefined(correction.ChangeType))
                return Result.Fail(ClinicalErrors.AmendmentInvalidChange);
            DiagnosisSnapshot? before = null;
            var id = correction.DiagnosisId ?? Guid.NewGuid();
            if (correction.ChangeType != AmendmentChangeType.Added &&
                (!states.TryGetValue(id, out before) || before.IsVoided)) return Result.Fail(ClinicalErrors.DiagnosisNotFound);
            if (correction.ChangeType == AmendmentChangeType.Added &&
                (correction.DiagnosisId is not null || id == Guid.Empty)) return Result.Fail(ClinicalErrors.AmendmentInvalidChange);
            var after = correction.ChangeType == AmendmentChangeType.Removed
                ? before! with { IsVoided = true }
                : new DiagnosisSnapshot(id, correction.Type.GetValueOrDefault(), correction.DisplayText?.Trim() ?? string.Empty,
                    Normalize(correction.Notes), false);
            if (before == after) return Result.Fail(ClinicalErrors.AmendmentInvalidChange);
            states[id] = after;
            changes.Add(EncounterAmendmentChange.Create(amendmentId, correction.Target, id, correction.ChangeType,
                before is null ? null : serializeSnapshot(before), serializeSnapshot(after)));
        }
        var valid = ValidateDiagnoses(states.Values.Where(d => !d.IsVoided));
        if (valid.IsFailure) return valid;
        foreach (var state in states.Values)
        {
            var existing = _diagnoses.SingleOrDefault(d => d.Id == state.Id);
            if (existing is null) _diagnoses.Add(Diagnosis.Create(Id, state, actor, nowUtc));
            else if (existing.Snapshot() != state) existing.Correct(state, actor, nowUtc);
        }
        ClinicalNotes = notes;
        _amendments.Add(EncounterAmendment.Create(amendmentId, Id, _amendments.Count + 1,
            reason.Trim(), doctorId, actor, nowUtc, changes));
        Touch("Amended", amendmentId, actor, nowUtc);
        return Result.Ok();
    }

    public static Result ValidateDiagnoses(IEnumerable<DiagnosisSnapshot> diagnoses)
    {
        var active = diagnoses.ToArray();
        if (active.Any(d => !Enum.IsDefined(d.Type) || string.IsNullOrWhiteSpace(d.DisplayText) ||
            d.DisplayText.Length > 500 || d.Notes?.Length > 2000)) return Result.Fail(ClinicalErrors.DiagnosisInvalid);
        if (active.GroupBy(d => d.DisplayText.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return Result.Fail(ClinicalErrors.DiagnosisDuplicate);
        var primary = active.Count(d => d.Type == DiagnosisType.Primary);
        if (primary > 1) return Result.Fail(ClinicalErrors.MultiplePrimary);
        if (active.Length > 0 && primary == 0) return Result.Fail(ClinicalErrors.PrimaryRequired);
        return Result.Ok();
    }

    private void Touch(string action, Guid? targetId, Guid actor, DateTime nowUtc)
    {
        Revision++;
        ModifiedOnUtc = nowUtc;
        ModifiedByApplicationUserId = actor;
        Audit(action, targetId, actor, nowUtc);
    }
    private void Audit(string action, Guid? targetId, Guid actor, DateTime nowUtc)
        => _auditEvents.Add(EncounterAuditEvent.Create(Id, action, targetId, actor, nowUtc));
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class Diagnosis : Entity<Guid>, IAuditableEntity
{
    private Diagnosis() { }
    public Guid MedicalEncounterId { get; private set; }
    public DiagnosisType Type { get; private set; }
    public string DisplayText { get; private set; } = string.Empty;
    public string NormalizedDisplayText { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public bool IsVoided { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public Guid? VoidedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    internal static Diagnosis Create(Guid encounterId, DiagnosisSnapshot state, Guid actor, DateTime nowUtc)
    {
        var diagnosis = new Diagnosis { Id = state.Id, MedicalEncounterId = encounterId,
            CreatedOnUtc = nowUtc, CreatedByApplicationUserId = actor };
        diagnosis.Set(state);
        return diagnosis;
    }
    internal DiagnosisSnapshot Snapshot() => new(Id, Type, DisplayText, Notes, IsVoided);
    internal void Correct(DiagnosisSnapshot state, Guid actor, DateTime nowUtc)
    {
        Set(state);
        ModifiedOnUtc = nowUtc;
        ModifiedByApplicationUserId = actor;
        if (state.IsVoided) { VoidedAtUtc = nowUtc; VoidedByApplicationUserId = actor; }
    }
    private void Set(DiagnosisSnapshot state)
    {
        Type = state.Type; DisplayText = state.DisplayText; Notes = state.Notes; IsVoided = state.IsVoided;
        NormalizedDisplayText = state.DisplayText.Trim().ToUpperInvariant();
    }
}

public sealed class EncounterAmendment : Entity<Guid>
{
    private readonly List<EncounterAmendmentChange> _changes = [];
    private EncounterAmendment() { }
    public Guid MedicalEncounterId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedByDoctorId { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<EncounterAmendmentChange> Changes => _changes.AsReadOnly();
    internal static EncounterAmendment Create(Guid id, Guid encounterId, int sequence, string reason,
        Guid doctorId, Guid actor, DateTime nowUtc, IEnumerable<EncounterAmendmentChange> changes)
    {
        var amendment = new EncounterAmendment { Id = id, MedicalEncounterId = encounterId, SequenceNumber = sequence,
            Reason = reason, CreatedByDoctorId = doctorId, CreatedByApplicationUserId = actor, CreatedAtUtc = nowUtc };
        amendment._changes.AddRange(changes);
        return amendment;
    }
}

public sealed class EncounterAmendmentChange : Entity<Guid>
{
    private EncounterAmendmentChange() { }
    public Guid EncounterAmendmentId { get; private set; }
    public AmendmentTarget TargetType { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public AmendmentChangeType ChangeType { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string? AfterSnapshot { get; private set; }
    internal static EncounterAmendmentChange Create(Guid amendmentId, AmendmentTarget target, Guid? targetId,
        AmendmentChangeType changeType, string? before, string? after) => new()
        { Id = Guid.NewGuid(), EncounterAmendmentId = amendmentId, TargetType = target, TargetEntityId = targetId,
            ChangeType = changeType, BeforeSnapshot = before, AfterSnapshot = after };
}

public sealed class EncounterAuditEvent : Entity<Guid>
{
    private EncounterAuditEvent() { }
    public Guid MedicalEncounterId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid? TargetEntityId { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public static EncounterAuditEvent RecordRead(Guid encounterId, Guid actor, DateTime nowUtc)
        => Create(encounterId, "ClinicalRead", null, actor, nowUtc);
    internal static EncounterAuditEvent Create(Guid encounterId, string action, Guid? targetId, Guid actor, DateTime nowUtc)
        => new() { Id = Guid.NewGuid(), MedicalEncounterId = encounterId, Action = action,
            TargetEntityId = targetId, ActorApplicationUserId = actor, OccurredAtUtc = nowUtc };
}
