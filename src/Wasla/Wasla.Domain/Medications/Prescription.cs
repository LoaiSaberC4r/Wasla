using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Clinical;

namespace Wasla.Domain.Medications;

public enum PrescriptionVersionStatus { Draft = 1, Finalized = 2, Superseded = 3, Voided = 4 }
public enum MedicationSource { DrugCatalog = 1, DoctorSubmitted = 2 }
public enum MedicationFrequency { OnceDaily = 1, TwiceDaily = 2, ThreeTimesDaily = 3, FourTimesDaily = 4,
    Every4Hours = 5, Every6Hours = 6, Every8Hours = 7, Every12Hours = 8, Morning = 9, Bedtime = 10, OnceOnly = 11, Custom = 12 }
public enum MedicationDurationType { Fixed = 1, Ongoing = 2 }
public enum MedicationDurationUnit { Days = 1, Weeks = 2, Months = 3 }
public enum MedicationQuantityUnit { Tablet = 1, Capsule = 2, Bottle = 3, Ampoule = 4, Vial = 5, Tube = 6,
    Inhaler = 7, Patch = 8, Suppository = 9, Other = 10 }
public sealed record PrescriptionItemData(string? Strength = null, string? DosageForm = null, string? Route = null,
    string? Dose = null, MedicationFrequency? FrequencyCode = null, string? FrequencyText = null,
    MedicationDurationType? DurationType = null, int? DurationValue = null, MedicationDurationUnit? DurationUnit = null,
    bool AsNeeded = false, string? PrnReason = null, string? MinimumIntervalText = null, string? MaxPer24HoursText = null,
    decimal? QuantityValue = null, MedicationQuantityUnit? QuantityUnit = null, string? Instructions = null);
public sealed record PrescriptionCompletionBlocker(string Code, Guid? ItemId, string Field);

