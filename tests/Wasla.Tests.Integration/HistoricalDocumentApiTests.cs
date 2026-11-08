using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class HistoricalDocumentApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Own_unverified_source_has_private_immutable_versions_and_withdrawal(bool sql)
    {
        await using var f = await CreateAsync(sql);
        var settings = f.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value;
        settings.Enabled = true; settings.RequireRealScanner = false; // This fixture tests storage/contracts, not malware protection.
        using var owner = await f.ClientAsync("patient"); using var other = await f.ClientAsync("other-patient"); using var admin = await f.ClientAsync("root");
        var key = Guid.NewGuid().ToString("N");
        using var created = await Upload(owner, null, key);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = (await JsonAsync(created)).GetProperty("documentId").GetGuid();
        using var replay = await Upload(owner, null, key); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("documentId").GetGuid());
        var path = $"/api/v1/patients/me/historical-documents/{id}";
        var doc = await GetAsync(owner, path);
        Assert.Equal("PatientProvided", doc.GetProperty("source").GetString()); Assert.Equal("Unverified", doc.GetProperty("verificationStatus").GetString());
        Assert.DoesNotContain("mediaKey", doc.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var attachment = Assert.Single(doc.GetProperty("currentVersion").GetProperty("attachments").EnumerateArray());
        Assert.Equal("old-report.pdf", attachment.GetProperty("fileName").GetString());
        var a = attachment.GetProperty("attachmentId").GetGuid();
        using var content = await owner.GetAsync($"{path}/attachments/{a}/content", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode); Assert.True(content.Headers.CacheControl?.NoStore);
        Assert.Contains("nosniff", content.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(FileBytes, await content.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using var stolen = await other.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        using var adminDenied = await admin.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, adminDenied.StatusCode);
        using var revised = await Upload(owner, id, Guid.NewGuid().ToString("N"), doc.GetProperty("rowVersion").GetString());
        Assert.True(revised.IsSuccessStatusCode, await revised.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, (await JsonAsync(revised)).GetProperty("versionNumber").GetInt32());
        var versions = await GetAsync(owner, path + "/versions"); Assert.Equal(2, versions.GetArrayLength());
        var secondPage = await GetAsync(owner, path + "/versions?pageNumber=2&pageSize=1"); Assert.Equal(1, Assert.Single(secondPage.EnumerateArray()).GetProperty("versionNumber").GetInt32());
        doc = await GetAsync(owner, path);
        using var stale = await Upload(owner, id, Guid.NewGuid().ToString("N"), "AAAAAAAAAAA="); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var timeline = await GetAsync(owner, "/api/v1/patients/me/medical-record/timeline?types=historical-documents");
        Assert.Contains("HistoricalPatientDocument", timeline.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("encounterId", timeline.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var withdrawn = await PostAsync(owner, path + "/withdraw", new { rowVersion = doc.GetProperty("rowVersion").GetString(), reason = "Uploaded in error" }, Guid.NewGuid().ToString("N"));
        Assert.True(withdrawn.IsSuccessStatusCode, await withdrawn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var gone = await owner.GetAsync($"{path}/attachments/{a}/content", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        await f.WithDbAsync(async db => {
            var stored = await db.Set<PatientHistoricalDocument>().SingleAsync(d => d.Id == id);
            Assert.Equal(f.PatientId, stored.SubjectPatientId); Assert.Equal(HistoricalDocumentState.Withdrawn, stored.State);
            Assert.Equal(2, await db.Set<PatientHistoricalDocumentVersion>().CountAsync(v => v.DocumentId == id));
            Assert.Equal(3, await db.Set<PatientHistoricalDocumentHistory>().CountAsync(h => h.DocumentId == id));
            Assert.Empty(await db.LabResults.ToArrayAsync()); Assert.Empty(await db.RadiologyResults.ToArrayAsync()); Assert.Empty(await db.MedicalEncounters.ToArrayAsync());
        });
    }
    [Fact]
    public async Task Required_scanner_missing_never_creates_available_metadata()
    {
        await using var f = await CreateAsync();
        f.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value.Enabled = true;
        using var owner = await f.ClientAsync("patient");
        using var response = await Upload(owner, null, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("HistoricalDocument.ScannerUnavailable", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        await f.WithDbAsync(async db => Assert.Empty(await db.Set<PatientHistoricalDocument>().ToArrayAsync()));
    }
    [Fact]
    public async Task Medical_policy_off_denies_child_upload_without_widening_self_route()
    {
        await using var f = await CreateAsync();
        f.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value.Enabled = true;
        using var owner = await f.ClientAsync("patient");
        using var response = await owner.GetAsync($"/api/v1/families/mine/dependents/{f.OtherPatientId}/historical-documents", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    private static readonly byte[] FileBytes = Encoding.ASCII.GetBytes("%PDF-1.4\nHistorical patient source\n%%EOF");
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_source_uses_current_medical_authority_and_preserves_actor_subject_separation(bool sql)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(sql);
        var settings = f.App.Services.GetRequiredService<IOptions<HistoricalDocumentOptions>>().Value; settings.Enabled = true; settings.RequireRealScanner = false;
        var path = $"/api/v1/families/mine/dependents/{f.SecondChildId}/historical-documents";
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = DocumentForm() }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await f.Father.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var id = (await JsonAsync(response)).GetProperty("documentId").GetGuid();
        var detail = await GetAsync(f.Mother, path + $"/{id}"); Assert.False(detail.GetProperty("canWithdraw").GetBoolean());
        using var crossParent = await PostAsync(f.Mother, path + $"/{id}/withdraw", new { rowVersion = detail.GetProperty("rowVersion").GetString(), reason = "Other uploader" }, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, crossParent.StatusCode);
        await f.App.WithDbAsync(async db => {
            var doc = await db.Set<PatientHistoricalDocument>().SingleAsync(d => d.Id == id);
            Assert.Equal(f.SecondChildId, doc.SubjectPatientId); Assert.Equal(f.App.PatientId, doc.UploaderPatientId);
            var actor = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == f.App.PatientId); Assert.Equal(actor.ApplicationUserId, doc.UploadedByApplicationUserId);
            var proof = await db.Set<Wasla.Domain.Families.VerifiedParentChildRelationship>().SingleAsync(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.SecondChildId);
            proof.Revoke(f.App.Clock.UtcNow); await db.SaveChangesAsync();
        });
        using var denied = await f.Father.GetAsync(path + $"/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var motherRetained = await GetAsync(f.Mother, path + $"/{id}"); Assert.Equal(id, motherRetained.GetProperty("documentId").GetGuid());
        await f.App.WithDbAsync(db => db.RolePermissions.Where(p => p.RoleId == Wasla.Domain.Security.SystemRoleIds.Patient &&
            p.PermissionId == Wasla.Domain.Security.SystemPermissionIds.For(Wasla.Domain.Security.PermissionNames.MedicalRecordsViewDependent)).ExecuteDeleteAsync());
        using var readRevoked = await f.Mother.GetAsync(path + $"/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, readRevoked.StatusCode);
        // Revoking medical READ must not manufacture or remove an independent WRITE grant.
        using var writeRequest = new HttpRequestMessage(HttpMethod.Post, path) { Content = DocumentForm() }; writeRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var writeOnly = await f.Mother.SendAsync(writeRequest, TestContext.Current.CancellationToken);
        Assert.True(writeOnly.StatusCode == HttpStatusCode.Created, await writeOnly.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    private static MultipartFormDataContent DocumentForm()
    {
        var form = new MultipartFormDataContent(); form.Add(new StringContent("HistoricalLabReport"), "category"); form.Add(new StringContent("true"), "dateUnknown");
        var file = new ByteArrayContent(FileBytes); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "files", "source.pdf"); return form;
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid? doc, string key, string? version = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("HistoricalRadiologyReport"), "category"); form.Add(new StringContent("true"), "dateUnknown");
        form.Add(new StringContent("Self declared external provider"), "providerName");
        if (version is not null) { form.Add(new StringContent(version), "rowVersion"); form.Add(new StringContent("Corrected metadata"), "reason"); }
        var file = new ByteArrayContent(FileBytes); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "files", "old-report.pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/patients/me/historical-documents" + (doc.HasValue ? $"/{doc}/versions" : "")) { Content = form };
        request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
