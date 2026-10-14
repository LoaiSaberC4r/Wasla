using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Domain.Operations;

public enum InvestigationStatus { Open = 1, InReview = 2, Resolved = 3, Closed = 4 }
public enum InvestigationPriority { Normal = 1, Urgent = 2 }
public enum InvestigationIssueType { DoctorVerification = 1, ApprovalIncident = 2, BackgroundJobFailure = 3, OutboxProblem = 4, DataQuality = 5, AdministrativeSupport = 6, SecurityMetadata = 7 }
public enum InvestigationResourceType { None = 0, Doctor = 1, SpecializationRequest = 2, BackgroundJobRun = 3, OutboxMessage = 4 }
public enum InvestigationResolutionType { Corrected = 1, Verified = 2, NoActionRequired = 3, InvalidCase = 4 }
public sealed class AdministrativeInvestigation : AggregateRoot<Guid>
{
    private readonly List<InvestigationHistory> _history = [];
    private AdministrativeInvestigation() { }
    public string TitleAr { get; private set; } = string.Empty;
    public string? TitleEn { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public InvestigationIssueType IssueType { get; private set; }
    public InvestigationPriority Priority { get; private set; }
    public InvestigationResourceType LinkedResourceType { get; private set; }
    public Guid? LinkedResourceId { get; private set; }
    public Guid? LinkedDoctorId { get; private set; }
    public Guid? LinkedSpecializationRequestId { get; private set; }
    public Guid? LinkedBackgroundJobRunId { get; private set; }
    public Guid? LinkedOutboxMessageId { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? AssignedSuperAdminId { get; private set; }
    public InvestigationStatus Status { get; private set; }
    public InvestigationResolutionType? ResolutionType { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public string? ClosureNotes { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ModifiedOnUtc { get; private set; }
    public DateTime? ResolvedOnUtc { get; private set; }
    public DateTime? ClosedOnUtc { get; private set; }
    public int HistorySequence { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<InvestigationHistory> History => _history.AsReadOnly();
    public static Result<AdministrativeInvestigation> Create(Guid id, string titleAr, string? titleEn, string description,
        InvestigationIssueType issue, InvestigationPriority priority, InvestigationResourceType resource, Guid? resourceId,
        Guid actor, DateTime now)
    {
        var ar = OperationalNotePolicy.Sanitize(titleAr, 200);
        var en = string.IsNullOrWhiteSpace(titleEn) ? null : OperationalNotePolicy.Sanitize(titleEn, 200);
        var note = OperationalNotePolicy.Sanitize(description);
        if (ar.IsFailure) return Result<AdministrativeInvestigation>.Fail(ar.Errors);
        if (en?.IsFailure == true) return Result<AdministrativeInvestigation>.Fail(en.Errors);
        if (note.IsFailure) return Result<AdministrativeInvestigation>.Fail(note.Errors);
        if (id == Guid.Empty || actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) || !Enum.IsDefined(issue) ||
            !Enum.IsDefined(priority) || !Enum.IsDefined(resource) || (resource == InvestigationResourceType.None) != (resourceId == null) ||
            resourceId == Guid.Empty) return Result<AdministrativeInvestigation>.Fail(OperatingFinanceErrors.Invalid());
        var row = new AdministrativeInvestigation { Id = id, TitleAr = ar.Value, TitleEn = en?.Value, Description = note.Value,
            IssueType = issue, Priority = priority, LinkedResourceType = resource, LinkedResourceId = resourceId,
            LinkedDoctorId = resource == InvestigationResourceType.Doctor ? resourceId : null,
            LinkedSpecializationRequestId = resource == InvestigationResourceType.SpecializationRequest ? resourceId : null,
            LinkedBackgroundJobRunId = resource == InvestigationResourceType.BackgroundJobRun ? resourceId : null,
            LinkedOutboxMessageId = resource == InvestigationResourceType.OutboxMessage ? resourceId : null,
            CreatedByApplicationUserId = actor, CreatedOnUtc = now, Status = InvestigationStatus.Open };
        row.Append("Created", actor, now, null); return Result<AdministrativeInvestigation>.Ok(row);
    }
    public Result Assign(Guid superAdminId, bool active, Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        if (Status is InvestigationStatus.Resolved or InvestigationStatus.Closed || superAdminId == Guid.Empty || !active)
            return Result.Fail(OperatingFinanceErrors.State("Investigation.ActiveAssigneeRequired"));
        AssignedSuperAdminId = superAdminId; Append("Assigned", actor, now, null); return Result.Ok();
    }
    public Result StartReview(bool activeAssignee, Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        if (Status != InvestigationStatus.Open || AssignedSuperAdminId == null || !activeAssignee)
            return Result.Fail(OperatingFinanceErrors.State("Investigation.ActiveAssigneeRequired"));
        Status = InvestigationStatus.InReview; Append("ReviewStarted", actor, now, null); return Result.Ok();
    }
    public Result Resolve(InvestigationResolutionType? outcome, string notes, bool activeAssignee, Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        if (Status != InvestigationStatus.InReview || AssignedSuperAdminId == null || !activeAssignee)
            return Result.Fail(OperatingFinanceErrors.State("Investigation.InvalidResolutionState"));
        if (outcome == null || !Enum.IsDefined(outcome.Value)) return Result.Fail(OperatingFinanceErrors.Invalid());
        var note = OperationalNotePolicy.Sanitize(notes); if (note.IsFailure) return Result.Fail(note.Errors);
        Status = InvestigationStatus.Resolved; ResolutionType = outcome; ResolutionNotes = note.Value; ResolvedOnUtc = now;
        Append("Resolved", actor, now, note.Value); return Result.Ok();
    }
    public Result Close(string notes, Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        if (Status is not (InvestigationStatus.Open or InvestigationStatus.Resolved))
            return Result.Fail(OperatingFinanceErrors.State("Investigation.InvalidClosureState"));
        var note = OperationalNotePolicy.Sanitize(notes); if (note.IsFailure) return Result.Fail(note.Errors);
        var withdrawal = Status == InvestigationStatus.Open; Status = InvestigationStatus.Closed; ClosedOnUtc = now; ClosureNotes = note.Value;
        Append(withdrawal ? "WithdrawnInvalidCase" : "Closed", actor, now, note.Value); return Result.Ok();
    }
    public Result Reopen(string reason, bool activeAssignee, Guid actor, DateTime now, Guid? assignedSuperAdminId = null)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) return Result.Fail(OperatingFinanceErrors.Invalid());
        var assignee = assignedSuperAdminId ?? AssignedSuperAdminId;
        if (Status is not (InvestigationStatus.Resolved or InvestigationStatus.Closed) || assignee == null || assignee == Guid.Empty || !activeAssignee)
            return Result.Fail(OperatingFinanceErrors.State("Investigation.ActiveAssigneeRequired"));
        var note = OperationalNotePolicy.Sanitize(reason); if (note.IsFailure) return Result.Fail(note.Errors);
        AssignedSuperAdminId = assignee; Status = InvestigationStatus.InReview; ClosedOnUtc = null; ResolvedOnUtc = null; ResolutionType = null; ResolutionNotes = null; ClosureNotes = null;
        Append("Reopened", actor, now, note.Value); return Result.Ok();
    }
    private void Append(string operation, Guid actor, DateTime now, string? note)
    {
        if (actor == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now)) throw new ArgumentException("A valid actor and UTC time are required.");
        ModifiedOnUtc = now; ++HistorySequence;
        _history.Add(new(Guid.NewGuid(), Id, operation, Status, actor, AssignedSuperAdminId, ResolutionType, note, now, HistorySequence));
    }
}
public sealed class InvestigationHistory : Entity<Guid>
{
    private InvestigationHistory() { }
    internal InvestigationHistory(Guid id, Guid caseId, string operation, InvestigationStatus status, Guid actor, Guid? assigned,
        InvestigationResolutionType? outcome, string? note, DateTime now, int sequence) : base(id)
    { CaseId = caseId; Operation = operation; Status = status; ActorApplicationUserId = actor; AssignedSuperAdminId = assigned;
        ResolutionType = outcome; Note = note; RecordedAtUtc = now; SequenceNumber = sequence; }
    public Guid CaseId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public InvestigationStatus Status { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public Guid? AssignedSuperAdminId { get; private set; }
    public InvestigationResolutionType? ResolutionType { get; private set; }
    public string? Note { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public int SequenceNumber { get; private set; }
}
