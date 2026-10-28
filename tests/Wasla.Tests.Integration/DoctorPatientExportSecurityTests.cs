using System.Net;
using System.Text;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientExportSecurityTests
{
    [Fact]
    public async Task Practice_closure_during_render_rechecks_untracked_scope_before_release()
    {
        await using var f = await Phase14MedicationApiTests.CreateAsync(false); using var manager = await f.ClientAsync("catalog-manager");
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var drug = await Phase14MedicationApiTests.CatalogDrugAsync(manager);
        var issued = await Phase14MedicationApiTests.FinalizedAsync(f, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid());
        await DoctorPatientsApiTests.RemoveGrants(f, PermissionNames.DoctorPatientsViewHistoricalOwn);
        await using var host = f.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPrescriptionPdfRenderer>(); services.AddSingleton<IPrescriptionPdfRenderer>(new TestRenderer(() => f.WithDbAsync(async db =>
            {
                (await db.DoctorPractices.SingleAsync(p => p.Id == f.PracticeId)).Deactivate(f.DoctorUserId); await db.SaveChangesAsync();
            })));
        }));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
        var id = issued.Prescription.GetProperty("prescriptionId").GetGuid();
        using var response = await client.GetAsync($"/api/v1/doctors/me/prescriptions/{id}/versions/1/pdf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("%PDF", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Revocation_during_render_and_required_audit_failure_never_release_pdf_bytes()
    {
        await using var f = await Phase14MedicationApiTests.CreateAsync(false); using var manager = await f.ClientAsync("catalog-manager");
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var drug = await Phase14MedicationApiTests.CatalogDrugAsync(manager);
        var issued = await Phase14MedicationApiTests.FinalizedAsync(f, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid());
        var id = issued.Prescription.GetProperty("prescriptionId").GetGuid(); var url = $"/api/v1/doctors/me/prescriptions/{id}/versions/1/pdf";
        await using (var revokedHost = f.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPrescriptionPdfRenderer>(); services.AddSingleton<IPrescriptionPdfRenderer>(new TestRenderer(() => DoctorPatientsApiTests.RemoveGrants(f, PermissionNames.PrescriptionsExportOwn)));
        })))
        {
            using var client = revokedHost.CreateClient(); client.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
            using var denied = await client.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain("%PDF", await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        await f.WithDbAsync(async db =>
        {
            Assert.Equal(0, await db.ArchiveAuditEvents.CountAsync(a => a.Action == "PrescriptionVersionPdfExport"));
            db.RolePermissions.Add(new(Guid.NewGuid(), SystemRoleIds.Doctor, SystemPermissionIds.For(PermissionNames.PrescriptionsExportOwn))); await db.SaveChangesAsync();
        });
        await using var auditHost = f.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var implementation = services.Last(s => s.ServiceType == typeof(IDoctorPatientReadService)).ImplementationType!;
            services.RemoveAll<IDoctorPatientReadService>();
            services.AddScoped<IDoctorPatientReadService>(provider => new FailedAuditReader((IDoctorPatientReadService)ActivatorUtilities.CreateInstance(provider, implementation)));
            services.RemoveAll<IPrescriptionPdfRenderer>(); services.AddSingleton<IPrescriptionPdfRenderer>(new TestRenderer(() => Task.CompletedTask));
        }));
        using var auditClient = auditHost.CreateClient(); auditClient.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
        using var auditDenied = await auditClient.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.InRange((int)auditDenied.StatusCode, 500, 599);
        Assert.DoesNotContain("%PDF", await auditDenied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    private sealed class TestRenderer(Func<Task> callback) : IPrescriptionPdfRenderer
    {
        public async Task<Result<byte[]>> RenderAsync(PrescriptionPdfDocument document, CancellationToken ct)
        {
            await callback(); return Result<byte[]>.Ok(Encoding.ASCII.GetBytes("%PDF-synthetic-security-probe"));
        }
    }
    private sealed class FailedAuditReader(IDoctorPatientReadService inner) : IDoctorPatientReadService
    {
        public Task<DoctorPatientPracticeScope?> PracticeScopeAsync(Guid practiceId, CancellationToken ct) => inner.PracticeScopeAsync(practiceId, ct);
        public Task<bool> HasAttendanceAsync(Guid doctorId, Guid patientId, CancellationToken ct) => inner.HasAttendanceAsync(doctorId, patientId, ct);
        public Task<DoctorPatientsPageResponse> DirectoryAsync(DoctorPatientActor actor, DoctorPatientsQuery query, CancellationToken ct) => inner.DirectoryAsync(actor, query, ct);
        public Task<DoctorMyPatientOverviewResponse?> OverviewAsync(DoctorPatientActor actor, Guid patientId, CancellationToken ct) => inner.OverviewAsync(actor, patientId, ct);
        public Task<object> HistoryAsync(DoctorPatientActor actor, DoctorPatientHistoryQuery query, CancellationToken ct) => inner.HistoryAsync(actor, query, ct);
        public Task<PrescriptionPdfDocument?> PdfDocumentAsync(Guid doctorId, Guid prescriptionId, int version, CancellationToken ct) => inner.PdfDocumentAsync(doctorId, prescriptionId, version, ct);
        public Task AuditAsync(DoctorPatientActor actor, string action, Guid targetId, CancellationToken ct) => throw new InvalidOperationException("Synthetic required-audit failure");
    }
}
