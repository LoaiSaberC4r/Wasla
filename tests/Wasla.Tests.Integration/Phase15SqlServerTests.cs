using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Medications;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class Phase15SqlServerTests
{
    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Failure_after_handler_success_compensates_private_files_and_allows_same_key_retry(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var patient = await app.ClientAsync("patient"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var draft = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Test"); var id = draft.GetProperty("requestId").GetGuid();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? draft : null, kind == "radiology" ? draft : null);
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicationIdempotencyRecords] ADD CONSTRAINT [CK_Phase15IdempotencyFailure] CHECK ([Operation] NOT LIKE '%Result:Submit')", TestContext.Current.CancellationToken));
        var key = Guid.NewGuid().ToString("N");
        using (var failed = await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind, key: key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(Directory.Exists(app.MedicalMediaRoot) ? Directory.GetFiles(app.MedicalMediaRoot, "*", SearchOption.AllDirectories) : []);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(0, kind == "lab" ? await db.PatientLabResultSubmissions.CountAsync(TestContext.Current.CancellationToken) : await db.PatientRadiologyResultSubmissions.CountAsync(TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicationIdempotencyRecords] DROP CONSTRAINT [CK_Phase15IdempotencyFailure]", TestContext.Current.CancellationToken);
        });
        await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind, key: key));
        Assert.Single(Directory.GetFiles(app.MedicalMediaRoot, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Complete_visit_rolls_back_both_orders_prescription_encounter_ticket_and_followup_then_retries()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var drugManager = await app.ClientAsync("catalog-manager");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid(); var ticketId = ticket.GetProperty("ticketId").GetGuid();
        var lab = await AddAsync(app, doctor, encounterId, "lab", "Blood test"); var radio = await AddAsync(app, doctor, encounterId, "radiology", "Chest image");
        var drug = await Phase14MedicationApiTests.CatalogDrugAsync(drugManager);
        var prescription = await OkAsync(await PostAsync(doctor, app.EncounterUrl(encounterId) + "/prescription/items", Phase14MedicationApiTests.CompleteItem(drugId: drug.GetProperty("drugCatalogId").GetGuid())));
        var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        encounter = await OkAsync(await doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Documented", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken));
        var body = new
        {
            ticketRowVersion = ticket.GetProperty("rowVersion").GetString(),
            encounterRowVersion = encounter.GetProperty("rowVersion").GetString(),
            prescriptionRowVersion = prescription.GetProperty("rowVersion").GetString(),
            labRequestRowVersion = lab.GetProperty("rowVersion").GetString(),
            radiologyRequestRowVersion = radio.GetProperty("rowVersion").GetString()
        };
        var url = app.TicketUrl(ticketId) + "/complete"; var key = Guid.NewGuid().ToString("N");
        using (var missing = await PostAsync(doctor, url, new { body.ticketRowVersion, body.encounterRowVersion, body.prescriptionRowVersion })) Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [RadiologyRequests] ADD CONSTRAINT [CK_Phase15ForcedPublishFailure] CHECK ([Status] <> 2)", TestContext.Current.CancellationToken));
        using (var failed = await PostAsync(doctor, url, body, key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(TicketStatus.InProgress, (await db.Tickets.SingleAsync(x => x.Id == ticketId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(EncounterStatus.InProgress, (await db.MedicalEncounters.SingleAsync(x => x.Id == encounterId, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(DiagnosticRequestStatus.Draft, (await db.LabRequests.SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.Equal(DiagnosticRequestStatus.Draft, (await db.RadiologyRequests.SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.Equal(PrescriptionVersionStatus.Draft, (await db.PrescriptionVersions.SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.False(await db.LabRequestHistories.AnyAsync(x => x.Action == "Published", TestContext.Current.CancellationToken));
            Assert.False(await db.TicketIdempotencyRecords.AnyAsync(x => x.IdempotencyKey == key, TestContext.Current.CancellationToken));
            Assert.False(await db.FollowUpEligibilities.AnyAsync(TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [RadiologyRequests] DROP CONSTRAINT [CK_Phase15ForcedPublishFailure]", TestContext.Current.CancellationToken);
        });
        await OkAsync(await PostAsync(doctor, url, body, key)); await OkAsync(await PostAsync(doctor, url, body, key));
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Concurrent_draft_creation_and_coverage_uploads_have_one_winner_and_stable_conflicts(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid();
        object body = kind == "lab" ? new { newLabTest = new { testName = "Test" } } : new { newRadiologyProcedure = new { procedureName = "Test" } };
        var adds = await Task.WhenAll(PostAsync(doctor, app.EncounterUrl(e) + $"/{kind}-request/items", body), PostAsync(doctor, app.EncounterUrl(e) + $"/{kind}-request/items", body));
        System.Text.Json.JsonElement draft;
        try { Assert.Single(adds, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(adds, r => r.StatusCode == HttpStatusCode.Conflict); draft = await JsonAsync(adds.Single(r => r.StatusCode == HttpStatusCode.OK)); }
        finally { foreach (var response in adds) response.Dispose(); }
        var id = draft.GetProperty("requestId").GetGuid(); var item = draft.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? draft : null, kind == "radiology" ? draft : null);
        var request = await GetAsync(doctor, Url(kind, $"-requests/{id}")); var token = request.GetProperty("rowVersion").GetString();
        var uploads = await Task.WhenAll(UploadAsync(doctor, Url(kind, $"-requests/{id}/results"), kind, [item], token), UploadAsync(doctor, Url(kind, $"-requests/{id}/results"), kind, [item], token));
        try { Assert.Single(uploads, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(uploads, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in uploads) response.Dispose(); }
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(1, kind == "lab" ? await db.LabCatalogRequests.CountAsync(TestContext.Current.CancellationToken) : await db.RadiologyCatalogRequests.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, kind == "lab" ? await db.LabResults.CountAsync(TestContext.Current.CancellationToken) : await db.RadiologyResults.CountAsync(TestContext.Current.CancellationToken));
        });
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Acceptance_correction_and_void_failures_roll_back_current_version_coverage_history_and_idempotency(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var patient = await app.ClientAsync("patient"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var draft = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Test");
        var id = draft.GetProperty("requestId").GetGuid(); var item = draft.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? draft : null, kind == "radiology" ? draft : null);
        var submitted = await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind)); var s = submitted.GetProperty("submission"); var sid = s.GetProperty("submissionId").GetGuid();
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [PatientLabResultSubmissions] ADD CONSTRAINT [CK_Phase15AcceptFailure] CHECK ([Status] <> 2)" : "ALTER TABLE [PatientRadiologyResultSubmissions] ADD CONSTRAINT [CK_Phase15AcceptFailure] CHECK ([Status] <> 2)"), TestContext.Current.CancellationToken));
        object accept = kind == "lab" ? new { coveredLabRequestItemIds = new[] { item }, rowVersion = s.GetProperty("rowVersion").GetString() } : new { coveredRadiologyRequestItemIds = new[] { item }, rowVersion = s.GetProperty("rowVersion").GetString() };
        var key = Guid.NewGuid().ToString("N"); var acceptUrl = Url(kind, $"-result-submissions/{sid}/accept");
        using (var failed = await PostAsync(doctor, acceptUrl, accept, key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(0, kind == "lab" ? await db.LabResults.CountAsync(TestContext.Current.CancellationToken) : await db.RadiologyResults.CountAsync(TestContext.Current.CancellationToken));
            Assert.False(await db.MedicationIdempotencyRecords.AnyAsync(x => x.IdempotencyKey == key, TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [PatientLabResultSubmissions] DROP CONSTRAINT [CK_Phase15AcceptFailure]" : "ALTER TABLE [PatientRadiologyResultSubmissions] DROP CONSTRAINT [CK_Phase15AcceptFailure]"), TestContext.Current.CancellationToken);
        });
        var accepted = await OkAsync(await PostAsync(doctor, acceptUrl, accept, key)); var r = accepted.GetProperty("result"); var rid = r.GetProperty("resultId").GetGuid();
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [LabResultVersions] ADD CONSTRAINT [CK_Phase15CorrectionFailure] CHECK ([VersionNumber] < 2)" : "ALTER TABLE [RadiologyResultVersions] ADD CONSTRAINT [CK_Phase15CorrectionFailure] CHECK ([VersionNumber] < 2)"), TestContext.Current.CancellationToken));
        key = Guid.NewGuid().ToString("N"); var correctionUrl = Url(kind, $"-results/{rid}/corrections");
        using (var failed = await UploadAsync(doctor, correctionUrl, kind, [item], r.GetProperty("rowVersion").GetString(), "Corrected report", key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("Completed", (await GetAsync(doctor, Url(kind, $"-requests/{id}"))).GetProperty("status").GetString());
        Assert.Equal("Finalized", (await GetAsync(doctor, Url(kind, $"-results/{rid}/versions")))[0].GetProperty("status").GetString());
        await app.WithDbAsync(async db =>
        {
            Assert.False(await db.MedicationIdempotencyRecords.AnyAsync(x => x.IdempotencyKey == key, TestContext.Current.CancellationToken));
            await db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [LabResultVersions] DROP CONSTRAINT [CK_Phase15CorrectionFailure]" : "ALTER TABLE [RadiologyResultVersions] DROP CONSTRAINT [CK_Phase15CorrectionFailure]"), TestContext.Current.CancellationToken);
        });
        var corrected = await OkAsync(await UploadAsync(doctor, correctionUrl, kind, [item], r.GetProperty("rowVersion").GetString(), "Corrected report", key)); r = corrected.GetProperty("result");
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [LabResultVersions] ADD CONSTRAINT [CK_Phase15VoidFailure] CHECK ([Status] <> 3)" : "ALTER TABLE [RadiologyResultVersions] ADD CONSTRAINT [CK_Phase15VoidFailure] CHECK ([Status] <> 3)"), TestContext.Current.CancellationToken));
        key = Guid.NewGuid().ToString("N"); var voidBody = new { rowVersion = r.GetProperty("rowVersion").GetString(), reason = "Invalid report" };
        using (var failed = await PostAsync(doctor, Url(kind, $"-results/{rid}/void"), voidBody, key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("Completed", (await GetAsync(patient, $"/api/v1/{kind}-requests/mine/{id}")).GetProperty("status").GetString());
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync((kind == "lab" ? "ALTER TABLE [LabResultVersions] DROP CONSTRAINT [CK_Phase15VoidFailure]" : "ALTER TABLE [RadiologyResultVersions] DROP CONSTRAINT [CK_Phase15VoidFailure]"), TestContext.Current.CancellationToken));
        await OkAsync(await PostAsync(doctor, Url(kind, $"-results/{rid}/void"), voidBody, key));
        await app.WithDbAsync(async db =>
        {
            Assert.True(await db.EncounterAuditEvents.AnyAsync(x => x.Action == kind.Replace("lab", "Lab", StringComparison.Ordinal).Replace("radiology", "Radiology", StringComparison.Ordinal) + "RequestsRead", TestContext.Current.CancellationToken));
        });
    }
}
