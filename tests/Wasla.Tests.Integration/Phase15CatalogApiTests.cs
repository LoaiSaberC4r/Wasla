using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

namespace Wasla.Tests.Integration;

public sealed class Phase15CatalogApiTests
{
    [Fact]
    public async Task Sql_catalog_supports_the_full_allowed_local_name_length()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var manager = await app.ClientAsync("medical-manager");
        var name = new string('A', 1000);
        var catalog = await OkAsync(await PostAsync(manager, "/api/v1/admin/lab-catalog", new { displayNameEn = name }));
        Assert.Equal(name, catalog.GetProperty("nameEn").GetString());
    }
    internal static byte[] Package(string arabic = "اسم رسمي", string status = "ACTIVE", string name = "Imported test")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void File(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(text); }
            File("AccessoryFiles/PanelsAndForms/Loinc.csv", "unrelated noncanonical content");
            File("LoincTable/Loinc.csv", "LOINC_NUM,COMPONENT,STATUS,CLASSTYPE,ORDER_OBS,LONG_COMMON_NAME,SHORTNAME\n" +
                $"1234-5,Analyte,{status},1,Order,{name},Lab\n2345-6,Imaging,{status},2,Order,{name},Radio\n");
            File("AccessoryFiles/LoincUniversalLabOrdersValueSet/LoincUniversalLabOrdersValueSet.csv", "LOINC_NUM\n1234-5\n");
            File("AccessoryFiles/LinguisticVariants/arJO32LinguisticVariant.csv", $"LOINC_NUM,LONG_COMMON_NAME\n1234-5,{arabic}\n2345-6,{arabic}\n");
            File("AccessoryFiles/LoincRsnaRadiologyPlaybook/LoincRsnaRadiologyPlaybook.csv", "LoincNumber,PartTypeName,PartName\n2345-6,Rad.Modality.Modality Type,XR\n2345-6,Rad.Anatomic Location.Region Imaged,Chest\n");
        }
        return stream.ToArray();
    }
    internal static async Task<JsonElement> PreviewAsync(HttpClient manager, string kind, byte[]? bytes = null)
    {
        using var form = new MultipartFormDataContent(); using var file = new ByteArrayContent(bytes ?? Package());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip"); form.Add(file, "file", "Loinc_2.83.zip");
        form.Add(new StringContent("2.83"), "sourceVersion");
        return await OkAsync(await manager.PostAsync($"/api/v1/admin/{kind}-catalog/imports/preview", form, TestContext.Current.CancellationToken));
    }
    internal static string ApplyUrl(string kind, JsonElement batch) => $"/api/v1/admin/{kind}-catalog/imports/{batch.GetProperty("batchId").GetGuid()}/apply";

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Streamed_official_layout_preview_ignores_unrelated_table_and_records_exact_package_hash(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync();
        using var manager = await app.ClientAsync("medical-manager");
        var bytes = Package();
        var batch = await PreviewAsync(manager, kind, bytes);
        Assert.Equal(1, batch.GetProperty("totalRecords").GetInt32());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), batch.GetProperty("fileSha256").GetString());
        var changes = await GetAsync(manager, $"/api/v1/admin/{kind}-catalog/imports/{batch.GetProperty("batchId").GetGuid()}/changes");
        Assert.Equal(kind == "lab" ? "1234-5" : "2345-6", Assert.Single(changes.GetProperty("items").EnumerateArray()).GetProperty("loincCode").GetString());
    }
    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Persisted_preview_cannot_be_extended_with_new_source_records(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync();
        using var manager = await app.ClientAsync("medical-manager");
        var batch = await PreviewAsync(manager, kind);
        var id = batch.GetProperty("batchId").GetGuid();
        await app.WithDbAsync(async db =>
        {
            if (kind == "lab")
            {
                await db.LabCatalogImportBatches.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
                var source = await db.LabCatalogImportRecords.SingleAsync(x => x.ImportBatchId == id, TestContext.Current.CancellationToken);
                var temporary = LabCatalogImportBatch.Stage("2.83", "synthetic.zip", new string('A', 64), Guid.NewGuid(), DateTime.UtcNow);
                temporary.Add(JsonSerializer.Deserialize<LoincSourceData>(source.SourceDataJson)! with { Code = "9999-9" }, source.SourceHash, DiagnosticImportDisposition.New, null, null);
                var record = Assert.Single(temporary.Records);
                db.Entry(record).Property(x => x.ImportBatchId).CurrentValue = id;
                db.LabCatalogImportRecords.Add(record);
            }
            else
            {
                await db.RadiologyCatalogImportBatches.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
                var source = await db.RadiologyCatalogImportRecords.SingleAsync(x => x.ImportBatchId == id, TestContext.Current.CancellationToken);
                var temporary = RadiologyCatalogImportBatch.Stage("2.83", "synthetic.zip", new string('A', 64), Guid.NewGuid(), DateTime.UtcNow);
                temporary.Add(JsonSerializer.Deserialize<LoincSourceData>(source.SourceDataJson)! with { Code = "9999-9" }, source.SourceHash, DiagnosticImportDisposition.New, null, null);
                var record = Assert.Single(temporary.Records);
                db.Entry(record).Property(x => x.ImportBatchId).CurrentValue = id;
                db.RadiologyCatalogImportRecords.Add(record);
            }
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Immutable diagnostic content cannot be extended after persistence.", error.Message);
            db.ChangeTracker.Clear();
            Assert.Equal(1, kind == "lab"
                ? await db.LabCatalogImportRecords.CountAsync(x => x.ImportBatchId == id, TestContext.Current.CancellationToken)
                : await db.RadiologyCatalogImportRecords.CountAsync(x => x.ImportBatchId == id, TestContext.Current.CancellationToken));
        });
    }

    [Theory]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    public async Task Reviewed_imports_are_atomic_idempotent_preserve_local_fields_and_detect_stale_previews(string kind, bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); using var manager = await app.ClientAsync("medical-manager"); using var doctor = await app.ClientAsync("doctor");
        var staged = await PreviewAsync(manager, kind);
        Assert.Equal("Staged", staged.GetProperty("status").GetString());
        Assert.Equal(0, (await GetAsync(doctor, Url(kind, "-catalog"))).GetProperty("totalCount").GetInt64());
        var changes = await GetAsync(manager, $"/api/v1/admin/{kind}-catalog/imports/{staged.GetProperty("batchId").GetGuid()}/changes");
        Assert.Equal("New", changes.GetProperty("items")[0].GetProperty("disposition").GetString());
        var key = Guid.NewGuid().ToString("N"); var body = new { rowVersion = staged.GetProperty("rowVersion").GetString() };
        await OkAsync(await PostAsync(manager, ApplyUrl(kind, staged), body, key));
        await OkAsync(await PostAsync(manager, ApplyUrl(kind, staged), body, key));
        var catalog = (await GetAsync(manager, $"/api/v1/admin/{kind}-catalog")).GetProperty("items")[0]; var id = catalog.GetProperty("catalogId").GetGuid();
        Assert.Equal("Loinc", catalog.GetProperty("source").GetString()); Assert.Equal("اسم رسمي", catalog.GetProperty("officialNameAr").GetString());
        if (kind == "radiology") Assert.Contains("Rad.Modality.Modality Type", catalog.GetProperty("attributesJson").GetString(), StringComparison.Ordinal);
        var detailUrl = $"/api/v1/admin/{kind}-catalog/{id}";
        catalog = await OkAsync(await manager.PutAsJsonAsync(detailUrl, new { rowVersion = catalog.GetProperty("rowVersion").GetString(), data = new { displayNameEn = "Local label", displayNameAr = "اسم محلي", internalNote = "Reference review" } }, TestContext.Current.CancellationToken));
        catalog = await OkAsync(await PostAsync(manager, detailUrl + "/deactivate", new { rowVersion = catalog.GetProperty("rowVersion").GetString(), reason = "Local policy" }));
        var next = await PreviewAsync(manager, kind, Package(arabic: "اسم جديد", name: "Updated source"));
        await OkAsync(await PostAsync(manager, ApplyUrl(kind, next), new { rowVersion = next.GetProperty("rowVersion").GetString() }));
        catalog = await GetAsync(manager, detailUrl);
        Assert.Equal("Inactive", catalog.GetProperty("status").GetString()); Assert.Equal("Local label", catalog.GetProperty("nameEn").GetString());
        Assert.Equal("Updated source", catalog.GetProperty("officialNameEn").GetString()); Assert.Equal("اسم جديد", catalog.GetProperty("officialNameAr").GetString());
        var first = await PreviewAsync(manager, kind); var stale = await PreviewAsync(manager, kind);
        await OkAsync(await PostAsync(manager, ApplyUrl(kind, first), new { rowVersion = first.GetProperty("rowVersion").GetString() }));
        using var conflict = await PostAsync(manager, ApplyUrl(kind, stale), new { rowVersion = stale.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); Assert.Contains("DiagnosticCatalogImport.ConcurrencyConflict", await conflict.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("Staged", (await GetAsync(manager, $"/api/v1/admin/{kind}-catalog/imports/{stale.GetProperty("batchId").GetGuid()}")).GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("lab")]
    [InlineData("radiology")]
    public async Task Merge_chains_are_flattened_and_possible_import_conflicts_require_explicit_skip(string kind)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var manager = await app.ClientAsync("medical-manager"); using var doctor = await app.ClientAsync("doctor");
        var url = $"/api/v1/admin/{kind}-catalog";
        var a = await OkAsync(await PostAsync(manager, url, new { displayNameEn = "Old label" }));
        var b = await OkAsync(await PostAsync(manager, url, new { displayNameEn = "Middle label" }));
        var c = await OkAsync(await PostAsync(manager, url, new { displayNameEn = "Imported test" }));
        await OkAsync(await PostAsync(manager, url + $"/{a.GetProperty("catalogId").GetGuid()}/merge", new { rowVersion = a.GetProperty("rowVersion").GetString(), targetCatalogId = b.GetProperty("catalogId").GetGuid(), reason = "Duplicate" }));
        await OkAsync(await PostAsync(manager, url + $"/{b.GetProperty("catalogId").GetGuid()}/merge", new { rowVersion = b.GetProperty("rowVersion").GetString(), targetCatalogId = c.GetProperty("catalogId").GetGuid(), reason = "Canonical" }));
        a = await GetAsync(manager, url + $"/{a.GetProperty("catalogId").GetGuid()}");
        Assert.Equal(c.GetProperty("catalogId").GetGuid(), a.GetProperty("mergedIntoId").GetGuid());
        Assert.Equal(c.GetProperty("catalogId").GetGuid(), (await GetAsync(doctor, Url(kind, "-catalog?search=Old%20label"))).GetProperty("items")[0].GetProperty("catalogId").GetGuid());
        var batch = await PreviewAsync(manager, kind);
        using (var failed = await PostAsync(manager, ApplyUrl(kind, batch), new { rowVersion = batch.GetProperty("rowVersion").GetString() })) Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        await OkAsync(await PostAsync(manager, ApplyUrl(kind, batch), new { rowVersion = batch.GetProperty("rowVersion").GetString(), skipPossibleConflicts = true }));
        Assert.Equal(3, (await GetAsync(manager, url)).GetProperty("totalCount").GetInt64());
        var retained = await GetAsync(manager, $"{url}/imports/{batch.GetProperty("batchId").GetGuid()}/changes?disposition=PossibleConflict"); Assert.Equal(1, retained.GetProperty("totalCount").GetInt64());
    }

    [Fact]
    public async Task Only_root_governs_medical_managers_and_first_login_and_role_isolation_are_enforced()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var root = await app.ClientAsync("root"); using var admin = await app.ClientAsync("admin");
        var body = new { userName = "medical2", email = "medical2@example.test", initialPassword = Phase14MedicationApiTests.Password, confirmPassword = Phase14MedicationApiTests.Password };
        foreach (var actor in new[] { "admin", "catalog-manager", "medical-manager", "doctor", "patient", "reception" })
        { using var denied = await app.ClientAsync(actor); using var response = await PostAsync(denied, "/api/v1/admin/medical-catalog-managers", body); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
        var created = await OkAsync(await PostAsync(root, "/api/v1/admin/medical-catalog-managers", body)); var id = created.GetProperty("id").GetGuid(); Assert.True(created.GetProperty("isFirstLogin").GetBoolean());
        using var manager = await app.ClientAsync("medical2");
        using (var response = await manager.GetAsync("/api/v1/admin/lab-catalog", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await PostAsync(manager, "/api/v1/auth/change-password", new { currentPassword = Phase14MedicationApiTests.Password, newPassword = "ChangedMedicalPass123!", confirmPassword = "ChangedMedicalPass123!" })) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var response = await admin.PutAsJsonAsync($"/api/v1/admin/medical-catalog-managers/{id}", new { email = "unsafe@example.test" }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await root.PutAsJsonAsync($"/api/v1/admin/roles/{SystemRoleIds.MedicalCatalogManager}/permissions", new { permissionIds = new[] { SystemPermissionIds.For(PermissionNames.LabRequestsViewOwn) } }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await OkAsync(await PostAsync(root, $"/api/v1/admin/medical-catalog-managers/{id}/deactivate", new { }));
        await OkAsync(await PostAsync(root, $"/api/v1/admin/medical-catalog-managers/{id}/activate", new { }));
        await app.WithDbAsync(async db =>
        {
            var mappings = await (from rp in db.RolePermissions join p in db.Permissions on rp.PermissionId equals p.Id where rp.RoleId == SystemRoleIds.MedicalCatalogManager select p.Name).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(PermissionNames.MedicalCatalogManagerDefaults.Order(), mappings.Order());
        });
    }
}
