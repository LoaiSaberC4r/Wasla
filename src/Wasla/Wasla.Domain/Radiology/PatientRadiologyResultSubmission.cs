using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Radiology;

public sealed class PatientRadiologyResultSubmission : AggregateRoot<Guid>
{
    private readonly List<PatientRadiologyResultSubmissionAttachment> _attachments = [];
    private readonly List<PatientRadiologyResultSubmissionHistory> _history = [];
    private PatientRadiologyResultSubmission() { }
    public Guid RequestId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string? ExternalProviderName { get; private set; }
    public DateOnly? ExternalReportDate { get; private set; }
    public string? PatientNote { get; private set; }
    public PatientSubmissionStatus Status { get; private set; }
    public string? PatientVisibleReason { get; private set; }
    public Guid? AcceptedResultId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PatientRadiologyResultSubmissionAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyCollection<PatientRadiologyResultSubmissionHistory> History => _history.AsReadOnly();
    public static Result<PatientRadiologyResultSubmission> Submit(RadiologyRequest r, IReadOnlyList<DiagnosticAttachmentData> files,
        string? provider, DateOnly? date, string? note, Guid patient, Guid actor, DateTime now)
    {
        if (r.PatientId != patient) return Result<PatientRadiologyResultSubmission>.Fail(DiagnosticErrors.NotFound("RadiologyResultSubmission.NotFound"));
        if (r.Status == DiagnosticRequestStatus.Draft || !r.Items.Any(i => i.Status == DiagnosticItemStatus.Requested))
            return Result<PatientRadiologyResultSubmission>.Fail(DiagnosticErrors.Conflict("RadiologyResultSubmission.NoRequestedItems"));
        if (!DiagnosticText.ValidAttachments(files) || provider?.Length > 500 || note?.Length > 2000)
            return Result<PatientRadiologyResultSubmission>.Fail(DiagnosticErrors.Validation("RadiologyResultSubmission.InvalidData"));
        var s = new PatientRadiologyResultSubmission
        {
            Id = Guid.NewGuid(),
            RequestId = r.Id,
            PatientId = patient,
            DoctorId = r.DoctorId,
            DoctorPracticeId = r.DoctorPracticeId,
            UploadedByUserId = actor,
            SubmittedAtUtc = now,
            Status = PatientSubmissionStatus.PendingReview,
            ExternalProviderName = DiagnosticText.Clean(provider),
            ExternalReportDate = date,
            PatientNote = DiagnosticText.Clean(note)
        };
        s._attachments.AddRange(files.Select(f => PatientRadiologyResultSubmissionAttachment.Create(s.Id, f)));
        s.Record("Submitted", null, null, actor, "Patient", now); return Result<PatientRadiologyResultSubmission>.Ok(s);
    }
    public Result Accept(Guid resultId, Guid actor, DateTime now) => Transition(PatientSubmissionStatus.Accepted, null, resultId, actor, "Doctor", now);
    public Result Reject(string reason, Guid actor, DateTime now)
    {
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("RadiologyResultSubmission.RejectionReasonRequired"));
        return Transition(PatientSubmissionStatus.Rejected, reason.Trim(), null, actor, "Doctor", now);
    }
    public Result Withdraw(Guid actor, DateTime now) => Transition(PatientSubmissionStatus.Withdrawn, null, null, actor, "Patient", now);
    public Result NoLongerApplicable(Guid actor, DateTime now) => Transition(PatientSubmissionStatus.NoLongerApplicable, null, null, actor, "Doctor", now);
    private Result Transition(PatientSubmissionStatus status, string? reason, Guid? resultId, Guid actor, string actorType, DateTime now)
    {
        if (Status != PatientSubmissionStatus.PendingReview) return Result.Fail(DiagnosticErrors.Conflict("RadiologyResultSubmission.NotPending"));
        var before = DiagnosticText.Json(new { Status, AcceptedResultId, PatientVisibleReason });
        Status = status; AcceptedResultId = resultId; PatientVisibleReason = reason; Record(status.ToString(), reason, before, actor, actorType, now); return Result.Ok();
    }
    private void Record(string action, string? reason, string? before, Guid actor, string actorType, DateTime now)
    { Revision++; _history.Add(PatientRadiologyResultSubmissionHistory.Create(Id, action, reason, before, DiagnosticText.Json(new { Status, AcceptedResultId, PatientVisibleReason }), actor, actorType, now)); }
}

public sealed class PatientRadiologyResultSubmissionAttachment : Entity<Guid>
{
    private PatientRadiologyResultSubmissionAttachment() { }
    public Guid SubmissionId { get; private set; }
    public string PrivateMediaKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime UploadedAtUtc { get; private set; }
    public DiagnosticAttachmentKind Kind { get; private set; }
    public DiagnosticAttachmentData Data() => new(PrivateMediaKey, OriginalFileName, ContentType, SizeBytes, Sha256, UploadedAtUtc, Kind);
    internal static PatientRadiologyResultSubmissionAttachment Create(Guid parent, DiagnosticAttachmentData d) => new()
    {
        Id = Guid.NewGuid(),
        SubmissionId = parent,
        PrivateMediaKey = d.PrivateMediaKey,
        OriginalFileName = d.OriginalFileName,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        Sha256 = d.Sha256,
        UploadedAtUtc = d.UploadedAtUtc,
        Kind = d.Kind
    };
}

public sealed class PatientRadiologyResultSubmissionHistory : Entity<Guid>
{
    private PatientRadiologyResultSubmissionHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static PatientRadiologyResultSubmissionHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
