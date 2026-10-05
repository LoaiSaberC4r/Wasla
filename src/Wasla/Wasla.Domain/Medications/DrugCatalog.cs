using System.Globalization;
using System.Text;
using System.Text.Json;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Medications;

public enum DrugCatalogStatus { Active = 1, NeedsReview = 2, Inactive = 3, Merged = 4 }
public enum DrugOriginType { MedicianDB = 1, DoctorRequest = 2, Manual = 3 }
public sealed record DrugData(string CommercialNameEn, string? CommercialNameAr = null, string? ScientificName = null,
    string? Manufacturer = null, string? DrugClass = null, string? Route = null, string? StrengthText = null,
    string? DosageForm = null, decimal? PriceEgp = null);

public static class MedicationText
{
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string Normalize(string? value)
    {
        var text = (value ?? string.Empty).Normalize(NormalizationForm.FormKD);
        var result = new StringBuilder();
        foreach (var c in text)
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == 'ـ') continue;
            result.Append(c switch { 'أ' or 'إ' or 'آ' => 'ا', 'ى' => 'ي', _ => char.ToUpperInvariant(c) });
        }
        return string.Join(' ', result.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
    public static bool UsableRoute(string? value) => Clean(value) is { } route && !string.Equals(route, "UNKNOWN", StringComparison.OrdinalIgnoreCase);
    public static Result Validate(DrugData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.CommercialNameEn) || data.CommercialNameEn.Length > 500 ||
            data.CommercialNameAr?.Length > 500 || data.ScientificName?.Length > 800 || data.Manufacturer?.Length > 500 ||
            data.DrugClass?.Length > 500 || data.Route?.Length > 200 || data.StrengthText?.Length > 200 || data.DosageForm?.Length > 200 ||
            data.PriceEgp < 0) return Result.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
        return Result.Ok();
    }
}

