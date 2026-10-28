using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase14MedicationApiTests;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientPrescriptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Issued_history_versions_pdf_and_closed_practice_export_preserve_write_guards(bool sql)
    {
        await using var f = await Phase14MedicationApiTests.CreateAsync(sql); using var manager = await f.ClientAsync("catalog-manager");
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception"); using var other = await f.ClientAsync("other-doctor");
        var drug = await CatalogDrugAsync(manager); var issued = await FinalizedAsync(f, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid());
        var id = issued.Prescription.GetProperty("prescriptionId").GetGuid(); var root = $"/api/v1/doctors/me/prescriptions/{id}";
        var list = await GetAsync(doctor, $"/api/v1/doctors/me/patients/{f.PatientId}/prescriptions");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32()); Assert.True(list.GetProperty("items")[0].GetProperty("canExport").GetBoolean());
        var path = Environment.GetEnvironmentVariable("WASLA_CHROMIUM_PATH");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(path), "WASLA_CHROMIUM_PATH is required for actual export.");
        f.Services.GetRequiredService<IOptions<Phase18Options>>().Value.ChromiumExecutablePath = path;
        using (var pdf = await doctor.GetAsync(root + "/versions/1/pdf?disposition=attachment", TestContext.Current.CancellationToken))
        {
            Assert.True(pdf.StatusCode == HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", pdf.Content.Headers.ContentDisposition?.DispositionType);
            Assert.True(pdf.Headers.CacheControl?.NoStore);
            Assert.True((await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).AsSpan().StartsWith("%PDF"u8));
        }
        using (var denied = await other.GetAsync(root + "/versions/1/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var invalid = await doctor.GetAsync(root + "/versions/1/pdf?disposition=evil", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        using (var missing = await doctor.GetAsync(root + "/versions/99/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var correction = await Phase14MedicationApiTests.AddAsync(doctor, root + "/correction-draft", new { reason = "Synthetic correction", rowVersion = issued.Prescription.GetProperty("rowVersion").GetString() });
        using (var draft = await doctor.GetAsync(root + "/versions/2/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        var corrected = await Phase14MedicationApiTests.AddAsync(doctor, root + "/correction-draft/finalize", new { rowVersion = correction.GetProperty("rowVersion").GetString() });
        var versions = await GetAsync(doctor, root + "/versions"); Assert.Equal("Superseded", versions[0].GetProperty("status").GetString());
        using (var superseded = await doctor.GetAsync(root + "/versions/1/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, superseded.StatusCode);
        await Phase14MedicationApiTests.AddAsync(doctor, root + "/void", new { reason = "Synthetic void", rowVersion = corrected.GetProperty("rowVersion").GetString() });
        using (var voided = await doctor.GetAsync(root + "/versions/2/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        list = await GetAsync(doctor, $"/api/v1/doctors/me/patients/{f.PatientId}/prescriptions"); Assert.Equal("Voided", list.GetProperty("items")[0].GetProperty("currentStatus").GetString());
        await f.WithDbAsync(async db => { (await db.DoctorPractices.SingleAsync(p => p.Id == f.PracticeId)).Deactivate(f.DoctorUserId); await db.SaveChangesAsync(); });
        var state = await GetAsync(doctor, root); Assert.All(state.GetProperty("capabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        await GetAsync(doctor, root + "/versions"); await GetAsync(doctor, root + "/versions/1");
        using (var pdf = await doctor.GetAsync(root + "/versions/1/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        foreach (var operation in new[] { "/void", "/correction-draft" })
        {
            using var denied = await PostAsync(doctor, root + operation, new { reason = "Forbidden", rowVersion = state.GetProperty("rowVersion").GetString() });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await DoctorPatientsApiTests.RemoveGrants(f, PermissionNames.PrescriptionsExportOwn);
        using (var revoked = await doctor.GetAsync(root + "/versions/1/pdf", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        await f.WithDbAsync(async db => Assert.Equal(4, await db.ArchiveAuditEvents.CountAsync(a => a.Action == "PrescriptionVersionPdfExport")));
    }
}
