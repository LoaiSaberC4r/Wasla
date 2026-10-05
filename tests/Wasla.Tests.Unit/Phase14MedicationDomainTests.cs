using System.Text;
using System.Text.Json;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase14MedicationDomainTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
    private static (Ticket Ticket, MedicalEncounter Encounter) Encounter()
    {
        var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, new DateOnly(2026, 10, 5), 1, TicketSource.WalkIn, Guid.NewGuid(), "Normal", "Normal", 0, Guid.NewGuid(),
            "NewConsultation", "Consultation", "Consultation", 300, "Africa/Cairo", Now, Now, CheckInMode.WalkIn, Actor, null)).Value;
        t.Call(Actor, Now); t.StartVisit(Actor, Now);
        return (t, MedicalEncounter.Start(t, Actor, Now).Value);
    }
    private static DrugCatalog Drug() => DrugCatalog.Create(new("BRUFEN 400 MG 30 TABS.", ScientificName: "Ibuprofen",
        Route: "ORAL", StrengthText: "400 MG", DosageForm: "Tablet", PriceEgp: 50), DrugOriginType.Manual, Actor, Now).Value;
    private static PrescriptionItemData CompleteData => new(Dose: "1 Tablet", FrequencyCode: MedicationFrequency.TwiceDaily,
        FrequencyText: "Twice daily", DurationType: MedicationDurationType.Fixed, DurationValue: 7, DurationUnit: MedicationDurationUnit.Days,
        QuantityValue: 14, QuantityUnit: MedicationQuantityUnit.Tablet, Instructions: "After food");
    [Theory]
    [InlineData("clean", "science", "ORAL", DrugCatalogStatus.Active, null)]
    [InlineData("clean", "science", "UNKNOWN", DrugCatalogStatus.Active, null)]
    [InlineData("SOAP", "science", "UNKNOWN", DrugCatalogStatus.Active, null)]
    [InlineData("clean", null, "ORAL", DrugCatalogStatus.NeedsReview, null)]
    [InlineData("clean CANCELLED", "science", "ORAL", DrugCatalogStatus.Inactive, "SourceCancelled")]
    [InlineData("clean ILLEGAL IMPORT", "science", "ORAL", DrugCatalogStatus.Inactive, "SourceIllegalImport")]
    public void Initial_import_uses_only_approved_classification(string name, string? science, string route, DrugCatalogStatus status, string? reason)
    {
        var drug = DrugCatalog.Create(new(name, ScientificName: science, DrugClass: ".", Route: route), DrugOriginType.MedicianDB,
            Actor, Now, Guid.NewGuid(), fingerprint: "identity").Value;
        Assert.Equal(status, drug.Status); Assert.Equal(reason, drug.StatusReason);
        Assert.Null(drug.StrengthText); Assert.Null(drug.DosageForm); Assert.Single(drug.History);
    }
    [Fact]
    public void Catalog_changes_require_reasons_and_preserve_immutable_snapshots()
    {
        var drug = Drug(); var original = drug.History.Single().AfterSnapshot;
        Assert.False(drug.Update(drug.Data() with { ScientificName = "changed" }, null, Actor, Now).IsSuccess);
        Assert.False(drug.SetActive(false, "", Actor, Now).IsSuccess);
        Assert.True(drug.SetActive(false, "review", Actor, Now).IsSuccess);
        Assert.False(drug.SetActive(true, "", Actor, Now).IsSuccess);
        Assert.True(drug.SetActive(true, "reviewed", Actor, Now).IsSuccess);
        Assert.True(drug.Update(drug.Data() with { PriceEgp = 80 }, null, Actor, Now).IsSuccess);
        drug.ApplyImportPrice(90, "new source", Actor, Now);
        Assert.Equal(80, drug.PriceEgp); Assert.True(drug.IsPriceManuallyOverridden);
        Assert.Equal(original, drug.History.First().AfterSnapshot); Assert.Equal(4, drug.History.Count);
    }
    [Theory]
    [InlineData(DrugCatalogStatus.Active, true)]
    [InlineData(DrugCatalogStatus.Inactive, false)]
    [InlineData(DrugCatalogStatus.NeedsReview, false)]
    [InlineData(DrugCatalogStatus.Merged, false)]
    public void Merge_requires_active_target_and_preserves_source_reference(DrugCatalogStatus targetStatus, bool valid)
    {
        var source = Drug(); var target = Drug();
        if (targetStatus == DrugCatalogStatus.NeedsReview) target = DrugCatalog.Create(new("unreviewed"), DrugOriginType.MedicianDB, Actor, Now).Value;
        if (targetStatus == DrugCatalogStatus.Inactive) target.SetActive(false, "inactive", Actor, Now);
        if (targetStatus == DrugCatalogStatus.Merged) target.MergeInto(Drug(), "duplicate", Actor, Now);
        Assert.Equal(valid, source.MergeInto(target, "duplicate", Actor, Now).IsSuccess);
        if (valid) { Assert.Equal(DrugCatalogStatus.Merged, source.Status); Assert.Equal(target.Id, source.MergedIntoDrugCatalogId); Assert.Equal(2, source.History.Count); }
    }
    [Fact]
    public void Merge_cannot_target_self_and_requires_reason_and_is_terminal()
    {
        var drug = Drug(); Assert.False(drug.MergeInto(drug, "self", Actor, Now).IsSuccess);
        Assert.False(drug.MergeInto(Drug(), "", Actor, Now).IsSuccess);
        Assert.True(drug.MergeInto(Drug(), "duplicate", Actor, Now).IsSuccess);
        Assert.False(drug.SetActive(true, "unmerge", Actor, Now).IsSuccess);
        Assert.False(drug.Update(drug.Data(), "update", Actor, Now).IsSuccess);
    }
    [Fact]
    public void Request_name_only_more_info_resubmission_and_approval_are_audited()
    {
        var request = DrugCatalogRequest.Submit(new("missing medicine"), Guid.NewGuid(), Actor, Now).Value;
        Assert.Equal(DrugCatalogRequestStatus.Pending, request.Status);
        Assert.False(request.Review(DrugCatalogRequestStatus.NeedsMoreInfo, null, null, Actor, Now).IsSuccess);
        request.Review(DrugCatalogRequestStatus.NeedsMoreInfo, "manufacturer?", null, Actor, Now);
        Assert.True(request.Update(new("missing medicine", Manufacturer: "maker"), Actor, Now).IsSuccess);
        Assert.Equal(DrugCatalogRequestStatus.Pending, request.Status);
        var id = Guid.NewGuid(); Assert.True(request.Review(DrugCatalogRequestStatus.Approved, null, id, Actor, Now).IsSuccess);
        Assert.Equal(id, request.ApprovedDrugCatalogId); Assert.False(request.Update(new("edit"), Actor, Now).IsSuccess);
        Assert.Collection(request.History, h => Assert.Equal("Submitted", h.Action), h => Assert.Equal("MoreInfoRequested", h.Action),
            h => Assert.Equal("Resubmitted", h.Action), h => Assert.Equal("Approved", h.Action));
    }
    [Fact]
    public void Rejected_duplicate_links_canonical_drug_and_is_terminal()
    {
        var request = DrugCatalogRequest.Submit(new("name"), Guid.NewGuid(), Actor, Now).Value; var canonical = Guid.NewGuid();
        Assert.False(request.Review(DrugCatalogRequestStatus.Rejected, "", canonical, Actor, Now).IsSuccess);
        Assert.True(request.Review(DrugCatalogRequestStatus.Rejected, "duplicate", canonical, Actor, Now).IsSuccess);
        Assert.Equal(canonical, request.DuplicateOfDrugCatalogId); Assert.Null(request.ApprovedDrugCatalogId);
        Assert.False(request.Review(DrugCatalogRequestStatus.Approved, null, canonical, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Initial_draft_copies_trusted_parties_allows_incomplete_items_and_aggregates_errors()
    {
        var (_, e) = Encounter(); var p = Prescription.CreateDraft(e, Actor, Now).Value;
        Assert.Equal(e.Id, p.MedicalEncounterId); Assert.Equal(e.DoctorId, p.DoctorId); Assert.Equal(e.PatientId, p.PatientId);
        var drug = DrugCatalog.Create(new("unstructured", ScientificName: "science", Route: "UNKNOWN"), DrugOriginType.Manual, Actor, Now).Value;
        Assert.True(p.AddCatalogItem(drug, new(), Actor, Now).IsSuccess);
        Assert.True(p.AddCatalogItem(drug, new(), Actor, Now).IsSuccess);
        var errors = p.FinalizationErrors(); Assert.Equal(12, errors.Count);
        Assert.All(errors, error => Assert.StartsWith("prescription.items/", error.Source));
        Assert.Equal(2, errors.Select(e => e.Source!.Split('/')[1]).Distinct().Count());
        Assert.False(p.FinalizeInitial(e, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Removing_last_initial_item_marks_root_for_automatic_discard()
    {
        var e = Encounter().Encounter; var p = Prescription.CreateDraft(e, Actor, Now).Value;
        p.AddCatalogItem(Drug(), new(), Actor, Now); Assert.False(p.IsEmptyInitialDraft);
        p.RemoveItem(p.Draft!.Items.Single().Id, Actor, Now); Assert.True(p.IsEmptyInitialDraft);
    }
    [Fact]
    public void Snapshots_survive_catalog_changes_and_request_approval()
    {
        var e = Encounter().Encounter; var p = Prescription.CreateDraft(e, Actor, Now).Value; var drug = Drug();
        p.AddCatalogItem(drug, CompleteData, Actor, Now);
        var request = DrugCatalogRequest.Submit(new("submitted", Strength: "5 mg", DosageForm: "Tablet", Route: "Oral"), e.DoctorId, Actor, Now).Value;
        p.AddSubmittedItem(request, CompleteData, Actor, Now);
        var items = p.Draft!.Items.ToArray();
        drug.Update(drug.Data() with { CommercialNameEn = "new name", StrengthText = "800 MG" }, "correction", Actor, Now);
        request.Review(DrugCatalogRequestStatus.Approved, null, drug.Id, Actor, Now);
        Assert.Equal("BRUFEN 400 MG 30 TABS.", items[0].MedicationNameSnapshot); Assert.Equal("400 MG", items[0].StrengthSnapshot);
        Assert.Equal(MedicationSource.DoctorSubmitted, items[1].MedicationSource); Assert.Null(items[1].DrugCatalogId);
        Assert.True(p.FinalizeInitial(e, Actor, Now).IsSuccess);
        Assert.False(p.UpdateItem(items[0].Id, new(), Actor, Now).IsSuccess);
    }
    [Fact]
    public void Correction_clones_new_ids_keeps_current_then_supersedes_it_and_void_is_immutable()
    {
        var (t, e) = Encounter(); var p = Prescription.CreateDraft(e, Actor, Now).Value;
        p.AddCatalogItem(Drug(), CompleteData, Actor, Now); p.FinalizeInitial(e, Actor, Now);
        var original = p.Current!; var item = original.Items.Single();
        Assert.False(p.StartCorrection(e, "reason", Actor, Now).IsSuccess);
        e.UpdateClinicalNotes("documented", Actor, Now); e.Complete(t, Actor, Now);
        Assert.False(p.StartCorrection(e, "", Actor, Now).IsSuccess);
        Assert.True(p.StartCorrection(e, "correct dose", Actor, Now).IsSuccess);
        Assert.True(p.StartCorrection(e, "resume", Actor, Now).IsSuccess); Assert.Equal(2, p.Versions.Count);
        Assert.Equal(original.Id, p.Current!.Id); Assert.NotEqual(item.Id, p.Draft!.Items.Single().Id);
        Assert.False(p.Void("void", Actor, Now).IsSuccess);
        p.UpdateItem(p.Draft.Items.Single().Id, p.Draft.Items.Single().Data() with { Dose = "2 Tablets" }, Actor, Now);
        Assert.True(p.FinalizeCorrection(Actor, Now).IsSuccess); Assert.Equal(PrescriptionVersionStatus.Superseded, original.Status);
        Assert.Equal(2, p.Current!.VersionNumber); Assert.Equal("1 Tablet", item.DoseText);
        Assert.False(p.Void("", Actor, Now).IsSuccess); Assert.True(p.Void("entered in error", Actor, Now).IsSuccess);
        Assert.Equal(PrescriptionVersionStatus.Voided, p.Current.Status); Assert.False(p.Void("again", Actor, Now).IsSuccess);
        Assert.False(p.StartCorrection(e, "again", Actor, Now).IsSuccess);
    }
    [Fact]
    public void Empty_correction_requires_items_and_can_be_discarded_without_voiding_current()
    {
        var (t, e) = Encounter(); var p = Prescription.CreateDraft(e, Actor, Now).Value;
        p.AddCatalogItem(Drug(), CompleteData, Actor, Now); p.FinalizeInitial(e, Actor, Now);
        e.UpdateClinicalNotes("documented", Actor, Now); e.Complete(t, Actor, Now); p.StartCorrection(e, "correction", Actor, Now);
        p.RemoveItem(p.Draft!.Items.Single().Id, Actor, Now); Assert.False(p.IsEmptyInitialDraft);
        Assert.Equal("Prescription.ItemsRequired", p.FinalizeCorrection(Actor, Now).Errors.Single().Code);
        Assert.True(p.DiscardCorrection(Actor, Now).IsSuccess); Assert.Null(p.Draft); Assert.Equal(PrescriptionVersionStatus.Finalized, p.Current!.Status);
    }
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Prn_requires_reason_and_minimum_interval_but_not_normal_frequency(bool prn, bool reason, bool interval)
    {
        var e = Encounter().Encounter; var p = Prescription.CreateDraft(e, Actor, Now).Value;
        p.AddCatalogItem(Drug(), CompleteData with { FrequencyText = null, AsNeeded = prn, PrnReason = reason ? "pain" : null, MinimumIntervalText = interval ? "6 hours" : null }, Actor, Now);
        Assert.Equal(prn && reason && interval, p.FinalizeInitial(e, Actor, Now).IsSuccess);
    }
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(7, true)]
    public void Supplied_fixed_duration_must_be_positive(int duration, bool accepted)
    {
        var p = Prescription.CreateDraft(Encounter().Encounter, Actor, Now).Value;
        Assert.Equal(accepted, p.AddCatalogItem(Drug(), CompleteData with { DurationValue = duration }, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Ongoing_duration_has_no_value_or_unit_quantity_is_positive_with_unit()
    {
        var e = Encounter().Encounter; var p = Prescription.CreateDraft(e, Actor, Now).Value;
        Assert.False(p.AddCatalogItem(Drug(), CompleteData with { DurationType = MedicationDurationType.Ongoing }, Actor, Now).IsSuccess);
        Assert.False(p.AddCatalogItem(Drug(), CompleteData with { QuantityValue = 0 }, Actor, Now).IsSuccess);
        Assert.False(p.AddCatalogItem(Drug(), CompleteData with { QuantityUnit = null }, Actor, Now).IsSuccess);
        Assert.True(p.AddCatalogItem(Drug(), CompleteData with { DurationType = MedicationDurationType.Ongoing, DurationValue = null, DurationUnit = null }, Actor, Now).IsSuccess);
        Assert.True(p.FinalizeInitial(e, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Import_fingerprints_exclude_price_content_hash_includes_it_and_schema_is_strict()
    {
        object Row(decimal price) => new { commercial_name_en = "name", commercial_name_ar = "alias", scientific_name = "science", manufacturer = "maker", drug_class = ".", route = "UNKNOWN", price_egp = price };
        var rows = DrugImportParser.Parse(JsonSerializer.SerializeToUtf8Bytes(new[] { Row(1), Row(2), Row(1) })).Value;
        Assert.Equal(rows[0].IdentityFingerprint, rows[1].IdentityFingerprint); Assert.NotEqual(rows[0].ContentHash, rows[1].ContentHash);
        Assert.Equal(rows[0].ContentHash, rows[2].ContentHash);
        foreach (var invalid in new[] { "[]", "{}", "[{\"commercial_name_en\":\"name\"}]", "[1]", "invalid" }) Assert.True(DrugImportParser.Parse(Encoding.UTF8.GetBytes(invalid)).IsFailure);
    }
}
