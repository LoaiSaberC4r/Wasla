using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Labs;

public sealed class LabResult : AggregateRoot<Guid>
{
    private readonly List<LabResultVersion> _versions = [];
    private readonly List<LabResultHistory> _history = [];
    private LabResult() { }
    public Guid RequestId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid? AcceptedSubmissionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LabResultVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyCollection<LabResultHistory> History => _history.AsReadOnly();
    public LabResultVersion? Current => _versions.SingleOrDefault(v => v.VersionNumber == CurrentVersionNumber && v.Status == DiagnosticVersionStatus.Finalized);
    public static Result<LabResult> Record(LabRequest request, IReadOnlyList<Guid> coverage, IReadOnlyList<DiagnosticAttachmentData> attachments,
        string? provider, DateOnly? date, DiagnosticUploader uploader, Guid originalUser, DateTime originalTime, Guid doctor, Guid actor, DateTime now, Guid? submissionId = null)
    {
        var valid = Validate(request, coverage, attachments, [], provider);
        if (valid.IsFailure) return Result<LabResult>.Fail(valid.Errors);
        if (request.DoctorId != doctor) return Result<LabResult>.Fail(DiagnosticErrors.Denied());
        var r = new LabResult
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            DoctorId = doctor,
            PatientId = request.PatientId,
            DoctorPracticeId = request.DoctorPracticeId,
            AcceptedSubmissionId = submissionId,
            CreatedAtUtc = now,
            CurrentVersionNumber = 1
        };
        r._versions.Add(LabResultVersion.Create(r.Id, 1, coverage, attachments, provider, date, uploader, originalUser, originalTime, doctor, now, null));
        r.RecordHistory("Recorded", null, null, actor, now); return Result<LabResult>.Ok(r);
    }
    public Result Correct(LabRequest request, IReadOnlyList<Guid> coverage, IReadOnlyList<DiagnosticAttachmentData> attachments,
        string? provider, DateOnly? date, string reason, Guid actor, DateTime now)
    {
        var current = Current;
        if (current is null || request.Id != RequestId) return Result.Fail(DiagnosticErrors.Conflict("LabResult.InvalidCorrectionState"));
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("LabResult.CorrectionReasonRequired"));
        var valid = Validate(request, coverage, attachments, current.Coverage.Select(c => c.RequestItemId).ToArray(), provider);
        if (valid.IsFailure) return valid;
        var before = Snapshot(); current.Supersede(); CurrentVersionNumber++;
        _versions.Add(LabResultVersion.Create(Id, CurrentVersionNumber, coverage, attachments, provider, date,
            DiagnosticUploader.Doctor, actor, now, DoctorId, now, reason));
        RecordHistory("Corrected", reason, before, actor, now); return Result.Ok();
    }
    public Result Void(string reason, Guid actor, DateTime now)
    {
        if (Current is not { } current) return Result.Fail(DiagnosticErrors.Conflict("LabResult.InvalidVoidState"));
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("LabResult.VoidReasonRequired"));
        var before = Snapshot(); current.Void(reason, now); RecordHistory("Voided", reason, before, actor, now); return Result.Ok();
    }
    private static Result Validate(LabRequest r, IReadOnlyList<Guid> ids, IReadOnlyList<DiagnosticAttachmentData> files, IReadOnlyList<Guid> previous, string? provider)
    {
        if (r.Status == DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("LabResult.InvalidCoverage"));
        if (ids.Count == 0) return Result.Fail(DiagnosticErrors.Validation("LabResult.CoverageRequired"));
        if (ids.Count != ids.Distinct().Count() || ids.Any(id => !r.Items.Any(i => i.Id == id &&
            (i.Status == DiagnosticItemStatus.Requested || i.Status == DiagnosticItemStatus.Completed && previous.Contains(id)))))
            return Result.Fail(DiagnosticErrors.Conflict("LabResult.InvalidCoverage"));
        if (!DiagnosticText.ValidAttachments(files)) return Result.Fail(DiagnosticErrors.Validation("LabResult.AttachmentRequired"));
        if (provider?.Length > 500) return Result.Fail(DiagnosticErrors.Validation("LabResult.InvalidData"));
        return Result.Ok();
    }
    private string Snapshot() => DiagnosticText.Json(new { CurrentVersionNumber, Versions = _versions.Select(v => new { v.Id, v.VersionNumber, v.Status, Coverage = v.Coverage.Select(c => c.RequestItemId), Attachments = v.Attachments.Select(a => new { a.Id, a.Sha256 }) }) });
    private void RecordHistory(string action, string? reason, string? before, Guid actor, DateTime now)
    { Revision++; _history.Add(LabResultHistory.Create(Id, action, reason, before, Snapshot(), actor, "Doctor", now)); }
}
public sealed class LabResultVersion : Entity<Guid>
{
    private readonly List<LabResultCoverage> _coverage = [];
    private readonly List<LabResultAttachment> _attachments = [];
    private LabResultVersion() { }
    public Guid ResultId { get; private set; }
    public int VersionNumber { get; private set; }
    public DiagnosticVersionStatus Status { get; private set; }
    public string? ExternalProviderName { get; private set; }
    public DateOnly? ExternalReportDate { get; private set; }
    public DiagnosticUploader OriginallyUploadedBy { get; private set; }
    public Guid OriginallyUploadedByUserId { get; private set; }
    public DateTime OriginallyUploadedAtUtc { get; private set; }
    public Guid ReviewedByDoctorId { get; private set; }
    public DateTime ReviewedAtUtc { get; private set; }
    public string? CorrectionReason { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public IReadOnlyCollection<LabResultCoverage> Coverage => _coverage.AsReadOnly();
    public IReadOnlyCollection<LabResultAttachment> Attachments => _attachments.AsReadOnly();
    internal static LabResultVersion Create(Guid result, int number, IReadOnlyList<Guid> ids, IReadOnlyList<DiagnosticAttachmentData> files,
        string? provider, DateOnly? date, DiagnosticUploader uploader, Guid originalUser, DateTime originalTime, Guid doctor, DateTime now, string? reason)
    {
        var v = new LabResultVersion
        {
            Id = Guid.NewGuid(),
            ResultId = result,
            VersionNumber = number,
            Status = DiagnosticVersionStatus.Finalized,
            ExternalProviderName = DiagnosticText.Clean(provider),
            ExternalReportDate = date,
            OriginallyUploadedBy = uploader,
            OriginallyUploadedByUserId = originalUser,
            OriginallyUploadedAtUtc = originalTime,
            ReviewedByDoctorId = doctor,
            ReviewedAtUtc = now,
            CorrectionReason = reason
        };
        v._coverage.AddRange(ids.Select(id => LabResultCoverage.Create(v.Id, id)));
        v._attachments.AddRange(files.Select(f => LabResultAttachment.Create(v.Id, f))); return v;
    }
    internal void Supersede() => Status = DiagnosticVersionStatus.Superseded;
    internal void Void(string reason, DateTime now) { Status = DiagnosticVersionStatus.Voided; VoidReason = reason.Trim(); VoidedAtUtc = now; }
}
public sealed class LabResultCoverage : Entity<Guid>
{
    private LabResultCoverage() { }
    public Guid ResultVersionId { get; private set; }
    public Guid RequestItemId { get; private set; }
    internal static LabResultCoverage Create(Guid version, Guid item) => new() { Id = Guid.NewGuid(), ResultVersionId = version, RequestItemId = item };
}

public sealed class LabResultAttachment : Entity<Guid>
{
    private LabResultAttachment() { }
    public Guid ResultVersionId { get; private set; }
    public string PrivateMediaKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime UploadedAtUtc { get; private set; }
    public DiagnosticAttachmentKind Kind { get; private set; }
    public DiagnosticAttachmentData Data() => new(PrivateMediaKey, OriginalFileName, ContentType, SizeBytes, Sha256, UploadedAtUtc, Kind);
    internal static LabResultAttachment Create(Guid parent, DiagnosticAttachmentData d) => new()
    {
        Id = Guid.NewGuid(),
        ResultVersionId = parent,
        PrivateMediaKey = d.PrivateMediaKey,
        OriginalFileName = d.OriginalFileName,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        Sha256 = d.Sha256,
        UploadedAtUtc = d.UploadedAtUtc,
        Kind = d.Kind
    };
}

public sealed class LabResultHistory : Entity<Guid>
{
    private LabResultHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static LabResultHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
