using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using System.Text.Json;

namespace Wasla.Domain.Radiology;

public sealed class RadiologyRequest : AggregateRoot<Guid>
{
    private readonly List<RadiologyRequestItem> _items = [];
    private readonly List<RadiologyRequestHistory> _history = [];
    private RadiologyRequest() { }
    public Guid MedicalEncounterId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public DiagnosticOrigin Origin { get; private set; }
    public DiagnosticRequestStatus Status { get; private set; }
    public string? PatientInstructions { get; private set; }
    public string? PostVisitReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RequestedAtUtc { get; private set; }
    public int Revision { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RadiologyRequestItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<RadiologyRequestHistory> History => _history.AsReadOnly();
    public static Result<RadiologyRequest> CreateDraft(MedicalEncounter encounter, Guid actor, DateTime now, string? instructions = null)
        => Create(encounter, DiagnosticOrigin.DuringEncounter, null, instructions, actor, now);
    public static Result<RadiologyRequest> CreatePostVisit(MedicalEncounter encounter, string reason, string? instructions, Guid actor, DateTime now)
        => Create(encounter, DiagnosticOrigin.PostVisit, reason, instructions, actor, now);
    private static Result<RadiologyRequest> Create(MedicalEncounter e, DiagnosticOrigin origin, string? reason, string? instructions, Guid actor, DateTime now)
    {
        if (e.Status != (origin == DiagnosticOrigin.DuringEncounter ? EncounterStatus.InProgress : EncounterStatus.Completed))
            return Result<RadiologyRequest>.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidEncounterState"));
        if (origin == DiagnosticOrigin.PostVisit && !DiagnosticText.ValidReason(reason)) return Result<RadiologyRequest>.Fail(DiagnosticErrors.Validation("RadiologyRequest.PostVisitReasonRequired"));
        if (instructions?.Length > 2000) return Result<RadiologyRequest>.Fail(DiagnosticErrors.Validation("RadiologyRequest.InvalidData"));
        var r = new RadiologyRequest
        {
            Id = Guid.NewGuid(),
            MedicalEncounterId = e.Id,
            PatientId = e.PatientId,
            DoctorId = e.DoctorId,
            DoctorPracticeId = e.DoctorPracticeId,
            Origin = origin,
            Status = DiagnosticRequestStatus.Draft,
            PostVisitReason = reason,
            PatientInstructions = DiagnosticText.Clean(instructions),
            CreatedAtUtc = now
        };
        r.Record("Created", reason, null, actor, now); return Result<RadiologyRequest>.Ok(r);
    }
    public Result AddCatalogItem(RadiologyProcedureCatalog catalog, string? instructions, Guid actor, DateTime now)
    {
        if (!catalog.IsSelectable) return Result.Fail(DiagnosticErrors.Conflict("RadiologyCatalog.NotActive"));
        return Add(catalog.Id, null, catalog.NameAr, catalog.NameEn, catalog.LoincCode, catalog.AttributesJson, instructions, actor, now);
    }
    public Result AddSubmittedItem(RadiologyCatalogRequest submitted, string? instructions, Guid actor, DateTime now)
    {
        if (submitted.RequestedByDoctorId != DoctorId) return Result.Fail(DiagnosticErrors.Denied());
        return Add(null, submitted.Id, null, submitted.Name, null, null, instructions, actor, now);
    }
    private Result Add(Guid? catalog, Guid? submitted, string? nameAr, string nameEn, string? code, string? attrs, string? instructions, Guid actor, DateTime now)
    {
        if (Status != DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"));
        if (catalog.HasValue == submitted.HasValue) return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.MultipleSources"));
        if (instructions?.Length > 2000) return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.InvalidData"));
        var normalized = DiagnosticText.Normalize(nameEn);
        if (_items.Any(i => i.NormalizedName == normalized || catalog.HasValue && i.CatalogId == catalog || code is not null && i.LoincCodeSnapshot == code))
            return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.DuplicateTest"));
        var before = Snapshot(); _items.Add(RadiologyRequestItem.Create(Id, catalog, submitted, nameAr, nameEn, code, attrs, instructions));
        Record("ItemAdded", null, before, actor, now); return Result.Ok();
    }
    public Result UpdateItem(Guid id, string? instructions, Guid actor, DateTime now)
    {
        if (Status != DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"));
        var item = _items.SingleOrDefault(i => i.Id == id);
        if (item is null) return Result.Fail(DiagnosticErrors.NotFound("RadiologyRequest.ItemNotFound"));
        if (instructions?.Length > 2000) return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.InvalidData"));
        var before = Snapshot(); item.Instruct(instructions); Record("ItemUpdated", null, before, actor, now); return Result.Ok();
    }
    public Result RemoveItem(Guid id, Guid actor, DateTime now)
    {
        if (Status != DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"));
        var item = _items.SingleOrDefault(i => i.Id == id);
        if (item is null) return Result.Fail(DiagnosticErrors.NotFound("RadiologyRequest.ItemNotFound"));
        var before = Snapshot(); _items.Remove(item); Record("DraftItemRemoved", null, before, actor, now); return Result.Ok();
    }
    public Result Publish(Guid actor, DateTime now)
    {
        if (Status != DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"));
        if (_items.Count == 0) return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.Empty"));
        var before = Snapshot(); Status = DiagnosticRequestStatus.Requested; RequestedAtUtc = now;
        Record(Origin == DiagnosticOrigin.PostVisit ? "PostVisitRequested" : "Published", PostVisitReason, before, actor, now); return Result.Ok();
    }
    public Result Cancel(Guid? itemId, string reason, Guid actor, DateTime now)
    {
        if (Status == DiagnosticRequestStatus.Draft) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.InvalidState"));
        if (!DiagnosticText.ValidReason(reason)) return Result.Fail(DiagnosticErrors.Validation("RadiologyRequest.CancellationReasonRequired"));
        var items = _items.Where(i => itemId is null ? i.Status == DiagnosticItemStatus.Requested : i.Id == itemId).ToArray();
        if (items.Length == 0) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.ItemNotRequested"));
        if (items.Any(i => i.Status == DiagnosticItemStatus.Completed)) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.CompletedItemCannotBeCancelled"));
        if (items.Any(i => i.Status != DiagnosticItemStatus.Requested)) return Result.Fail(DiagnosticErrors.Conflict("RadiologyRequest.ItemNotRequested"));
        var before = Snapshot(); foreach (var i in items) i.Cancel(reason, actor, now); Recalculate();
        Record("Cancelled", reason, before, actor, now); return Result.Ok();
    }
    public void RecalculateCoverage(IReadOnlySet<Guid> coveredIds, Guid actor, DateTime now)
    {
        if (Status == DiagnosticRequestStatus.Draft) throw new InvalidOperationException("Drafts cannot have results.");
        var before = Snapshot();
        foreach (var item in _items.Where(i => i.Status != DiagnosticItemStatus.Cancelled)) item.SetCompleted(coveredIds.Contains(item.Id));
        Recalculate(); Record("CoverageRecalculated", null, before, actor, now);
    }
    private void Recalculate()
    {
        var completed = _items.Any(i => i.Status == DiagnosticItemStatus.Completed);
        var requested = _items.Any(i => i.Status == DiagnosticItemStatus.Requested);
        Status = requested ? completed ? DiagnosticRequestStatus.PartiallyCompleted : DiagnosticRequestStatus.Requested :
            completed ? DiagnosticRequestStatus.Completed : DiagnosticRequestStatus.Cancelled;
    }
    private string Snapshot() => DiagnosticText.Json(new { Status, Origin, PatientInstructions, PostVisitReason, Items = _items.Select(i => new { i.Id, i.CatalogId, i.CatalogRequestId, i.NameArSnapshot, i.NameEnSnapshot, i.Status, i.DoctorInstructions, i.CancellationReason }) });
    private void Record(string action, string? reason, string? before, Guid actor, DateTime now)
    { Revision++; _history.Add(RadiologyRequestHistory.Create(Id, action, reason, before, Snapshot(), actor, "Doctor", now)); }
}
public sealed class RadiologyRequestItem : Entity<Guid>
{
    private RadiologyRequestItem() { }
    public Guid RequestId { get; private set; }
    public DiagnosticItemSource Source { get; private set; }
    public Guid? CatalogId { get; private set; }
    public Guid? CatalogRequestId { get; private set; }
    public string? NameArSnapshot { get; private set; }
    public string NameEnSnapshot { get; private set; } = string.Empty;
    public string? LoincCodeSnapshot { get; private set; }
    public string? ModalitySnapshot { get; private set; }
    public string? AnatomicLocationSnapshot { get; private set; }
    public string? LateralitySnapshot { get; private set; }
    public string NormalizedName { get; private set; } = string.Empty;
    public string? DoctorInstructions { get; private set; }
    public DiagnosticItemStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    internal static RadiologyRequestItem Create(Guid request, Guid? catalog, Guid? submitted, string? ar, string en, string? code, string? attrs, string? instructions)
    {
        var a = attrs is null ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) :
            JsonSerializer.Deserialize<Dictionary<string, string[]>>(attrs)!.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        string? Attr(string name) => a.TryGetValue(name, out var values) ? string.Join("; ", values) : null;
        return new()
        {
            Id = Guid.NewGuid(),
            RequestId = request,
            Source = catalog.HasValue ? DiagnosticItemSource.Catalog : DiagnosticItemSource.DoctorSubmitted,
            CatalogId = catalog,
            CatalogRequestId = submitted,
            NameArSnapshot = ar,
            NameEnSnapshot = en,
            LoincCodeSnapshot = code,
            NormalizedName = DiagnosticText.Normalize(en),
            DoctorInstructions = DiagnosticText.Clean(instructions),
            Status = DiagnosticItemStatus.Requested,
            ModalitySnapshot = Attr("Rad.Modality.Modality Type"),
            AnatomicLocationSnapshot = Attr("Rad.Anatomic Location.Region Imaged"),
            LateralitySnapshot = Attr("Rad.Anatomic Location.Laterality")
        };
    }
    internal void Instruct(string? text) => DoctorInstructions = DiagnosticText.Clean(text);
    internal void Cancel(string reason, Guid actor, DateTime now) { Status = DiagnosticItemStatus.Cancelled; CancellationReason = reason.Trim(); CancelledByUserId = actor; CancelledAtUtc = now; }
    internal void SetCompleted(bool covered) => Status = covered ? DiagnosticItemStatus.Completed : DiagnosticItemStatus.Requested;
}

public sealed class RadiologyRequestHistory : Entity<Guid>
{
    private RadiologyRequestHistory() { }
    public Guid ResourceId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string AfterSnapshot { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    internal static RadiologyRequestHistory Create(Guid resource, string action, string? reason, string? before, string after, Guid actor, string actorType, DateTime now)
        => new() { Id = Guid.NewGuid(), ResourceId = resource, Action = action, Reason = reason, BeforeSnapshot = before, AfterSnapshot = after, ActorUserId = actor, ActorType = actorType, OccurredAtUtc = now };
}
