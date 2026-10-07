
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Unit;

public sealed class Phase15LabDomainTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);
    private static (Ticket Ticket, MedicalEncounter Encounter) Encounter()
    {
        var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            new DateOnly(2026, 10, 7), 1, TicketSource.WalkIn, Guid.NewGuid(), "Normal", "Normal", 0, Guid.NewGuid(), "NewConsultation", "Consultation", "Consultation", 300, "Africa/Cairo", Now, Now, CheckInMode.WalkIn, Actor, null)).Value;
        t.Call(Actor, Now); t.StartVisit(Actor, Now); return (t, MedicalEncounter.Start(t, Actor, Now).Value);
    }
    private static LabTestCatalog Catalog(string name = "Test") => LabTestCatalog.CreateWasla(new(DisplayNameEn: name), Actor, Now).Value;
    private static LabRequest Issued(int count = 2)
    {
        var r = LabRequest.CreateDraft(Encounter().Encounter, Actor, Now).Value;
        for (var i = 0; i < count; i++) Assert.True(r.AddCatalogItem(Catalog("Test " + i), "Instructions", Actor, Now).IsSuccess);
        Assert.True(r.Publish(Actor, Now).IsSuccess); return r;
    }
    private static DiagnosticAttachmentData[] Attachments => [new("private/report.pdf", "report.pdf", "application/pdf", 100, new string('A', 64), Now)];
    private static LabResult Result(LabRequest r, params Guid[] ids)
        => LabResult.Record(r, ids, Attachments, "External provider", new DateOnly(2026, 10, 7), DiagnosticUploader.Doctor, Actor, Now, r.DoctorId, Actor, Now).Value;
    [Fact]
    public void Draft_snapshots_duplicate_prevention_and_publication()
    {
        var e = Encounter().Encounter; var catalog = Catalog(); var r = LabRequest.CreateDraft(e, Actor, Now).Value;
        Assert.Equal(e.PatientId, r.PatientId); Assert.Equal(e.DoctorId, r.DoctorId); Assert.Equal(e.DoctorPracticeId, r.DoctorPracticeId);
        Assert.True(r.AddCatalogItem(catalog, "Original instructions", Actor, Now).IsSuccess);
        Assert.False(r.AddCatalogItem(catalog, null, Actor, Now).IsSuccess);
        Assert.False(r.AddSubmittedItem(LabCatalogRequest.Submit(new("test"), r.DoctorId, Actor, Now).Value, null, Actor, Now).IsSuccess);
        catalog.Update(new(DisplayNameEn: "Renamed"), Actor, Now); Assert.Equal("Test", r.Items.Single().NameEnSnapshot);
        Assert.True(r.Publish(Actor, Now).IsSuccess); Assert.Equal(DiagnosticRequestStatus.Requested, r.Status);
        Assert.False(r.UpdateItem(r.Items.Single().Id, "Changed", Actor, Now).IsSuccess);
        Assert.False(r.RemoveItem(r.Items.Single().Id, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Last_draft_item_removal_is_empty_and_cannot_be_issued()
    {
        var r = LabRequest.CreateDraft(Encounter().Encounter, Actor, Now).Value; r.AddCatalogItem(Catalog(), null, Actor, Now);
        Assert.True(r.RemoveItem(r.Items.Single().Id, Actor, Now).IsSuccess); Assert.Empty(r.Items); Assert.False(r.Publish(Actor, Now).IsSuccess);
        Assert.NotEmpty(r.History);
    }
    [Fact]
    public void Post_visit_reason_and_completed_encounter_are_required_and_encounter_is_preserved()
    {
        var (ticket, e) = Encounter(); Assert.False(LabRequest.CreatePostVisit(e, "reason", null, Actor, Now).IsSuccess);
        e.UpdateClinicalNotes("Documented", Actor, Now); e.Complete(ticket, Actor, Now);
        Assert.False(LabRequest.CreatePostVisit(e, "", null, Actor, Now).IsSuccess);
        var r = LabRequest.CreatePostVisit(e, "New investigation", null, Actor, Now).Value; r.AddCatalogItem(Catalog(), null, Actor, Now); r.Publish(Actor, Now);
        Assert.Equal(DiagnosticOrigin.PostVisit, r.Origin); Assert.Equal(DiagnosticRequestStatus.Requested, r.Status); Assert.Equal(EncounterStatus.Completed, e.Status); Assert.Empty(e.Amendments);
    }
    [Fact]
    public void Cancellation_preserves_completed_items_and_recalculates_parent()
    {
        var r = Issued(); var completed = r.Items.First().Id; r.RecalculateCoverage(new HashSet<Guid> { completed }, Actor, Now);
        Assert.Equal(DiagnosticRequestStatus.PartiallyCompleted, r.Status);
        Assert.False(r.Cancel(completed, "Reason", Actor, Now).IsSuccess); Assert.False(r.Cancel(null, "", Actor, Now).IsSuccess);
        Assert.True(r.Cancel(null, "No longer needed", Actor, Now).IsSuccess); Assert.Equal(DiagnosticRequestStatus.Completed, r.Status);
        Assert.Equal("No longer needed", r.Items.Single(i => i.Id != completed).CancellationReason);
        var all = Issued(); all.Cancel(null, "Cancelled", Actor, Now); Assert.Equal(DiagnosticRequestStatus.Cancelled, all.Status);
    }
    [Fact]
    public void Patient_submission_never_completes_the_order_and_acceptance_retains_patient_provenance()
    {
        var r = Issued(); var s = PatientLabResultSubmission.Submit(r, Attachments, "Outside provider", null, "Report note", r.PatientId, Actor, Now).Value;
        Assert.Equal(PatientSubmissionStatus.PendingReview, s.Status); Assert.Equal(DiagnosticRequestStatus.Requested, r.Status);
        var result = LabResult.Record(r, r.Items.Select(i => i.Id).ToArray(), s.Attachments.Select(a => a.Data()).ToArray(), s.ExternalProviderName, null,
            DiagnosticUploader.Patient, s.UploadedByUserId, s.SubmittedAtUtc, r.DoctorId, Actor, Now, s.Id).Value;
        Assert.True(s.Accept(result.Id, Actor, Now).IsSuccess); Assert.Equal(DiagnosticUploader.Patient, result.Current!.OriginallyUploadedBy);
        Assert.Single(result.Current.Attachments); Assert.Equal(2, result.Current.Coverage.Count);
        r.RecalculateCoverage(result.Current.Coverage.Select(c => c.RequestItemId).ToHashSet(), Actor, Now); Assert.Equal(DiagnosticRequestStatus.Completed, r.Status);
    }
    [Theory]
    [InlineData(PatientSubmissionStatus.Accepted)]
    [InlineData(PatientSubmissionStatus.Rejected)]
    [InlineData(PatientSubmissionStatus.Withdrawn)]
    [InlineData(PatientSubmissionStatus.NoLongerApplicable)]
    public void Submission_transitions_are_terminal_and_audited(PatientSubmissionStatus terminal)
    {
        var r = Issued(); var s = PatientLabResultSubmission.Submit(r, Attachments, null, null, null, r.PatientId, Actor, Now).Value;
        var changed = terminal switch
        {
            PatientSubmissionStatus.Accepted => s.Accept(Guid.NewGuid(), Actor, Now),
            PatientSubmissionStatus.Rejected => s.Reject("Readable reason", Actor, Now),
            PatientSubmissionStatus.Withdrawn => s.Withdraw(Actor, Now),
            _ => s.NoLongerApplicable(Actor, Now)
        };
        Assert.True(changed.IsSuccess); Assert.Equal(terminal, s.Status); Assert.False(s.Withdraw(Actor, Now).IsSuccess); Assert.Equal(2, s.History.Count);
    }
    [Fact]
    public void Coverage_rejects_foreign_duplicate_cancelled_and_already_completed_items()
    {
        var r = Issued(); var id = r.Items.First().Id;
        Assert.False(LabResult.Record(r, [Guid.NewGuid()], Attachments, null, null, DiagnosticUploader.Doctor, Actor, Now, r.DoctorId, Actor, Now).IsSuccess);
        Assert.False(LabResult.Record(r, [id, id], Attachments, null, null, DiagnosticUploader.Doctor, Actor, Now, r.DoctorId, Actor, Now).IsSuccess);
        r.RecalculateCoverage(new HashSet<Guid> { id }, Actor, Now);
        Assert.False(LabResult.Record(r, [id], Attachments, null, null, DiagnosticUploader.Doctor, Actor, Now, r.DoctorId, Actor, Now).IsSuccess);
        var pending = r.Items.Single(i => i.Id != id).Id; r.Cancel(pending, "Cancelled", Actor, Now);
        Assert.False(LabResult.Record(r, [pending], Attachments, null, null, DiagnosticUploader.Doctor, Actor, Now, r.DoctorId, Actor, Now).IsSuccess);
    }
    [Fact]
    public void Correction_creates_a_new_version_and_removed_coverage_reopens_items_then_void_reopens_remaining()
    {
        var r = Issued(); var ids = r.Items.Select(i => i.Id).ToArray(); var result = Result(r, ids);
        r.RecalculateCoverage(ids.ToHashSet(), Actor, Now);
        Assert.False(result.Correct(r, [ids[0]], Attachments, null, null, "", Actor, Now).IsSuccess);
        Assert.True(result.Correct(r, [ids[0]], Attachments, "Corrected provider", null, "Correction", Actor, Now).IsSuccess);
        Assert.Equal(DiagnosticVersionStatus.Superseded, result.Versions.First().Status); Assert.Equal(2, result.CurrentVersionNumber);
        r.RecalculateCoverage(result.Current!.Coverage.Select(c => c.RequestItemId).ToHashSet(), Actor, Now);
        Assert.Equal(DiagnosticRequestStatus.PartiallyCompleted, r.Status); Assert.Equal(DiagnosticItemStatus.Requested, r.Items.Single(i => i.Id == ids[1]).Status);
        Assert.False(result.Void("", Actor, Now).IsSuccess); Assert.True(result.Void("Invalid report", Actor, Now).IsSuccess); Assert.Null(result.Current);
        r.RecalculateCoverage(new HashSet<Guid>(), Actor, Now); Assert.Equal(DiagnosticRequestStatus.Requested, r.Status);
        Assert.False(result.Correct(r, ids, Attachments, null, null, "Correction", Actor, Now).IsSuccess); Assert.False(result.Void("again", Actor, Now).IsSuccess);
    }
    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("TRIAL", false)]
    [InlineData("DISCOURAGED", false)]
    [InlineData("DEPRECATED", false)]
    public void Source_status_controls_selectability_and_import_preserves_local_presentation(string status, bool selectable)
    {
        var source = new LoincSourceData("1234-5", "Official name", null, status, "2.83", true, new Dictionary<string, string> { { "COMPONENT", "Component" } }, new Dictionary<string, string[]>());
        var c = LabTestCatalog.Import(source, "hash", Actor, Now).Value; Assert.Equal(selectable, c.IsSelectable); Assert.Null(c.OfficialNameAr);
        c.Update(new(DisplayNameEn: "Local name", DisplayNameAr: "اسم محلي", AliasesEn: "Alias"), Actor, Now);
        c.ApplySource(source with { NameEn = "New official name", OfficialNameAr = "اسم رسمي" }, "newhash", Actor, Now);
        Assert.Equal("Local name", c.NameEn); Assert.Equal("اسم رسمي", c.OfficialNameAr); Assert.Equal("Alias", c.AliasesEn);
    }
    [Fact]
    public void Catalog_merge_and_review_lifecycles_are_reasoned_and_terminal()
    {
        var c = Catalog(); Assert.False(c.MergeInto(c, "Reason", Actor, Now).IsSuccess); Assert.False(c.MergeInto(Catalog(), "", Actor, Now).IsSuccess);
        var target = Catalog("Canonical"); Assert.True(c.MergeInto(target, "Duplicate", Actor, Now).IsSuccess); Assert.False(c.Update(new(DisplayNameEn: "New"), Actor, Now).IsSuccess);
        var r = LabCatalogRequest.Submit(new("Missing test"), Guid.NewGuid(), Actor, Now).Value;
        Assert.False(r.Review(MedicalCatalogRequestStatus.NeedsMoreInfo, "", null, Actor, Now).IsSuccess);
        Assert.True(r.Review(MedicalCatalogRequestStatus.NeedsMoreInfo, "Specimen needed", null, Actor, Now).IsSuccess);
        Assert.False(r.Review(MedicalCatalogRequestStatus.Approved, null, target.Id, Actor, Now).IsSuccess);
        Assert.True(r.Update(new("Missing test", "Serum"), Actor, Now).IsSuccess); Assert.Equal(MedicalCatalogRequestStatus.Pending, r.Status);
        Assert.True(r.Review(MedicalCatalogRequestStatus.Rejected, "Already exists", target.Id, Actor, Now).IsSuccess); Assert.Equal("Duplicate", r.ReasonType);
        Assert.False(r.Update(new("Changed"), Actor, Now).IsSuccess);
    }
}
