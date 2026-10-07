using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase15DiagnosticApiTests
{
    internal static async Task<Phase13ApiFixture> CreateAsync(bool sql = false)
    {
        var app = await Phase14MedicationApiTests.CreateAsync(sql);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
        var hash = await scope.ServiceProvider.GetRequiredService<IPasswordService>().HashAsync(Phase14MedicationApiTests.Password, TestContext.Current.CancellationToken);
        var manager = ApplicationUser.Create(Guid.NewGuid(), "medical-manager", "medical@example.test", null, hash, UserType.MedicalCatalogManager, false, app.Clock.UtcNow).Value;
        db.ApplicationUsers.Add(manager); db.UserRoles.Add(new UserRole(Guid.NewGuid(), manager.Id, SystemRoleIds.MedicalCatalogManager));
        var root = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root", TestContext.Current.CancellationToken);
        root.ChangePassword(hash, app.Clock.UtcNow); await db.SaveChangesAsync(TestContext.Current.CancellationToken); return app;
    }
    internal static string Url(string kind, string suffix) => "/api/v1/doctors/me/" + kind + suffix;
    internal static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response) { Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(response); }
    }
    internal static async Task<JsonElement> AddAsync(Phase13ApiFixture app, HttpClient doctor, Guid encounter, string kind, string name, string? token = null, string? key = null)
    {
        object body = kind == "lab" ? new { newLabTest = new { testName = name }, labRequestRowVersion = token } : new { newRadiologyProcedure = new { procedureName = name }, radiologyRequestRowVersion = token };
        return await OkAsync(await PostAsync(doctor, app.EncounterUrl(encounter) + "/" + kind + "-request/items", body, key));
    }
    internal static async Task<JsonElement> CompleteAsync(Phase13ApiFixture app, HttpClient doctor, JsonElement ticket, JsonElement? lab = null, JsonElement? radiology = null)
    {
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid(); var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "Documented consultation", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        encounter = await JsonAsync(notes);
        return await OkAsync(await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new
        {
            ticketRowVersion = ticket.GetProperty("rowVersion").GetString(),
            encounterRowVersion = encounter.GetProperty("rowVersion").GetString(),
            labRequestRowVersion = lab?.GetProperty("rowVersion").GetString(),
            radiologyRequestRowVersion = radiology?.GetProperty("rowVersion").GetString()
        }));
    }
    internal static Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, string kind, IEnumerable<Guid>? covered = null, string? token = null, string? reason = null, string? key = null, byte[]? bytes = null, string name = "report.pdf", string contentType = "application/pdf")
    {
        var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes ?? Encoding.UTF8.GetBytes("%PDF-1.7\nexternal report")); file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "attachments", name);
        if (kind == "radiology") form.Add(new StringContent("Report"), "attachmentKinds");
        if (covered is not null) foreach (var id in covered) form.Add(new StringContent(id.ToString()), kind == "lab" ? "coveredLabRequestItemIds" : "coveredRadiologyRequestItemIds");
        if (token is not null) form.Add(new StringContent(token), "rowVersion"); if (reason is not null) form.Add(new StringContent(reason), "reason");
        form.Add(new StringContent("External clinic"), kind == "lab" ? "externalLaboratoryName" : "externalRadiologyCenterName");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form }; request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    public async Task Submission_acceptance_correction_void_and_private_media_preserve_trust_chain(string kind, bool sql)
    {
        await using var app = await CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var patient = await app.ClientAsync("patient"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounter = ticket.GetProperty("medicalEncounterId").GetGuid();
        var order = await AddAsync(app, doctor, encounter, kind, "Test A"); var key = Guid.NewGuid().ToString("N");
        order = await AddAsync(app, doctor, encounter, kind, "Test B", order.GetProperty("rowVersion").GetString(), key);
        var id = order.GetProperty("requestId").GetGuid(); var ids = order.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray();
        using (var hidden = await patient.GetAsync($"/api/v1/{kind}-requests/mine/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? order : null, kind == "radiology" ? order : null);
        var patientOrder = await GetAsync(patient, $"/api/v1/{kind}-requests/mine/{id}"); Assert.Equal("Requested", patientOrder.GetProperty("status").GetString()); Assert.False(patientOrder.TryGetProperty("postVisitReason", out _));
        var submitted = await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind)); var submission = submitted.GetProperty("submission"); var sid = submission.GetProperty("submissionId").GetGuid();
        Assert.Equal("Requested", submitted.GetProperty("request").GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, submitted.GetProperty("result").ValueKind);
        object accept = kind == "lab" ? new { coveredLabRequestItemIds = ids, rowVersion = submission.GetProperty("rowVersion").GetString() } : new { coveredRadiologyRequestItemIds = ids, rowVersion = submission.GetProperty("rowVersion").GetString() };
        key = Guid.NewGuid().ToString("N"); var accepted = await OkAsync(await PostAsync(doctor, Url(kind, $"-result-submissions/{sid}/accept"), accept, key));
        var replay = await OkAsync(await PostAsync(doctor, Url(kind, $"-result-submissions/{sid}/accept"), accept, key));
        Assert.Equal(accepted.GetProperty("result").GetProperty("resultId").GetGuid(), replay.GetProperty("result").GetProperty("resultId").GetGuid());
        Assert.Equal("Completed", accepted.GetProperty("request").GetProperty("status").GetString());
        var result = accepted.GetProperty("result"); var rid = result.GetProperty("resultId").GetGuid(); var oldAttachment = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        Assert.Equal("Patient", result.GetProperty("current").GetProperty("originallyUploadedBy").GetString()); Assert.Single(result.GetProperty("current").GetProperty("attachments").EnumerateArray());
        using (var download = await patient.GetAsync($"/api/v1/{kind}-results/mine/{rid}/attachments/{oldAttachment}/content", TestContext.Current.CancellationToken)) { Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType); Assert.True(download.Headers.CacheControl?.NoStore); }
        var corrected = await OkAsync(await UploadAsync(doctor, Url(kind, $"-results/{rid}/corrections"), kind, [ids[0]], result.GetProperty("rowVersion").GetString(), "Correction"));
        Assert.Equal("PartiallyCompleted", corrected.GetProperty("request").GetProperty("status").GetString()); result = corrected.GetProperty("result"); Assert.Equal(2, result.GetProperty("currentVersionNumber").GetInt32());
        using (var hidden = await patient.GetAsync($"/api/v1/{kind}-results/mine/{rid}/attachments/{oldAttachment}/content", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var currentAttachment = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        var patientResult = await GetAsync(patient, $"/api/v1/{kind}-results/mine/{rid}"); Assert.False(patientResult.GetProperty("current").TryGetProperty("correctionReason", out _)); Assert.DoesNotContain("privateMediaKey", patientResult.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var voided = await OkAsync(await PostAsync(doctor, Url(kind, $"-results/{rid}/void"), new { rowVersion = result.GetProperty("rowVersion").GetString(), reason = "Report invalid" }));
        Assert.Equal("Requested", voided.GetProperty("request").GetProperty("status").GetString());
        using (var hidden = await patient.GetAsync($"/api/v1/{kind}-results/mine/{rid}/attachments/{currentAttachment}/content", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var versions = await GetAsync(doctor, Url(kind, $"-results/{rid}/versions")); Assert.Equal("Superseded", versions[0].GetProperty("status").GetString()); Assert.Equal("Voided", versions[1].GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Scope_actor_state_permission_revocation_and_patient_coverage_are_enforced(string kind)
    {
        await using var app = await CreateAsync(); using var doctor = await app.ClientAsync("doctor"); using var patient = await app.ClientAsync("patient"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var order = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Test"); var id = order.GetProperty("requestId").GetGuid();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? order : null, kind == "radiology" ? order : null);
        using var otherDoctor = await app.ClientAsync("other-doctor"); using var otherPatient = await app.ClientAsync("other-patient");
        using (var response = await otherDoctor.GetAsync(Url(kind, $"-requests/{id}"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await otherPatient.GetAsync($"/api/v1/{kind}-requests/mine/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        foreach (var actor in new[] { "reception", "admin", "root", "medical-manager", "catalog-manager" })
        {
            using var denied = await app.ClientAsync(actor); using var response = await denied.GetAsync(Url(kind, $"-requests/{id}"), TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using (var response = await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind, [order.GetProperty("items")[0].GetProperty("itemId").GetGuid()])) Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await app.WithDbAsync(async db => { var d = await db.Doctors.SingleAsync(d => d.Id == app.DoctorId, TestContext.Current.CancellationToken); d.Suspend("Suspended", app.DoctorUserId, app.Clock.UtcNow); await db.SaveChangesAsync(TestContext.Current.CancellationToken); });
        using (var response = await doctor.GetAsync(Url(kind, $"-requests/{id}"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Requested", (await GetAsync(patient, $"/api/v1/{kind}-requests/mine/{id}")).GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Missing_test_atomicity_draft_removal_and_postvisit_orders_preserve_encounter(string kind)
    {
        await using var app = await CreateAsync(); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid(); var order = await AddAsync(app, doctor, e, kind, "Test");
        var token = order.GetProperty("rowVersion").GetString(); var request = order.GetProperty("items")[0].GetProperty("catalogRequestId").GetGuid();
        object duplicate = kind == "lab" ? new { newLabTest = new { testName = "TEST" }, labRequestRowVersion = token } : new { newRadiologyProcedure = new { procedureName = "TEST" }, radiologyRequestRowVersion = token };
        using (var response = await PostAsync(doctor, app.EncounterUrl(e) + $"/{kind}-request/items", duplicate)) Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(1, kind == "lab" ? await db.LabCatalogRequests.CountAsync(TestContext.Current.CancellationToken) : await db.RadiologyCatalogRequests.CountAsync(TestContext.Current.CancellationToken)); });
        var item = order.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        var empty = await OkAsync(await doctor.DeleteAsync(app.EncounterUrl(e) + $"/{kind}-request/items/{item}?rowVersion=" + Uri.EscapeDataString(token!), TestContext.Current.CancellationToken)); Assert.Equal(JsonValueKind.Null, empty.GetProperty("requestId").ValueKind);
        await CompleteAsync(app, doctor, ticket); var before = await GetAsync(doctor, app.EncounterUrl(e));
        object post = kind == "lab" ? new { postVisitReason = "New investigation", items = new[] { new { newLabTest = new { testName = "Post visit test" } } } } : new { postVisitReason = "New investigation", items = new[] { new { newRadiologyProcedure = new { procedureName = "Post visit test" } } } };
        var created = await OkAsync(await PostAsync(doctor, app.EncounterUrl(e) + $"/{kind}-requests/post-visit", post)); Assert.Equal("Requested", created.GetProperty("status").GetString()); Assert.Equal("PostVisit", created.GetProperty("origin").GetString());
        var after = await GetAsync(doctor, app.EncounterUrl(e)); Assert.Equal(before.GetProperty("rowVersion").GetString(), after.GetProperty("rowVersion").GetString());
        using var manager = await app.ClientAsync("medical-manager"); var review = await GetAsync(manager, $"/api/v1/admin/{kind}-catalog-requests/{request}");
        await OkAsync(await PostAsync(manager, $"/api/v1/admin/{kind}-catalog-requests/{request}/approve", new { rowVersion = review.GetProperty("rowVersion").GetString(), approvedData = new { displayNameEn = "Canonical test" } }));
    }
}