public sealed class DrugCatalog : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<DrugCatalogHistory> _history = [];
    private DrugCatalog() { }
    public string CommercialNameEn { get; private set; } = string.Empty;
    public string? CommercialNameAr { get; private set; }
    public string? ScientificName { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? DrugClass { get; private set; }
    public string? Route { get; private set; }
    public string? StrengthText { get; private set; }
    public string? DosageForm { get; private set; }
    public decimal? PriceEgp { get; private set; }
    public DrugCatalogStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public Guid? MergedIntoDrugCatalogId { get; private set; }
    public DrugOriginType OriginType { get; private set; }
    public Guid? OriginImportBatchId { get; private set; }
    public Guid? OriginDrugCatalogRequestId { get; private set; }
    public string? SourceIdentityFingerprint { get; private set; }
    public string? SourceContentHash { get; private set; }
    public bool IsManagerReviewed { get; private set; }
    public bool IsPriceManuallyOverridden { get; private set; }
    public string NormalizedCommercialNameEn { get; private set; } = string.Empty;
    public string? NormalizedCommercialNameAr { get; private set; }
    public string? NormalizedScientificName { get; private set; }
    public string? NormalizedManufacturer { get; private set; }
    public bool IsMissingFromLatestSource { get; private set; }
    public Guid? MissingFromSourceSinceBatchId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<DrugCatalogHistory> History => _history.AsReadOnly();
    public DrugData Data() => new(CommercialNameEn, CommercialNameAr, ScientificName, Manufacturer, DrugClass, Route, StrengthText, DosageForm, PriceEgp);

    public static Result<DrugCatalog> Create(DrugData data, DrugOriginType origin, Guid actor, DateTime now,
        Guid? batchId = null, Guid? requestId = null, string? fingerprint = null, string? contentHash = null)
    {
        var valid = MedicationText.Validate(data);
        if (valid.IsFailure) return Result<DrugCatalog>.Fail(valid.Errors);
        if (!Enum.IsDefined(origin) || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            return Result<DrugCatalog>.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
        var drug = new DrugCatalog { Id = Guid.NewGuid(), OriginType = origin, OriginImportBatchId = batchId,
            OriginDrugCatalogRequestId = requestId, SourceIdentityFingerprint = fingerprint, SourceContentHash = contentHash,
            CreatedOnUtc = now, CreatedByApplicationUserId = actor, IsManagerReviewed = origin != DrugOriginType.MedicianDB };
        drug.SetData(data);
        (drug.Status, drug.StatusReason) = origin == DrugOriginType.MedicianDB ? InitialStatus(data) : (DrugCatalogStatus.Active, null);
        drug.Record("Created", drug.StatusReason, null, actor, now);
        return Result<DrugCatalog>.Ok(drug);
    }
    public static (DrugCatalogStatus Status, string? Reason) InitialStatus(DrugData data)
    {
        var source = string.Join(' ', data.CommercialNameEn, data.CommercialNameAr, data.ScientificName, data.Manufacturer, data.DrugClass, data.Route);
        if (source.Contains("CANCELLED", StringComparison.OrdinalIgnoreCase)) return (DrugCatalogStatus.Inactive, "SourceCancelled");
        if (source.Contains("ILLEGAL IMPORT", StringComparison.OrdinalIgnoreCase)) return (DrugCatalogStatus.Inactive, "SourceIllegalImport");
        return string.IsNullOrWhiteSpace(data.ScientificName) ? (DrugCatalogStatus.NeedsReview, null) : (DrugCatalogStatus.Active, null);
    }
    public Result Update(DrugData data, string? reason, Guid actor, DateTime now)
    {
        if (Status == DrugCatalogStatus.Merged) return Result.Fail(MedicationErrors.Conflict("DrugCatalog.AlreadyMerged"));
        if (reason?.Length > 1000) return Result.Fail(MedicationErrors.Validation("DrugCatalog.InvalidData"));
        var valid = MedicationText.Validate(data); if (valid.IsFailure) return valid;
        var clinicalChange = Data() with { PriceEgp = null, Manufacturer = null, DrugClass = null } !=
            data with { PriceEgp = null, Manufacturer = null, DrugClass = null };
        if (Status == DrugCatalogStatus.Active && clinicalChange && string.IsNullOrWhiteSpace(reason))
            return Result.Fail(MedicationErrors.Validation("DrugCatalog.ChangeReasonRequired"));
        var before = Snapshot();
        if (PriceEgp != data.PriceEgp) IsPriceManuallyOverridden = true;
        SetData(data); IsManagerReviewed = true;
        Record("Updated", reason, before, actor, now); return Result.Ok();
    }
    public Result SetActive(bool active, string reason, Guid actor, DateTime now)
    {
        if (Status == DrugCatalogStatus.Merged || active && Status == DrugCatalogStatus.Active || !active && Status == DrugCatalogStatus.Inactive)
            return Result.Fail(MedicationErrors.Conflict("DrugCatalog.InvalidState"));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return Result.Fail(MedicationErrors.Validation(active ? "DrugCatalog.ChangeReasonRequired" : "DrugCatalog.DeactivationReasonRequired"));
        var before = Snapshot(); var previous = Status;
        Status = active ? DrugCatalogStatus.Active : DrugCatalogStatus.Inactive; StatusReason = reason.Trim(); IsManagerReviewed = true;
        Record(active ? previous == DrugCatalogStatus.Inactive ? "Reactivated" : "Activated" : "Deactivated", reason, before, actor, now);
        return Result.Ok();
    }
    public Result MergeInto(DrugCatalog target, string reason, Guid actor, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Id == target.Id) return Result.Fail(MedicationErrors.Conflict("DrugCatalog.CannotMergeIntoSelf"));
        if (Status == DrugCatalogStatus.Merged) return Result.Fail(MedicationErrors.Conflict("DrugCatalog.AlreadyMerged"));
        if (target.Status != DrugCatalogStatus.Active) return Result.Fail(MedicationErrors.Conflict("DrugCatalog.MergeTargetInvalid"));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return Result.Fail(MedicationErrors.Validation("DrugCatalog.ChangeReasonRequired"));
        var before = Snapshot(); Status = DrugCatalogStatus.Merged; MergedIntoDrugCatalogId = target.Id; StatusReason = reason.Trim(); IsManagerReviewed = true;
        Record("Merged", reason, before, actor, now); return Result.Ok();
    }
    public void ApplyImportPrice(decimal? price, string contentHash, Guid actor, DateTime now)
    {
        if (IsPriceManuallyOverridden) return;
        var before = Snapshot(); PriceEgp = price; SourceContentHash = contentHash;
        Record("ImportPriceUpdated", null, before, actor, now);
    }
    public void RequireImportReview(Guid actor, DateTime now)
    {
        if (IsManagerReviewed || Status != DrugCatalogStatus.Active) return;
        var before = Snapshot(); Status = DrugCatalogStatus.NeedsReview; StatusReason = "PossibleDuplicate";
        Record("ImportReviewRequired", StatusReason, before, actor, now);
    }
    public void MarkSourcePresence(bool present, Guid batch, Guid actor, DateTime now)
    {
        if (IsMissingFromLatestSource == !present) return;
        var before = Snapshot(); IsMissingFromLatestSource = !present;
        MissingFromSourceSinceBatchId = present ? null : batch;
        Record("SourcePresenceUpdated", null, before, actor, now);
    }
    private void SetData(DrugData d)
    {
        CommercialNameEn = d.CommercialNameEn; CommercialNameAr = d.CommercialNameAr; ScientificName = d.ScientificName;
        Manufacturer = d.Manufacturer; DrugClass = d.DrugClass; Route = d.Route; StrengthText = d.StrengthText; DosageForm = d.DosageForm; PriceEgp = d.PriceEgp;
        NormalizedCommercialNameEn = MedicationText.Normalize(CommercialNameEn); NormalizedCommercialNameAr = MedicationText.Normalize(CommercialNameAr);
        NormalizedScientificName = MedicationText.Normalize(ScientificName); NormalizedManufacturer = MedicationText.Normalize(Manufacturer);
    }
    private string Snapshot() => JsonSerializer.Serialize(new { Data = Data(), Status, StatusReason, MergedIntoDrugCatalogId, IsManagerReviewed, IsPriceManuallyOverridden, IsMissingFromLatestSource });
    private void Record(string action, string? reason, string? before, Guid actor, DateTime now)
    {
        Revision++; ModifiedOnUtc = now; ModifiedByApplicationUserId = actor;
        _history.Add(DrugCatalogHistory.Create(Id, action, reason, before, Snapshot(), actor, now));
    }
}

public sealed class DrugCatalogHistory : Entity<Guid>
{
    private DrugCatalogHistory() { }
    public Guid DrugCatalogId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid PerformedByApplicationUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    internal static DrugCatalogHistory Create(Guid id, string action, string? reason, string? before, string after, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), DrugCatalogId = id, Action = action, Reason = reason, BeforeSnapshot = before,
            AfterSnapshot = after, PerformedByApplicationUserId = actor, OccurredAtUtc = now };
}
