using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase14MedicationApiTests;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class Phase14SqlServerTests
{
    [Fact]
    public async Task Correction_failure_rolls_back_superseding_current_version_and_preserves_retry()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var finalized = await FinalizedAsync(app, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid());
        var p = finalized.Prescription; var id = p.GetProperty("prescriptionId").GetGuid(); var currentId = p.GetProperty("current").GetProperty("versionId").GetGuid(); var url = $"/api/v1/doctors/me/prescriptions/{id}/correction-draft";
        p = await AddAsync(doctor, url, new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "correction" });
        await app.WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @preservedId uniqueidentifier = {currentId}; DECLARE @statement nvarchar(max) = N'ALTER TABLE [PrescriptionVersions] ADD CONSTRAINT [CK_Phase14CorrectionFailure] CHECK ([Status] <> 2 OR [Id] = ''' + CONVERT(nvarchar(36), @preservedId) + N''')'; EXEC(@statement)", TestContext.Current.CancellationToken));
        var body = new { rowVersion = p.GetProperty("rowVersion").GetString() }; var key = Guid.NewGuid().ToString("N");
        using var failed = await PostAsync(doctor, url + "/finalize", body, key); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(PrescriptionVersionStatus.Finalized, (await db.PrescriptionVersions.IgnoreAutoIncludes().SingleAsync(v => v.Id == currentId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(1, await db.PrescriptionVersions.IgnoreAutoIncludes().CountAsync(v => v.Status == PrescriptionVersionStatus.Draft, TestContext.Current.CancellationToken));
            Assert.False(await db.PrescriptionAuditEvents.AnyAsync(a => a.Action == "CorrectionFinalized", TestContext.Current.CancellationToken));
            Assert.False(await db.MedicationIdempotencyRecords.AnyAsync(i => i.IdempotencyKey == key, TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [PrescriptionVersions] DROP CONSTRAINT [CK_Phase14CorrectionFailure]", TestContext.Current.CancellationToken);
        });
        using var retry = await PostAsync(doctor, url + "/finalize", body, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var replay = await PostAsync(doctor, url + "/finalize", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }
    [Fact]
    public async Task Concurrent_initial_adds_and_correction_finalizations_have_one_winner()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var drugId = drug.GetProperty("drugCatalogId").GetGuid();
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid(); var url = app.EncounterUrl(e) + "/prescription/items";
        var adds = await Task.WhenAll(PostAsync(doctor, url, CompleteItem(drugId: drugId)), PostAsync(doctor, url, CompleteItem(drugId: drugId)));
        System.Text.Json.JsonElement p;
        try { Assert.Single(adds, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(adds, r => r.StatusCode == HttpStatusCode.Conflict); p = await JsonAsync(adds.Single(r => r.StatusCode == HttpStatusCode.OK)); }
        finally { foreach (var response in adds) response.Dispose(); }
        var id = p.GetProperty("prescriptionId").GetGuid();
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.Prescriptions.CountAsync(TestContext.Current.CancellationToken)); Assert.Equal(1, await db.PrescriptionItems.CountAsync(TestContext.Current.CancellationToken)); });
        var encounter = await GetAsync(doctor, app.EncounterUrl(e));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(e) + "/clinical-notes", new { clinicalNotes = "documented", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        using var completed = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = p.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        p = await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}"); var corrections = await Task.WhenAll(PostAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/correction-draft", new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "correction A" }), PostAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/correction-draft", new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "correction B" }));
        try { Assert.All(corrections, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode)); p = await JsonAsync(corrections[0]); var other = await JsonAsync(corrections[1]); Assert.Equal(p.GetProperty("draft").GetProperty("versionId").GetGuid(), other.GetProperty("draft").GetProperty("versionId").GetGuid()); }
        finally { foreach (var response in corrections) response.Dispose(); }
        var finalizations = await Task.WhenAll(PostAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/correction-draft/finalize", new { rowVersion = p.GetProperty("rowVersion").GetString() }), PostAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/correction-draft/finalize", new { rowVersion = p.GetProperty("rowVersion").GetString() }));
        try { Assert.Single(finalizations, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(finalizations, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in finalizations) response.Dispose(); }
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.PrescriptionVersions.IgnoreAutoIncludes().CountAsync(v => v.Status == PrescriptionVersionStatus.Finalized, TestContext.Current.CancellationToken));
            Assert.Equal(1, await db.PrescriptionVersions.IgnoreAutoIncludes().CountAsync(v => v.Status == PrescriptionVersionStatus.Superseded, TestContext.Current.CancellationToken));
            Assert.Equal(0, await db.PrescriptionVersions.IgnoreAutoIncludes().CountAsync(v => v.Status == PrescriptionVersionStatus.Draft, TestContext.Current.CancellationToken));
        });
    }
    [Fact]
    public async Task Sql_constraints_reject_second_root_draft_current_branch_and_invalid_item_values()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var finalized = await FinalizedAsync(app, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid()); var p = finalized.Prescription; var id = p.GetProperty("prescriptionId").GetGuid(); var versionId = p.GetProperty("current").GetProperty("versionId").GetGuid();
        await app.WithDbAsync(async db =>
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Prescriptions] ([Id],[MedicalEncounterId],[DoctorId],[PatientId],[CreatedOnUtc],[Revision],[LastVersionNumber]) SELECT {Guid.NewGuid()},[MedicalEncounterId],[DoctorId],[PatientId],[CreatedOnUtc],0,1 FROM [Prescriptions] WHERE [Id]={id}", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PrescriptionVersions] ([Id],[PrescriptionId],[VersionNumber],[Status],[CreatedAtUtc],[CreatedByApplicationUserId],[FinalizedAtUtc],[FinalizedByApplicationUserId]) SELECT {Guid.NewGuid()},[PrescriptionId],90,2,[CreatedAtUtc],[CreatedByApplicationUserId],[FinalizedAtUtc],[FinalizedByApplicationUserId] FROM [PrescriptionVersions] WHERE [Id]={versionId}", TestContext.Current.CancellationToken));
        });
        p = await AddAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/correction-draft", new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "correction" }); var draftId = p.GetProperty("draft").GetProperty("versionId").GetGuid();
        await app.WithDbAsync(async db =>
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PrescriptionVersions] ([Id],[PrescriptionId],[VersionNumber],[Status],[CreatedAtUtc],[CreatedByApplicationUserId]) SELECT {Guid.NewGuid()},[PrescriptionId],91,1,[CreatedAtUtc],[CreatedByApplicationUserId] FROM [PrescriptionVersions] WHERE [Id]={draftId}", TestContext.Current.CancellationToken));
            // Different status avoids the draft/current filter: only the predecessor constraint can reject this branch.
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PrescriptionVersions] ([Id],[PrescriptionId],[VersionNumber],[Status],[CreatedAtUtc],[CreatedByApplicationUserId],[PreviousVersionId],[FinalizedAtUtc]) SELECT {Guid.NewGuid()},[PrescriptionId],92,3,[CreatedAtUtc],[CreatedByApplicationUserId],[PreviousVersionId],[CreatedAtUtc] FROM [PrescriptionVersions] WHERE [Id]={draftId}", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PrescriptionItems] SET [QuantityValue]=0 WHERE [PrescriptionVersionId]={draftId}", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PrescriptionItems] SET [DurationType]=2 WHERE [PrescriptionVersionId]={draftId}", TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PrescriptionItems] SET [MedicationSource]=2 WHERE [PrescriptionVersionId]={draftId}", TestContext.Current.CancellationToken));
        });
    }
    [Fact]
    public async Task Completion_failure_rolls_back_ticket_encounter_prescription_audit_and_idempotency()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var ticketId = ticket.GetProperty("ticketId").GetGuid(); var e = ticket.GetProperty("medicalEncounterId").GetGuid();
        var p = await AddAsync(doctor, app.EncounterUrl(e) + "/prescription/items", CompleteItem(drugId: drug.GetProperty("drugCatalogId").GetGuid())); var encounter = await GetAsync(doctor, app.EncounterUrl(e));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(e) + "/clinical-notes", new { clinicalNotes = "documented", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); encounter = await JsonAsync(notes);
        var body = new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = p.GetProperty("rowVersion").GetString() }; var key = Guid.NewGuid().ToString("N");
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [PrescriptionVersions] ADD CONSTRAINT [CK_Phase14ForcedFinalizeFailure] CHECK ([Status] <> 2)", TestContext.Current.CancellationToken));
        using var failed = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(TicketStatus.InProgress, (await db.Tickets.SingleAsync(t => t.Id == ticketId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(EncounterStatus.InProgress, (await db.MedicalEncounters.SingleAsync(x => x.Id == e, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(PrescriptionVersionStatus.Draft, (await db.PrescriptionVersions.IgnoreAutoIncludes().SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.Equal(0, await db.PrescriptionAuditEvents.CountAsync(a => a.Action == "Finalized", TestContext.Current.CancellationToken));
            Assert.False(await db.TicketIdempotencyRecords.AnyAsync(i => i.IdempotencyKey == key, TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [PrescriptionVersions] DROP CONSTRAINT [CK_Phase14ForcedFinalizeFailure]", TestContext.Current.CancellationToken);
        });
        using var retry = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var replay = await PostAsync(doctor, app.TicketUrl(ticketId) + "/complete", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }
    [Fact]
    public async Task Stale_completion_token_returns_409_and_duplicate_import_apply_creates_one_catalog_set()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid(); var url = app.EncounterUrl(e) + "/prescription/items";
        var p = await AddAsync(doctor, url, CompleteItem(drugId: drug.GetProperty("drugCatalogId").GetGuid())); var old = p.GetProperty("rowVersion").GetString();
        await AddAsync(doctor, url, CompleteItem(old, drug.GetProperty("drugCatalogId").GetGuid())); var encounter = await GetAsync(doctor, app.EncounterUrl(e));
        using var stale = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = old });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("Prescription.ConcurrencyConflict", await stale.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var batch = await PreviewAsync(manager, SourceRow("Imported A", "new science"), SourceRow("Imported B", "other science")); var applyUrl = $"/api/v1/admin/drug-catalog/imports/{batch.GetProperty("batchId").GetGuid()}/apply";
        var results = await Task.WhenAll(PostAsync(manager, applyUrl, new { }), PostAsync(manager, applyUrl, new { }));
        try { Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode)); }
        finally { foreach (var response in results) response.Dispose(); }
        await app.WithDbAsync(async db => Assert.Equal(2, await db.DrugCatalogs.CountAsync(d => d.OriginType == DrugOriginType.MedicianDB, TestContext.Current.CancellationToken)));
    }
}
