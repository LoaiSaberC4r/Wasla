using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18PatientDashboardApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Dashboard_composes_self_handlers_and_disabled_guardian_access_is_unavailable(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var patient = await app.ClientAsync("patient");
        var capabilities = await GetAsync(patient, "/api/v1/patients/me/dashboard/capabilities");
        Assert.Equal(app.PatientId, capabilities.GetProperty("patientId").GetGuid());
        Assert.False(capabilities.GetProperty("guardianSharingEnabled").GetBoolean());
        var family = capabilities.GetProperty("capabilities").EnumerateArray().Single(c => c.GetProperty("code").GetString() == "FamilyMedicalEligibility");
        Assert.False(family.GetProperty("available").GetBoolean()); Assert.Equal(JsonValueKind.Null, family.GetProperty("route").ValueKind);
        var overview = await GetAsync(patient, "/api/v1/patients/me/dashboard/overview?patientId=" + app.OtherPatientId);
        Assert.Equal(app.PatientId, overview.GetProperty("patientId").GetGuid());
        Assert.Empty(overview.GetProperty("activeTickets").EnumerateArray());
        Assert.NotEqual(JsonValueKind.Null, overview.GetProperty("medicalSummary").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, overview.GetProperty("recentFinancialTransactions").ValueKind);
        Assert.Equal(JsonValueKind.Null, overview.GetProperty("family").ValueKind);
        using var doctor = await app.ClientAsync("doctor");
        using var denied = await doctor.GetAsync("/api/v1/patients/me/dashboard/overview", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await app.WithDbAsync(async db =>
        {
            await db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Patient && p.PermissionId == SystemPermissionIds.For(PermissionNames.MedicalRecordsViewOwn))
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });
        var revoked = await GetAsync(patient, "/api/v1/patients/me/dashboard/overview");
        Assert.Equal(JsonValueKind.Null, revoked.GetProperty("medicalSummary").ValueKind);
        Assert.Equal(JsonValueKind.Null, revoked.GetProperty("recentPermittedTimeline").ValueKind);
    }
}
