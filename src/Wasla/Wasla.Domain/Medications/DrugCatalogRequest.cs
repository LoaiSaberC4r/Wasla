using BuildingBlock.Domain.EntitiesHelper;
using System.Text.Json;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Medications;

public enum DrugCatalogRequestStatus { Pending = 1, NeedsMoreInfo = 2, Approved = 3, Rejected = 4 }
public sealed record MedicationRequestData(string MedicationName, string? ScientificName = null, string? Manufacturer = null,
    string? DrugClass = null, string? Route = null, string? Strength = null, string? DosageForm = null, string? DoctorNote = null);

public sealed class DrugCatalogRequest : AggregateRoot<Guid>
{
    private readonly List<DrugCatalogRequestHistory> _history = [];
    private DrugCatalogRequest() { }
    public Guid RequestedByDoctorId { get; private set; }
    public Guid RequestedByApplicationUserId { get; private set; }
    public string MedicationName { get; private set; } = string.Empty;
    public string? ScientificName { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? DrugClass { get; private set; }
    public string? Route { get; private set; }
    public string? StrengthText { get; private set; }
    public string? DosageForm { get; private set; }
    public string? DoctorNote { get; private set; }
    public DrugCatalogRequestStatus Status { get; private set; }
    public Guid? ApprovedDrugCatalogId { get; private set; }
    public Guid? DuplicateOfDrugCatalogId { get; private set; }
    public string? CurrentReviewReason { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ModifiedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<DrugCatalogRequestHistory> History => _history.AsReadOnly();
    public MedicationRequestData Data() => new(MedicationName, ScientificName, Manufacturer, DrugClass, Route, StrengthText, DosageForm, DoctorNote);
    public static Result<DrugCatalogRequest> Submit(MedicationRequestData data, Guid doctor, Guid actor, DateTime now)
    {
        var valid = Validate(data); if (valid.IsFailure) return Result<DrugCatalogRequest>.Fail(valid.Errors);
        var request = new DrugCatalogRequest { Id = Guid.NewGuid(), RequestedByDoctorId = doctor,
            RequestedByApplicationUserId = actor, Status = DrugCatalogRequestStatus.Pending, CreatedAtUtc = now };
        request.SetData(data); request.Record("Submitted", null, null, actor, now);
        return Result<DrugCatalogRequest>.Ok(request);
    }
    public Result Update(MedicationRequestData data, Guid actor, DateTime now)
    {
        if (Status is not (DrugCatalogRequestStatus.Pending or DrugCatalogRequestStatus.NeedsMoreInfo))
            return Result.Fail(MedicationErrors.Conflict("DrugCatalogRequest.InvalidState"));
        var valid = Validate(data); if (valid.IsFailure) return valid;
        var before = Snapshot(); var resubmit = Status == DrugCatalogRequestStatus.NeedsMoreInfo;
        SetData(data); Status = DrugCatalogRequestStatus.Pending; CurrentReviewReason = null;
        Record(resubmit ? "Resubmitted" : "Updated", null, before, actor, now); return Result.Ok();
    }
    public Result Review(DrugCatalogRequestStatus status, string? reason, Guid? drugId, Guid actor, DateTime now)
    {
        if (Status is DrugCatalogRequestStatus.Approved or DrugCatalogRequestStatus.Rejected)
            return Result.Fail(MedicationErrors.Conflict(Status == DrugCatalogRequestStatus.Approved ? "DrugCatalogRequest.AlreadyApproved" : "DrugCatalogRequest.AlreadyRejected"));
        if (status is not (DrugCatalogRequestStatus.NeedsMoreInfo or DrugCatalogRequestStatus.Approved or DrugCatalogRequestStatus.Rejected))
            return Result.Fail(MedicationErrors.Conflict("DrugCatalogRequest.InvalidState"));
        if (status == DrugCatalogRequestStatus.NeedsMoreInfo && string.IsNullOrWhiteSpace(reason))
            return Result.Fail(MedicationErrors.Validation("DrugCatalogRequest.MoreInfoReasonRequired"));
        if (status == DrugCatalogRequestStatus.Rejected && string.IsNullOrWhiteSpace(reason))
            return Result.Fail(MedicationErrors.Validation("DrugCatalogRequest.RejectionReasonRequired"));
        if (status == DrugCatalogRequestStatus.Approved && drugId is null || reason?.Length > 1000)
            return Result.Fail(MedicationErrors.Validation("DrugCatalogRequest.InvalidData"));
        var before = Snapshot(); Status = status; CurrentReviewReason = MedicationText.Clean(reason);
        ApprovedDrugCatalogId = status == DrugCatalogRequestStatus.Approved ? drugId : null;
        DuplicateOfDrugCatalogId = status == DrugCatalogRequestStatus.Rejected ? drugId : null;
        ReviewedByApplicationUserId = actor; ReviewedAtUtc = now;
        Record(status switch { DrugCatalogRequestStatus.Approved => "Approved", DrugCatalogRequestStatus.Rejected => "Rejected", _ => "MoreInfoRequested" }, reason, before, actor, now);
        return Result.Ok();
    }
    private static Result Validate(MedicationRequestData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.MedicationName)) return Result.Fail(MedicationErrors.Validation("DrugCatalogRequest.MedicationNameRequired"));
        var valid = MedicationText.Validate(new(data.MedicationName, ScientificName: data.ScientificName, Manufacturer: data.Manufacturer,
            DrugClass: data.DrugClass, Route: data.Route, StrengthText: data.Strength, DosageForm: data.DosageForm));
        return valid.IsFailure || data.DoctorNote?.Length > 2000 ? Result.Fail(MedicationErrors.Validation("DrugCatalogRequest.InvalidData")) : Result.Ok();
    }
    private void SetData(MedicationRequestData d)
    {
        MedicationName = d.MedicationName.Trim(); ScientificName = MedicationText.Clean(d.ScientificName); Manufacturer = MedicationText.Clean(d.Manufacturer);
        DrugClass = MedicationText.Clean(d.DrugClass); Route = MedicationText.Clean(d.Route); StrengthText = MedicationText.Clean(d.Strength);
        DosageForm = MedicationText.Clean(d.DosageForm); DoctorNote = MedicationText.Clean(d.DoctorNote);
    }
    private string Snapshot() => JsonSerializer.Serialize(new { Data = Data(), Status, ApprovedDrugCatalogId, DuplicateOfDrugCatalogId, CurrentReviewReason });
    private void Record(string action, string? reason, string? before, Guid actor, DateTime now)
    {
        Revision++; ModifiedAtUtc = now;
        _history.Add(DrugCatalogRequestHistory.Create(Id, action, reason, before, Snapshot(), actor, now));
    }
}
public sealed class DrugCatalogRequestHistory : Entity<Guid>
{
    private DrugCatalogRequestHistory() { }
    public Guid DrugCatalogRequestId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid PerformedByApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    internal static DrugCatalogRequestHistory Create(Guid id, string action, string? reason, string? before, string after, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), DrugCatalogRequestId = id, Action = action, Reason = reason, BeforeSnapshot = before,
            AfterSnapshot = after, PerformedByApplicationUserId = actor, OccurredAtUtc = now };
}
