using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Labs;

public sealed class PatientLabResultSubmission : AggregateRoot<Guid>
{
    private readonly List<PatientLabResultSubmissionAttachment> _attachments = [];
    private readonly List<PatientLabResultSubmissionHistory> _history = [];
    private PatientLabResultSubmission() { }
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
    public IReadOnlyCollection<PatientLabResultSubmissionAttachment> Attachments => _attachments.AsReadOnly();
    public IReadOnlyCollection<PatientLabResultSubmissionHistory> History => _history.AsReadOnly();
    public static Result<PatientLabResultSubmission> Submit(LabRequest r, IReadOnlyList<DiagnosticAttachmentData> files,
        string? provider, DateOnly? date, string? note, Guid patient, Guid actor, DateTime now)
    {
        if (r.PatientId != patient) return Result<PatientLabResultSubmission>.Fail(DiagnosticErrors.NotFound("LabResultSubmission.NotFound"));
        if (r.Status == DiagnosticRequestStatus.Draft || !r.Items.Any(i => i.Status == DiagnosticItemStatus.Requested))
            return Result<PatientLabResultSubmission>.Fail(DiagnosticErrors.Conflict("LabResultSubmission.NoRequestedItems"));
        if (!DiagnosticText.ValidAttachments(files) || provider?.Length > 500 || note?.Length > 2000)
            return Result<PatientLabResultSubmission>.Fail(DiagnosticErrors.Validation("LabResultSubmission.InvalidData"));
        var s = new PatientLabResultSubmission
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
        s._attachments.AddRange(files.Select(f => PatientLabResultSubmissionAttachment.Create(s.Id, f)));
        s.Record("Submitted", null, null, actor, "Patient", now); return Result<PatientLabResultSubmission>.Ok(s);
    }
    public Result Accept(Guid resultId, Guid actor, DateTime now) => Transition(PatientSubmissionStatus.Accepted, null, resultId, actor, "Doctor", now);
    public Result Reject(string reason, Guid actor, DateTime now)
    {
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("LabResultSubmission.RejectionReasonRequired"));
        return Transition(PatientSubmissionStatus.Rejected, reason.Trim(), null, actor, "Doctor", now);
    }
    public Result Withdraw(Guid actor, DateTime now) => Transition(PatientSubmissionStatus.Withdrawn, null, null, actor, "Patient", now);
    public Result NoLongerApplicable(Guid actor, DateTime now) => Transition(PatientSubmissionStatus.NoLongerApplicable, null, null, actor, "Doctor", now);
    private Result Transition(PatientSubmissionStatus status, string? reason, Guid? resultId, Guid actor, string actorType, DateTime now)
    {
        if (Status != PatientSubmissionStatus.PendingReview) return Result.Fail(DiagnosticErrors.Conflict("LabResultSubmission.NotPending"));
        var before = DiagnosticText.Json(new { Status, AcceptedResultId, PatientVisibleReason });
        Status = status; AcceptedResultId = resultId; PatientVisibleReason = reason; Record(status.ToString(), reason, before, actor, actorType, now); return Result.Ok();
    }
    private void Record(string action, string? reason, string? before, Guid actor, string actorType, DateTime now)
    { Revision++; _history.Add(PatientLabResultSubmissionHistory.Create(Id, action, reason, before, DiagnosticText.Json(new { Status, AcceptedResultId, PatientVisibleReason }), actor, actorType, now)); }
}

public sealed class PatientLabResultSubmissionAttachment : Entity<Guid>
{
    private PatientLabResultSubmissionAttachment() { }
    public Guid SubmissionId { get; private set; }
    public string PrivateMediaKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime UploadedAtUtc { get; private set; }
    public DiagnosticAttachmentKind Kind { get; private set; }
    public DiagnosticAttachmentData Data() => new(PrivateMediaKey, OriginalFileName, ContentType, SizeBytes, Sha256, UploadedAtUtc, Kind);
    internal static PatientLabResultSubmissionAttachment Create(Guid parent, DiagnosticAttachmentData d) => new()
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

public sealed class PatientLabResultSubmissionHistory : Entity<Guid>
{
    private PatientLabResultSubmissionHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static PatientLabResultSubmissionHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
