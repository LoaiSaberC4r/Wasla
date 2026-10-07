using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Radiology;

public sealed class RadiologyCatalogRequest : AggregateRoot<Guid>
{
    private readonly List<RadiologyCatalogRequestHistory> _history = [];
    private RadiologyCatalogRequest() { }
    public Guid RequestedByDoctorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Specimen { get; private set; }
    public string? CatalogClarificationNote { get; private set; }
    public MedicalCatalogRequestStatus Status { get; private set; }
    public Guid? CanonicalCatalogId { get; private set; }
    public string? ReasonType { get; private set; }
    public string? ReviewReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RadiologyCatalogRequestHistory> History => _history.AsReadOnly();
    public static Result<RadiologyCatalogRequest> Submit(DiagnosticCatalogRequestData data, Guid doctor, Guid actor, DateTime now)
    {
        if (!DiagnosticText.ValidRequestData(data)) return Result<RadiologyCatalogRequest>.Fail(DiagnosticErrors.Validation("RadiologyCatalogRequest.InvalidData"));
        var r = new RadiologyCatalogRequest { Id = Guid.NewGuid(), RequestedByDoctorId = doctor, Status = MedicalCatalogRequestStatus.Pending, CreatedAtUtc = now };
        r.SetData(data); r.Record("Submitted", null, null, actor, "Doctor", now); return Result<RadiologyCatalogRequest>.Ok(r);
    }
    public Result Update(DiagnosticCatalogRequestData data, Guid actor, DateTime now)
    {
        if (Status is not (MedicalCatalogRequestStatus.Pending or MedicalCatalogRequestStatus.NeedsMoreInfo)) return Result.Fail(DiagnosticErrors.Conflict("RadiologyCatalogRequest.InvalidState"));
        if (!DiagnosticText.ValidRequestData(data)) return Result.Fail(DiagnosticErrors.Validation("RadiologyCatalogRequest.InvalidData"));
        var before = Snapshot(); SetData(data); Status = MedicalCatalogRequestStatus.Pending; ReviewReason = null;
        Record("Resubmitted", null, before, actor, "Doctor", now); return Result.Ok();
    }
    public Result Review(MedicalCatalogRequestStatus status, string? reason, Guid? catalogId, Guid actor, DateTime now)
    {
        if (Status != MedicalCatalogRequestStatus.Pending || status is not (MedicalCatalogRequestStatus.NeedsMoreInfo or MedicalCatalogRequestStatus.Approved or MedicalCatalogRequestStatus.Rejected))
            return Result.Fail(DiagnosticErrors.Conflict("RadiologyCatalogRequest.InvalidState"));
        if (status == MedicalCatalogRequestStatus.NeedsMoreInfo && !DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("RadiologyCatalogRequest.MoreInfoReasonRequired"));
        if (status == MedicalCatalogRequestStatus.Rejected && !DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("RadiologyCatalogRequest.RejectionReasonRequired"));
        if (status == MedicalCatalogRequestStatus.Approved && catalogId is null) return Result.Fail(DiagnosticErrors.Validation("RadiologyCatalogRequest.InvalidDuplicateTarget"));
        var before = Snapshot(); Status = status; ReviewReason = reason; CanonicalCatalogId = catalogId;
        ReasonType = status == MedicalCatalogRequestStatus.Rejected && catalogId.HasValue ? "Duplicate" : null;
        Record(status.ToString(), reason, before, actor, "MedicalCatalogManager", now); return Result.Ok();
    }
    private void SetData(DiagnosticCatalogRequestData d) { Name = d.Name.Trim(); Specimen = DiagnosticText.Clean(d.Specimen); CatalogClarificationNote = DiagnosticText.Clean(d.CatalogClarificationNote); }
    private string Snapshot() => DiagnosticText.Json(new { Name, Specimen, CatalogClarificationNote, Status, CanonicalCatalogId, ReasonType, ReviewReason });
    private void Record(string action, string? reason, string? before, Guid actor, string type, DateTime now)
    { Revision++; _history.Add(RadiologyCatalogRequestHistory.Create(Id, action, reason, before, Snapshot(), actor, type, now)); }
}

public sealed class RadiologyCatalogRequestHistory : Entity<Guid>
{
    private RadiologyCatalogRequestHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static RadiologyCatalogRequestHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