public sealed class Prescription : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<PrescriptionVersion> _versions = [];
    private readonly List<PrescriptionAuditEvent> _auditEvents = [];
    private Prescription() { }
    public Guid MedicalEncounterId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PatientId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public int Revision { get; private set; }
    public int LastVersionNumber { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PrescriptionVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyCollection<PrescriptionAuditEvent> AuditEvents => _auditEvents.AsReadOnly();
    public PrescriptionVersion? Draft => _versions.SingleOrDefault(v => v.Status == PrescriptionVersionStatus.Draft);
    public PrescriptionVersion? Current => _versions.SingleOrDefault(v => v.Status is PrescriptionVersionStatus.Finalized or PrescriptionVersionStatus.Voided);
    public bool IsEmptyInitialDraft => _versions.Count == 1 && Draft is { VersionNumber: 1, Items.Count: 0 };

    public static Result<Prescription> CreateDraft(MedicalEncounter encounter, Guid actor, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        if (encounter.Status != EncounterStatus.InProgress) return Result<Prescription>.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var prescription = new Prescription { Id = Guid.NewGuid(), MedicalEncounterId = encounter.Id,
            DoctorId = encounter.DoctorId, PatientId = encounter.PatientId, CreatedOnUtc = now, LastVersionNumber = 1 };
        var version = PrescriptionVersion.Create(prescription.Id, 1, null, null, actor, now);
        prescription._versions.Add(version); prescription.Audit("InitialDraftCreated", version.Id, actor, now);
        return Result<Prescription>.Ok(prescription);
    }
    public Result AddCatalogItem(DrugCatalog drug, PrescriptionItemData data, Guid actor, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(drug);
        if (drug.Status != DrugCatalogStatus.Active) return Result.Fail(MedicationErrors.Conflict("DrugCatalog.NotActive"));
        return AddItem(MedicationSource.DrugCatalog, drug.Id, null, drug.CommercialNameEn, drug.ScientificName,
            data with { Strength = data.Strength ?? drug.StrengthText, DosageForm = data.DosageForm ?? drug.DosageForm,
                Route = data.Route ?? (MedicationText.UsableRoute(drug.Route) ? drug.Route : null) }, actor, now);
    }
    public Result AddSubmittedItem(DrugCatalogRequest request, PrescriptionItemData data, Guid actor, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestedByDoctorId != DoctorId) return Result.Fail(MedicationErrors.AccessDenied("Prescription.AccessDenied"));
        return AddItem(MedicationSource.DoctorSubmitted, null, request.Id, request.MedicationName, request.ScientificName,
            data with { Strength = data.Strength ?? request.StrengthText, DosageForm = data.DosageForm ?? request.DosageForm,
                Route = data.Route ?? (MedicationText.UsableRoute(request.Route) ? request.Route : null) }, actor, now);
    }
    private Result AddItem(MedicationSource source, Guid? drug, Guid? request, string name, string? scientific,
        PrescriptionItemData data, Guid actor, DateTime now)
    {
        if (Draft is not { } draft) return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var created = PrescriptionItem.Create(draft.Id, draft.Items.Count == 0 ? 1 : draft.Items.Max(i => i.SortOrder) + 1,
            source, drug, request, name, scientific, data);
        if (created.IsFailure) return Result.Fail(created.Errors);
        draft.Add(created.Value); Touch(now); _ = actor; return Result.Ok();
    }
    public Result UpdateItem(Guid id, PrescriptionItemData data, Guid actor, DateTime now)
    {
        if (Draft is not { } draft) return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var item = draft.Items.SingleOrDefault(i => i.Id == id);
        if (item is null) return Result.Fail(MedicationErrors.NotFound("PrescriptionItem.NotFound"));
        var valid = item.Update(data); if (valid.IsFailure) return valid;
        Touch(now); _ = actor; return Result.Ok();
    }
    public Result RemoveItem(Guid id, Guid actor, DateTime now)
    {
        if (Draft is not { } draft) return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        if (!draft.Remove(id)) return Result.Fail(MedicationErrors.NotFound("PrescriptionItem.NotFound"));
        Touch(now); _ = actor; return Result.Ok();
    }
    public IReadOnlyList<Error> FinalizationErrors()
    {
        if (Draft is not { } draft) return [MedicationErrors.Conflict("Prescription.InvalidState")];
        var errors = draft.Items.OrderBy(i => i.SortOrder).SelectMany(i => i.FinalizationErrors()).ToList();
        if (draft.Items.Count == 0) errors.Add(MedicationErrors.Validation("Prescription.ItemsRequired", "prescription.items"));
        return errors;
    }
    public IReadOnlyList<PrescriptionCompletionBlocker> CompletionBlockers()
        => FinalizationErrors().Select(e => new PrescriptionCompletionBlocker(e.Code,
            e.Source?.Split('/') is { Length: >= 3 } parts && Guid.TryParse(parts[1], out var id) ? id : null,
            e.Source?.Split('/').Last() ?? "prescription")).ToArray();
    public Result FinalizeInitial(MedicalEncounter encounter, Guid actor, DateTime now)
    {
        if (encounter.Id != MedicalEncounterId || encounter.Status != EncounterStatus.InProgress || Draft is not { VersionNumber: 1 })
            return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var errors = FinalizationErrors(); if (errors.Count > 0) return Result.Fail(errors);
        return FinalizeValidatedInitial(actor, now);
    }
    private Result FinalizeValidatedInitial(Guid actor, DateTime now)
    {
        if (Draft is not { VersionNumber: 1, PreviousVersionId: null }) return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var errors = FinalizationErrors(); if (errors.Count > 0) return Result.Fail(errors);
        Draft!.Finalize(actor, now); Audit("Finalized", Current!.Id, actor, now); return Result.Ok();
    }
    public Result StartCorrection(MedicalEncounter encounter, string reason, Guid actor, DateTime now)
    {
        if (encounter.Id != MedicalEncounterId || encounter.Status != EncounterStatus.Completed || Current is not { Status: PrescriptionVersionStatus.Finalized } current)
            return Result.Fail(MedicationErrors.Conflict("Prescription.NoCurrentVersion"));
        if (Draft is { PreviousVersionId: not null }) return Result.Ok();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return Result.Fail(MedicationErrors.Validation("Prescription.CorrectionReasonRequired"));
        var clone = PrescriptionVersion.Create(Id, ++LastVersionNumber, current.Id, reason.Trim(), actor, now);
        foreach (var item in current.Items.OrderBy(i => i.SortOrder)) clone.Add(item.Clone(clone.Id));
        _versions.Add(clone); Audit("CorrectionStarted", clone.Id, actor, now); return Result.Ok();
    }
    public Result FinalizeCorrection(Guid actor, DateTime now)
    {
        if (Draft is not { PreviousVersionId: not null } draft || Current is not { Status: PrescriptionVersionStatus.Finalized } current || draft.PreviousVersionId != current.Id)
            return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        var errors = FinalizationErrors(); if (errors.Count > 0) return Result.Fail(errors);
        current.Supersede(); draft.Finalize(actor, now); Audit("CorrectionFinalized", draft.Id, actor, now); return Result.Ok();
    }
    public Result DiscardCorrection(Guid actor, DateTime now)
    {
        if (Draft is not { PreviousVersionId: not null } draft) return Result.Fail(MedicationErrors.Conflict("Prescription.InvalidState"));
        _versions.Remove(draft); Audit("CorrectionDiscarded", draft.Id, actor, now); return Result.Ok();
    }
    public Result Void(string reason, Guid actor, DateTime now)
    {
        if (Draft is not null) return Result.Fail(MedicationErrors.Conflict("Prescription.CorrectionDraftAlreadyExists"));
        if (Current is not { Status: PrescriptionVersionStatus.Finalized } current) return Result.Fail(MedicationErrors.Conflict("Prescription.NoCurrentVersion"));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return Result.Fail(MedicationErrors.Validation("Prescription.VoidReasonRequired"));
        current.Void(reason.Trim(), actor, now); Audit("Voided", current.Id, actor, now); return Result.Ok();
    }
    private void Touch(DateTime now) { Revision++; ModifiedOnUtc = now; }
    private void Audit(string action, Guid version, Guid actor, DateTime now)
    {
        Touch(now); _auditEvents.Add(PrescriptionAuditEvent.Create(Id, version, DoctorId, action, actor, now));
    }
}

