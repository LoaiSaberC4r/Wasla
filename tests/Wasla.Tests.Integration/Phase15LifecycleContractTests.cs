using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

namespace Wasla.Tests.Integration;

public sealed class Phase15LifecycleContractTests
{
    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Withdrawal_rejection_cancellation_and_permission_revocation_preserve_patient_trust(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var doctor = await app.ClientAsync("doctor"); using var patient = await app.ClientAsync("patient"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid();
        var draft = await AddAsync(app, doctor, e, kind, "One"); draft = await AddAsync(app, doctor, e, kind, "Two", draft.GetProperty("rowVersion").GetString()); var id = draft.GetProperty("requestId").GetGuid();
        var items = draft.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? draft : null, kind == "radiology" ? draft : null);
        var submitted = (await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind))).GetProperty("submission");
        var sid = submitted.GetProperty("submissionId").GetGuid();
        var withdrawn = await OkAsync(await PostAsync(patient, $"/api/v1/{kind}-result-submissions/mine/{sid}/withdraw", new { rowVersion = submitted.GetProperty("rowVersion").GetString() }));
        Assert.Equal("Withdrawn", withdrawn.GetProperty("submission").GetProperty("status").GetString());
        submitted = (await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind))).GetProperty("submission"); sid = submitted.GetProperty("submissionId").GetGuid();
        var rejected = await OkAsync(await PostAsync(doctor, Url(kind, $"-result-submissions/{sid}/reject"), new { rowVersion = submitted.GetProperty("rowVersion").GetString(), patientVisibleReason = "Please upload the complete report" }));
        Assert.Equal("Rejected", rejected.GetProperty("submission").GetProperty("status").GetString());
        Assert.Equal("Please upload the complete report", (await GetAsync(patient, $"/api/v1/{kind}-result-submissions/mine/{sid}")).GetProperty("patientVisibleReason").GetString());
        submitted = (await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind))).GetProperty("submission"); sid = submitted.GetProperty("submissionId").GetGuid();
        var request = await GetAsync(doctor, Url(kind, $"-requests/{id}"));
        var uploaded = await OkAsync(await UploadAsync(doctor, Url(kind, $"-requests/{id}/results"), kind, [items[0]], request.GetProperty("rowVersion").GetString()));
        request = uploaded.GetProperty("request"); var result = uploaded.GetProperty("result"); var rid = result.GetProperty("resultId").GetGuid(); var aid = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        using (var completed = await PostAsync(doctor, Url(kind, $"-requests/{id}/items/{items[0]}/cancel"), new { rowVersion = request.GetProperty("rowVersion").GetString(), reason = "Stop" })) Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
        var cancelled = await OkAsync(await PostAsync(doctor, Url(kind, $"-requests/{id}/cancel-remaining"), new { rowVersion = request.GetProperty("rowVersion").GetString(), reason = "No longer needed" }));
        Assert.Equal("Completed", cancelled.GetProperty("status").GetString());
        Assert.Equal("NoLongerApplicable", (await GetAsync(patient, $"/api/v1/{kind}-result-submissions/mine/{sid}")).GetProperty("status").GetString());
        var enKind = kind == "lab" ? "Lab" : "Radiology";
        await app.WithDbAsync(async db =>
        {
            var permission = SystemPermissionIds.For(enKind + "Results.ViewOwn"); var mapping = await db.RolePermissions.SingleAsync(x => x.RoleId == SystemRoleIds.Doctor && x.PermissionId == permission, TestContext.Current.CancellationToken);
            db.RolePermissions.Remove(mapping); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Assert.Empty((await GetAsync(doctor, Url(kind, $"-requests/{id}"))).GetProperty("currentResults").EnumerateArray());
        using (var denied = await doctor.GetAsync(Url(kind, $"-results/{rid}/versions/1/attachments/{aid}/content"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(rid, (await GetAsync(patient, $"/api/v1/{kind}-results/mine/{rid}")).GetProperty("resultId").GetGuid());
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Missing_catalog_review_resubmits_without_rewriting_order_snapshot_and_replays_safely(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var manager = await app.ClientAsync("medical-manager");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var draft = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Submitted label");
        var original = draft.GetProperty("items")[0].GetProperty("nameEnSnapshot").GetString(); var catalogRequestId = draft.GetProperty("items")[0].GetProperty("catalogRequestId").GetGuid();
        var adminUrl = $"/api/v1/admin/{kind}-catalog-requests/{catalogRequestId}";
        var request = await GetAsync(manager, adminUrl);
        request = await OkAsync(await PostAsync(manager, adminUrl + "/request-more-info", new { rowVersion = request.GetProperty("rowVersion").GetString(), reason = "Specify the specimen" }));
        object update = kind == "lab" ? new { data = new { testName = "Revised catalog label", specimen = "Blood" }, rowVersion = request.GetProperty("rowVersion").GetString() } : new { data = new { procedureName = "Revised catalog label", specimen = "Chest" }, rowVersion = request.GetProperty("rowVersion").GetString() };
        request = await OkAsync(await doctor.PutAsJsonAsync(Url(kind, $"-catalog-requests/{catalogRequestId}"), update, TestContext.Current.CancellationToken)); Assert.Equal("Pending", request.GetProperty("status").GetString());
        var approve = new { rowVersion = request.GetProperty("rowVersion").GetString(), approvedData = new { displayNameEn = "Canonical label" } }; var key = Guid.NewGuid().ToString("N");
        var accepted = await OkAsync(await PostAsync(manager, adminUrl + "/approve", approve, key)); var replay = await OkAsync(await PostAsync(manager, adminUrl + "/approve", approve, key));
        Assert.Equal(accepted.GetProperty("canonicalCatalogId").GetGuid(), replay.GetProperty("canonicalCatalogId").GetGuid());
        Assert.Equal(original, (await GetAsync(doctor, Url(kind, $"-requests/{draft.GetProperty("requestId").GetGuid()}"))).GetProperty("items")[0].GetProperty("nameEnSnapshot").GetString());
        using (var mismatch = await PostAsync(manager, adminUrl + "/approve", new { approve.rowVersion, approvedData = new { displayNameEn = "Different" } }, key)) Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Unsupported_or_spoofed_medical_attachments_create_no_submission_or_official_result(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var draft = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Test"); var id = draft.GetProperty("requestId").GetGuid();
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? draft : null, kind == "radiology" ? draft : null);
        foreach (var file in new[] { ("report.pdf", "application/pdf", Encoding.UTF8.GetBytes("not a PDF")), ("report.svg", "image/svg+xml", Encoding.UTF8.GetBytes("<svg/>")), ("report.pdf", "image/png", Encoding.UTF8.GetBytes("%PDF-1.7")) })
        { using var failed = await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{id}/submissions", kind, bytes: file.Item3, name: file.Item1, contentType: file.Item2); Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode); }
        await app.WithDbAsync(async db => { Assert.Equal(0, kind == "lab" ? await db.PatientLabResultSubmissions.CountAsync(TestContext.Current.CancellationToken) : await db.PatientRadiologyResultSubmissions.CountAsync(TestContext.Current.CancellationToken)); });
    }

    [Fact]
    public async Task OpenApi_exposes_diagnostic_routes_concurrency_and_patient_upload_without_coverage_selection()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var client = app.CreateClient(); var api = await GetAsync(client, "/swagger/v1/swagger.json"); var paths = api.GetProperty("paths");
        foreach (var kind in new[] { "lab", "radiology" })
        {
            foreach (var route in new[] { $"/api/v1/admin/{kind}-catalog/imports/preview", $"/api/v1/doctors/me/{kind}-result-submissions/{{submissionId}}/accept", $"/api/v1/{kind}-requests/mine/{{requestId}}/submissions", $"/api/v1/doctors/me/{kind}-results/{{resultId}}/corrections" }) Assert.True(paths.TryGetProperty(route, out _), route);
            Assert.False(paths.GetProperty($"/api/v1/admin/{kind}-catalog/{{id}}").TryGetProperty("delete", out _));
            var form = paths.GetProperty($"/api/v1/{kind}-requests/mine/{{requestId}}/submissions").GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties");
            Assert.DoesNotContain(form.EnumerateObject(), p => p.Name.Contains("covered", StringComparison.OrdinalIgnoreCase));
        }
        var completion = api.GetProperty("components").GetProperty("schemas").GetProperty("CompleteVisitRequest").GetProperty("properties"); Assert.True(completion.TryGetProperty("labRequestRowVersion", out _)); Assert.True(completion.TryGetProperty("radiologyRequestRowVersion", out _));
    }

    [Fact]
    public void Every_diagnostic_error_has_distinct_english_and_arabic_text()
    {
        var rm = new System.Resources.ResourceManager("Wasla.Domain.Resources.ErrorMessage", typeof(ErrorMessage).Assembly); var en = rm.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, true)!;
        var codes = en.Cast<System.Collections.DictionaryEntry>().Select(x => (string)x.Key).Where(k => k.StartsWith("Lab", StringComparison.Ordinal) || k.StartsWith("Radiology", StringComparison.Ordinal) || k.StartsWith("Diagnostic", StringComparison.Ordinal) || k.StartsWith("MedicalCatalogManager", StringComparison.Ordinal)).ToArray(); Assert.True(codes.Length > 100);
        foreach (var code in codes) { var english = ErrorMessage.GetString(code, System.Globalization.CultureInfo.InvariantCulture); var arabic = ErrorMessage.GetString(code, System.Globalization.CultureInfo.GetCultureInfo("ar")); Assert.NotEqual(code, english); Assert.NotEqual(english, arabic); Assert.False(string.IsNullOrWhiteSpace(arabic)); }
    }
}
