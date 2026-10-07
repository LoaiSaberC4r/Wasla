using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Labs;

public sealed class LabTestCatalog : AggregateRoot<Guid>
{
    private readonly List<LabTestCatalogHistory> _history = [];
    private LabTestCatalog() { }
    public MedicalCatalogSource Source { get; private set; }
    public MedicalCatalogStatus Status { get; private set; }
    public string? LoincCode { get; private set; }
    public string? OfficialNameEn { get; private set; }
    public string? OfficialNameAr { get; private set; }
    public string? ExternalStatus { get; private set; }
    public string? SourceVersion { get; private set; }
    public string? SourceDataJson { get; private set; }
    public string? SourceHash { get; private set; }
    public string? Component { get; private set; }
    public string? ShortName { get; private set; }
    public string? AttributesJson { get; private set; }
    public bool IsCommonOrder { get; private set; }
    public string? DisplayNameEn { get; private set; }
    public string? DisplayNameAr { get; private set; }
    public string? AliasesEn { get; private set; }
    public string? AliasesAr { get; private set; }
    public string? InternalNote { get; private set; }
    public string NormalizedSearch { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? MergedIntoId { get; private set; }
    public bool IsLocallyDeactivated { get; private set; }
    public DateTime? LastSeenAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LabTestCatalogHistory> History => _history.AsReadOnly();
    public bool IsSelectable => Status == MedicalCatalogStatus.Active && (Source == MedicalCatalogSource.Wasla || ExternalStatus == "ACTIVE");
    public string NameEn => DisplayNameEn ?? OfficialNameEn ?? string.Empty;
    public string? NameAr => DisplayNameAr ?? OfficialNameAr;
    public CatalogPresentation Presentation() => new(DisplayNameEn, DisplayNameAr, AliasesEn, AliasesAr, InternalNote);
    public static Result<LabTestCatalog> CreateWasla(CatalogPresentation data, Guid actor, DateTime now)
    {
        if (!DiagnosticText.ValidPresentation(data) || string.IsNullOrWhiteSpace(data.DisplayNameEn))
            return Result<LabTestCatalog>.Fail(DiagnosticErrors.Validation("LabCatalog.InvalidData"));
        var c = new LabTestCatalog { Id = Guid.NewGuid(), Source = MedicalCatalogSource.Wasla, Status = MedicalCatalogStatus.Active, CreatedAtUtc = now };
        c.SetPresentation(data); c.Record("Created", null, null, actor, now); return Result<LabTestCatalog>.Ok(c);
    }
    public static Result<LabTestCatalog> Import(LoincSourceData data, string hash, Guid actor, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(data.Code) || string.IsNullOrWhiteSpace(data.NameEn))
            return Result<LabTestCatalog>.Fail(DiagnosticErrors.Validation("LabCatalog.InvalidData"));
        var c = new LabTestCatalog { Id = Guid.NewGuid(), Source = MedicalCatalogSource.Loinc, CreatedAtUtc = now };
        c.SetSource(data, hash, now); c.Record("Imported", null, null, actor, now); return Result<LabTestCatalog>.Ok(c);
    }
    public Result Update(CatalogPresentation data, Guid actor, DateTime now)
    {
        if (Status == MedicalCatalogStatus.Merged) return Result.Fail(DiagnosticErrors.Conflict("LabCatalog.Merged"));
        if (!DiagnosticText.ValidPresentation(data) || Source == MedicalCatalogSource.Wasla && string.IsNullOrWhiteSpace(data.DisplayNameEn))
            return Result.Fail(DiagnosticErrors.Validation("LabCatalog.InvalidData"));
        var before = Snapshot(); SetPresentation(data); Record("Updated", null, before, actor, now); return Result.Ok();
    }
    public Result SetActive(bool active, string? reason, Guid actor, DateTime now)
    {
        if (Status == MedicalCatalogStatus.Merged) return Result.Fail(DiagnosticErrors.Conflict("LabCatalog.Merged"));
        if (active && (Status == MedicalCatalogStatus.Active || Source == MedicalCatalogSource.Loinc && ExternalStatus != "ACTIVE") ||
            !active && Status == MedicalCatalogStatus.Inactive) return Result.Fail(DiagnosticErrors.Conflict("LabCatalog.InvalidState"));
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("LabCatalog.ReasonRequired"));
        var before = Snapshot(); Status = active ? MedicalCatalogStatus.Active : MedicalCatalogStatus.Inactive;
        IsLocallyDeactivated = !active; Record(active ? "Activated" : "Deactivated", reason, before, actor, now); return Result.Ok();
    }
    public Result MergeInto(LabTestCatalog target, string? reason, Guid actor, DateTime now)
    {
        if (Status == MedicalCatalogStatus.Merged) return Result.Fail(DiagnosticErrors.Conflict("LabCatalog.Merged"));
        if (target.Id == Id || !target.IsSelectable) return Result.Fail(DiagnosticErrors.Conflict("LabCatalog.InvalidMergeTarget"));
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("LabCatalog.ReasonRequired"));
        var before = Snapshot(); Status = MedicalCatalogStatus.Merged; MergedIntoId = target.Id;
        Record("Merged", reason, before, actor, now); return Result.Ok();
    }
    public void ApplySource(LoincSourceData data, string hash, Guid actor, DateTime now)
    {
        if (Source != MedicalCatalogSource.Loinc || data.Code != LoincCode) throw new InvalidOperationException("Source identity is immutable.");
        var before = Snapshot(); SetSource(data, hash, now); Record("SourceUpdated", null, before, actor, now);
    }
    public void RedirectMerge(Guid target, Guid actor, DateTime now)
    {
        if (Status != MedicalCatalogStatus.Merged || target == Id) throw new InvalidOperationException("Invalid merge redirect.");
        var before = Snapshot(); MergedIntoId = target; Record("MergeChainFlattened", null, before, actor, now);
    }
    private void SetSource(LoincSourceData d, string hash, DateTime now)
    {
        LoincCode = d.Code; OfficialNameEn = d.NameEn; OfficialNameAr = d.OfficialNameAr; ExternalStatus = d.Status;
        SourceVersion = d.SourceVersion; SourceDataJson = DiagnosticText.Json(d); SourceHash = hash;
        Component = d.Fields.GetValueOrDefault("COMPONENT"); ShortName = d.Fields.GetValueOrDefault("SHORTNAME");
        AttributesJson = DiagnosticText.Json(d.Attributes); IsCommonOrder = d.IsCommonOrder; LastSeenAtUtc = now;
        if (Status != MedicalCatalogStatus.Merged) Status = d.Status == "ACTIVE" && !IsLocallyDeactivated ? MedicalCatalogStatus.Active : MedicalCatalogStatus.Inactive;
        Normalize();
    }
    private void SetPresentation(CatalogPresentation d)
    {
        DisplayNameEn = DiagnosticText.Clean(d.DisplayNameEn); DisplayNameAr = DiagnosticText.Clean(d.DisplayNameAr);
        AliasesEn = DiagnosticText.Clean(d.AliasesEn); AliasesAr = DiagnosticText.Clean(d.AliasesAr); InternalNote = DiagnosticText.Clean(d.InternalNote); Normalize();
    }
    private void Normalize()
    {
        NormalizedName = DiagnosticText.Normalize(NameEn);
        NormalizedSearch = DiagnosticText.Normalize(string.Join(' ', LoincCode, OfficialNameEn, OfficialNameAr, DisplayNameEn, DisplayNameAr, AliasesEn, AliasesAr, ShortName, Component));
    }
    private string Snapshot() => DiagnosticText.Json(new { Source, Status, LoincCode, OfficialNameEn, OfficialNameAr, ExternalStatus, SourceVersion, SourceDataJson, Presentation = Presentation(), MergedIntoId });
    private void Record(string action, string? reason, string? before, Guid actor, DateTime now)
    {
        Revision++; _history.Add(LabTestCatalogHistory.Create(Id, action, reason, before, Snapshot(), actor, "MedicalCatalogManager", now));
    }
}

public sealed class LabTestCatalogHistory : Entity<Guid>
{
    private LabTestCatalogHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static LabTestCatalogHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