public sealed class PrescriptionVersion : Entity<Guid>
{
    private readonly List<PrescriptionItem> _items = [];
    private PrescriptionVersion() { }
    public Guid PrescriptionId { get; private set; }
    public int VersionNumber { get; private set; }
    public PrescriptionVersionStatus Status { get; private set; }
    public Guid? PreviousVersionId { get; private set; }
    public string? CorrectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public Guid? FinalizedByApplicationUserId { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public Guid? VoidedByApplicationUserId { get; private set; }
    public string? VoidReason { get; private set; }
    public IReadOnlyCollection<PrescriptionItem> Items => _items.AsReadOnly();
    internal static PrescriptionVersion Create(Guid id, int number, Guid? previous, string? reason, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), PrescriptionId = id, VersionNumber = number, Status = PrescriptionVersionStatus.Draft,
            PreviousVersionId = previous, CorrectionReason = reason, CreatedByApplicationUserId = actor, CreatedAtUtc = now };
    internal void Add(PrescriptionItem item) => _items.Add(item);
    internal bool Remove(Guid id) { var item = _items.SingleOrDefault(i => i.Id == id); return item is not null && _items.Remove(item); }
    internal void Finalize(Guid actor, DateTime now) { Status = PrescriptionVersionStatus.Finalized; FinalizedAtUtc = now; FinalizedByApplicationUserId = actor; }
    internal void Supersede() => Status = PrescriptionVersionStatus.Superseded;
    internal void Void(string reason, Guid actor, DateTime now) { Status = PrescriptionVersionStatus.Voided; VoidReason = reason; VoidedAtUtc = now; VoidedByApplicationUserId = actor; }
}

