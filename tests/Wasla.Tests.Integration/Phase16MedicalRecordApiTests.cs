using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16MedicalRecordApiTests
{
    private const string PatientRecord = "/api/v1/patients/me/medical-record";
    private const string Inbox = "/api/v1/doctors/me/medical-record-access-requests";
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P16_07_through_23_hidden_request_approval_revoke_and_unique_ticket_scope_work_end_to_end(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid();
        await app.CompleteAsync(owner, source); await HideAsync(app, owner, id);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        var timeline = await GetAsync(doctor, url + "/timeline"); var hidden = timeline.GetProperty("items")[0];
        Assert.Equal("HiddenEncounterPlaceholder", hidden.GetProperty("type").GetString());
        foreach (var forbidden in new[] { "occurredAtUtc", "sourceId", "encounterId", "title", "doctor", "practice", "attachments", "patientId" }) Assert.False(hidden.TryGetProperty(forbidden, out _));
        var beforeEntry = hidden.GetProperty("entryId").GetString();
        Assert.Equal(beforeEntry, (await GetAsync(doctor, url + "/timeline")).GetProperty("items")[0].GetProperty("entryId").GetString());
        using (var denied = await doctor.GetAsync(url + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var body = new { hiddenEncounterToken = hidden.GetProperty("hiddenEncounterToken").GetString(), reason = " Clinical justification " }; var key = Guid.NewGuid().ToString("N");
        var requested = await OkAsync(await PostAsync(doctor, url + "/access-requests", body, key)); var requestId = requested.GetProperty("requestId").GetGuid();
        var replay = await OkAsync(await PostAsync(doctor, url + "/access-requests", body, key)); Assert.Equal(requestId, replay.GetProperty("requestId").GetGuid());
        Assert.False(requested.TryGetProperty("ownerDetails", out _)); Assert.False(requested.TryGetProperty("grantedEncounterId", out _));
        using (var duplicate = await PostAsync(doctor, url + "/access-requests", body)) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var badge = await GetAsync(owner, Inbox + "/inbox/summary"); Assert.Equal(1, badge.GetProperty("actionableCount").GetInt32());
        Assert.Single((await GetAsync(owner, Inbox + "/inbox")).GetProperty("items").EnumerateArray());
        var review = await GetAsync(owner, Inbox + $"/{requestId}"); Assert.True(review.GetProperty("capabilities").GetProperty("canApprove").GetBoolean());
        var approveKey = Guid.NewGuid().ToString("N"); var approveBody = new { rowVersion = review.GetProperty("rowVersion").GetString() };
        var approved = await OkAsync(await PostAsync(owner, Inbox + $"/{requestId}/approve", approveBody, approveKey));
        await OkAsync(await PostAsync(owner, Inbox + $"/{requestId}/approve", approveBody, approveKey));
        var status = await GetAsync(doctor, url + $"/access-requests/{requestId}"); Assert.Equal(id, status.GetProperty("grantedEncounterId").GetGuid());
        var details = await GetAsync(doctor, url + $"/encounters/{id}"); Assert.DoesNotContain("clinicalNotes", details.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await OkAsync(await PostAsync(owner, Inbox + $"/{requestId}/revoke", new { rowVersion = approved.GetProperty("rowVersion").GetString() }));
        using (var denied = await doctor.GetAsync(url + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var duplicate = await PostAsync(doctor, url + "/access-requests", body)) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Revoked", (await GetAsync(doctor, url + $"/access-requests/{requestId}")).GetProperty("effectiveStatus").GetString());
        Assert.Equal(3, (await GetAsync(owner, Inbox + $"/{requestId}/history")).GetArrayLength());
    }
    [Fact]
    public async Task Defaults_roles_stale_permissions_waiting_ticket_and_guardian_gates_fail_closed()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("other-doctor");
        var treating = await TreatingAsync(app, false); var url = TreatingUrl(app, treating.Ticket);
        using (var disabled = await doctor.GetAsync(url + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        EnableSharing(app);
        using (var waiting = await doctor.GetAsync(url + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, waiting.StatusCode);
        foreach (var actor in new[] { "reception", "admin", "root", "medical-manager", "catalog-manager" })
        {
            using var client = await app.ClientAsync(actor); using var response = await client.GetAsync(PatientRecord + "/overview", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = true;
        foreach (var route in new[] { "/api/v1/families/mine/medical-record-dependents", $"/api/v1/families/mine/dependents/{app.PatientId}/medical-record/overview" })
        { using var denied = await patient.GetAsync(route, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        await app.WithDbAsync(async db =>
        {
            var permission = SystemPermissionIds.For(PermissionNames.MedicalRecordsViewOwn);
            db.RolePermissions.RemoveRange(await db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Patient && r.PermissionId == permission).ToArrayAsync(TestContext.Current.CancellationToken));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using (var denied = await patient.GetAsync(PatientRecord + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
