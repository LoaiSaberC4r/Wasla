using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientsContractTests
{
    [Fact]
    public async Task Eight_get_contracts_have_typed_private_openapi_and_seed_only_doctor_defaults_idempotently()
    {
        await using var f = await Phase13ApiFixture.CreateAsync(); using var client = f.CreateClient();
        using var swagger = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var document = JsonNode.Parse(await swagger.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var routes = new[] { "/api/v1/doctors/me/patients", "/api/v1/doctors/me/patients/{patientId}",
            "/api/v1/doctors/me/patients/{patientId}/attendances", "/api/v1/doctors/me/patients/{patientId}/diagnoses",
            "/api/v1/doctors/me/patients/{patientId}/prescriptions", "/api/v1/doctors/me/patients/{patientId}/appointments",
            "/api/v1/doctors/me/patients/{patientId}/follow-ups", "/api/v1/doctors/me/prescriptions/{prescriptionId}/versions/{versionNumber}/pdf" };
        foreach (var route in routes)
        {
            var operation = document["paths"]![route]!["get"]!;
            Assert.NotNull(operation["responses"]!["200"]!["content"]);
            Assert.NotNull(operation["responses"]!["200"]!["headers"]!["Cache-Control"]);
            Assert.NotNull(operation["responses"]!["200"]!["headers"]!["X-Content-Type-Options"]);
            Assert.NotNull(operation["description"]);
        }
        var pdf = document["paths"]![routes[^1]]!["get"]!["responses"]!["200"]!["content"]!;
        Assert.Equal("binary", pdf["application/pdf"]!["schema"]!["format"]!.GetValue<string>());
        foreach (var schema in new[] { "DoctorPatientsPageResponse", "DoctorPatientListItem", "DoctorMyPatientOverviewResponse", "DoctorPatientAttendance", "DoctorPatientDiagnosis", "DoctorPatientPrescription", "DoctorPatientAppointment", "DoctorPatientFollowUp" })
            Assert.NotNull(document["components"]!["schemas"]![schema]);
        using var anonymous = await client.GetAsync(routes[0], TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.True(anonymous.Headers.CacheControl?.NoStore);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var before = await db.RolePermissions.CountAsync(TestContext.Current.CancellationToken);
        var seeder = typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
        await (Task)seeder.GetMethod("SeedAsync")!.Invoke(scope.ServiceProvider.GetRequiredService(seeder), [TestContext.Current.CancellationToken])!;
        Assert.Equal(before, await db.RolePermissions.CountAsync(TestContext.Current.CancellationToken));
        foreach (var permission in PermissionNames.DoctorPatientDefaults)
        {
            var id = SystemPermissionIds.For(permission);
            var grants = await db.RolePermissions.Where(r => r.PermissionId == id).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(SystemRoleIds.Doctor, Assert.Single(grants).RoleId);
        }
        var output = Environment.GetEnvironmentVariable("WASLA_CONTRACT_OUTPUT_DIRECTORY");
        if (output is not null) { Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, "doctor-patients-openapi.json"), document.ToJsonString(), TestContext.Current.CancellationToken); }
    }
}