public sealed class PrescriptionItem : Entity<Guid>
{
    private PrescriptionItem() { }
    public Guid PrescriptionVersionId { get; private set; }
    public int SortOrder { get; private set; }
    public MedicationSource MedicationSource { get; private set; }
    public Guid? DrugCatalogId { get; private set; }
    public Guid? DrugCatalogRequestId { get; private set; }
    public string MedicationNameSnapshot { get; private set; } = string.Empty;
    public string? ScientificNameSnapshot { get; private set; }
    public string? StrengthSnapshot { get; private set; }
    public string? DosageFormSnapshot { get; private set; }
    public string? RouteSnapshot { get; private set; }
    public string? DoseText { get; private set; }
    public MedicationFrequency? FrequencyCode { get; private set; }
    public string? FrequencyText { get; private set; }
    public MedicationDurationType? DurationType { get; private set; }
    public int? DurationValue { get; private set; }
    public MedicationDurationUnit? DurationUnit { get; private set; }
    public bool AsNeeded { get; private set; }
    public string? PrnReason { get; private set; }
    public string? MinimumIntervalText { get; private set; }
    public string? MaxPer24HoursText { get; private set; }
    public decimal? QuantityValue { get; private set; }
    public MedicationQuantityUnit? QuantityUnit { get; private set; }
    public string? Instructions { get; private set; }
    public PrescriptionItemData Data() => new(StrengthSnapshot, DosageFormSnapshot, RouteSnapshot, DoseText,
        FrequencyCode, FrequencyText, DurationType, DurationValue, DurationUnit, AsNeeded, PrnReason,
        MinimumIntervalText, MaxPer24HoursText, QuantityValue, QuantityUnit, Instructions);
    internal static Result<PrescriptionItem> Create(Guid version, int order, MedicationSource source, Guid? drug,
        Guid? request, string name, string? scientific, PrescriptionItemData data)
    {
        if (string.IsNullOrWhiteSpace(name) || source == MedicationSource.DrugCatalog && (drug is null || request is not null) ||
            source == MedicationSource.DoctorSubmitted && (drug is not null || request is null))
            return Result<PrescriptionItem>.Fail(MedicationErrors.Validation("PrescriptionItem.MedicationRequired"));
        var item = new PrescriptionItem { Id = Guid.NewGuid(), PrescriptionVersionId = version, SortOrder = order,
            MedicationSource = source, DrugCatalogId = drug, DrugCatalogRequestId = request,
            MedicationNameSnapshot = name, ScientificNameSnapshot = scientific };
        var update = item.Update(data);
        return update.IsFailure ? Result<PrescriptionItem>.Fail(update.Errors) : Result<PrescriptionItem>.Ok(item);
    }
    internal Result Update(PrescriptionItemData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = ValidateProvided(data, Id);
        if (errors.Count > 0) return Result.Fail(errors);
        StrengthSnapshot = MedicationText.Clean(data.Strength); DosageFormSnapshot = MedicationText.Clean(data.DosageForm);
        RouteSnapshot = MedicationText.Clean(data.Route); DoseText = MedicationText.Clean(data.Dose);
        FrequencyCode = data.FrequencyCode; FrequencyText = MedicationText.Clean(data.FrequencyText);
        DurationType = data.DurationType; DurationValue = data.DurationValue; DurationUnit = data.DurationUnit;
        AsNeeded = data.AsNeeded; PrnReason = MedicationText.Clean(data.PrnReason); MinimumIntervalText = MedicationText.Clean(data.MinimumIntervalText);
        MaxPer24HoursText = MedicationText.Clean(data.MaxPer24HoursText); QuantityValue = data.QuantityValue;
        QuantityUnit = data.QuantityUnit; Instructions = MedicationText.Clean(data.Instructions); return Result.Ok();
    }
    internal PrescriptionItem Clone(Guid version) => Create(version, SortOrder, MedicationSource, DrugCatalogId,
        DrugCatalogRequestId, MedicationNameSnapshot, ScientificNameSnapshot, Data()).Value;
    public IReadOnlyList<Error> FinalizationErrors()
    {
        var errors = ValidateProvided(Data(), Id).ToList();
        void Required(bool missing, string code, string field) { if (missing) errors.Add(ItemError(code, Id, field)); }
        Required(string.IsNullOrWhiteSpace(MedicationNameSnapshot), "MedicationRequired", "medication");
        Required(string.IsNullOrWhiteSpace(StrengthSnapshot), "StrengthRequired", "strength");
        Required(string.IsNullOrWhiteSpace(DosageFormSnapshot), "DosageFormRequired", "dosageForm");
        Required(!MedicationText.UsableRoute(RouteSnapshot), "RouteRequired", "route");
        Required(string.IsNullOrWhiteSpace(DoseText), "DoseRequired", "dose");
        Required(!AsNeeded && string.IsNullOrWhiteSpace(FrequencyText), "FrequencyRequired", "frequency");
        Required(DurationType is null, "DurationRequired", "durationType");
        Required(DurationType == MedicationDurationType.Fixed && DurationValue is null, "DurationValueRequired", "durationValue");
        Required(DurationType == MedicationDurationType.Fixed && DurationUnit is null, "DurationInvalid", "durationUnit");
        Required(AsNeeded && string.IsNullOrWhiteSpace(PrnReason), "PrnReasonRequired", "prnReason");
        Required(AsNeeded && string.IsNullOrWhiteSpace(MinimumIntervalText), "MinimumIntervalRequired", "minimumIntervalText");
        return errors;
    }
    private static Error ItemError(string code, Guid id, string field) => MedicationErrors.Validation("PrescriptionItem." + code, $"prescription.items/{id}/{field}");
    private static List<Error> ValidateProvided(PrescriptionItemData d, Guid id)
    {
        var errors = new List<Error>();
        if (d.DurationValue <= 0 || d.DurationType is { } type && !Enum.IsDefined(type) || d.DurationUnit is { } unit && !Enum.IsDefined(unit) ||
            d.DurationType == MedicationDurationType.Ongoing && (d.DurationValue is not null || d.DurationUnit is not null) ||
            d.DurationType is null && (d.DurationValue is not null || d.DurationUnit is not null)) errors.Add(ItemError("DurationInvalid", id, "duration"));
        if (d.QuantityValue <= 0 || d.QuantityValue is not null && d.QuantityUnit is null || d.QuantityValue is null && d.QuantityUnit is not null ||
            d.QuantityUnit is { } quantityUnit && !Enum.IsDefined(quantityUnit)) errors.Add(ItemError("QuantityInvalid", id, "quantity"));
        if (d.FrequencyCode is { } frequency && !Enum.IsDefined(frequency)) errors.Add(ItemError("FrequencyRequired", id, "frequency"));
        if (new[] { d.Strength, d.DosageForm, d.Route, d.Dose, d.FrequencyText, d.PrnReason, d.MinimumIntervalText, d.MaxPer24HoursText }.Any(s => s?.Length > 200) || d.Instructions?.Length > 2000)
            errors.Add(ItemError("InvalidData", id, "fields"));
        return errors;
    }
}

public sealed class PrescriptionAuditEvent : Entity<Guid>
{
    private PrescriptionAuditEvent() { }
    public Guid PrescriptionId { get; private set; }
    public Guid? PrescriptionVersionId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static PrescriptionAuditEvent Create(Guid id, Guid version, Guid doctor, string action, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), PrescriptionId = id, PrescriptionVersionId = version, DoctorId = doctor,
            Action = action, ActorApplicationUserId = actor, OccurredAtUtc = now };
}
