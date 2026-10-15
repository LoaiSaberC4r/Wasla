using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16PolicyApiTests
{
    private const string Inbox = "/api/v1/doctors/me/medical-record-access-requests";
    [Theory]
    [InlineData(TicketSource.WalkIn)]
    [InlineData(TicketSource.Reservation)]
    public async Task Live_ticket_origins_share_only_completed_current_outputs_and_exact_patient_link(TicketSource origin)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid();
        var treating = await TreatingAsync(app, source: origin); var url = TreatingUrl(app, treating.Ticket);
        Assert.Empty((await GetAsync(doctor, url + "/timeline")).GetProperty("items").EnumerateArray());
        using (var denied = await doctor.GetAsync(url + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await app.CompleteAsync(owner, source);
        Assert.Single((await GetAsync(doctor, url + "/timeline")).GetProperty("items").EnumerateArray());
        await app.WithDbAsync(async db =>
        {
            var e = await db.MedicalEncounters.SingleAsync(e => e.Id == treating.Encounter, TestContext.Current.CancellationToken);
            db.Entry(e).Property(e => e.PatientId).CurrentValue = app.OtherPatientId; await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using (var denied = await doctor.GetAsync(url + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Fact]
    public async Task Hidden_tokens_are_actor_ticket_purpose_time_and_revision_bound_and_rejection_is_terminal()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid(); await app.CompleteAsync(owner, source); await HideAsync(app, owner, id);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        var tokens = app.Services.GetRequiredService<IMedicalRecordTokens>();
        var valid = new HiddenEncounterToken(treating.User, treating.Doctor, treating.Ticket, id, 1, app.Clock.UtcNow);
        var invalid = new[] { "plain-ids:" + id, tokens.Protect("WrongPurpose", valid), tokens.Protect("HiddenEncounter", valid with { ActorId = Guid.NewGuid() }),
            tokens.Protect("HiddenEncounter", valid with { TicketId = Guid.NewGuid() }), tokens.Protect("HiddenEncounter", valid with { Revision = 0 }),
            tokens.Protect("HiddenEncounter", valid with { IssuedAtUtc = app.Clock.UtcNow.AddMinutes(-16) }) };
        foreach (var token in invalid)
        {
            using var denied = await PostAsync(doctor, url + "/access-requests", new { hiddenEncounterToken = token, reason = "Reason" }); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        var body = new { hiddenEncounterToken = tokens.Protect("HiddenEncounter", valid), reason = "Reason" };
        var requested = await OkAsync(await PostAsync(doctor, url + "/access-requests", body)); var rid = requested.GetProperty("requestId").GetGuid();
        var rejected = await OkAsync(await PostAsync(owner, Inbox + $"/{rid}/reject", new { rowVersion = requested.GetProperty("rowVersion").GetString() }));
        Assert.Equal("Rejected", rejected.GetProperty("effectiveStatus").GetString());
        using (var duplicate = await PostAsync(doctor, url + "/access-requests", body)) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using (var approve = await PostAsync(owner, Inbox + $"/{rid}/approve", new { rowVersion = rejected.GetProperty("rowVersion").GetString() })) Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        using (var denied = await doctor.GetAsync(url + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Fact]
    public async Task Visibility_revision_and_permission_revocation_invalidate_current_grants_and_cursor_without_resurrection()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid(); var initial = await GetAsync(owner, app.EncounterUrl(id));
        await OkAsync(await PostAsync(owner, app.EncounterUrl(id) + "/diagnoses", new { type = "Primary", displayText = "Safe diagnosis", encounterRowVersion = initial.GetProperty("rowVersion").GetString() }));
        await app.CompleteAsync(owner, source); await HideAsync(app, owner, id);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket); var hidden = (await GetAsync(doctor, url + "/timeline")).GetProperty("items")[0];
        var requested = await OkAsync(await PostAsync(doctor, url + "/access-requests", new { hiddenEncounterToken = hidden.GetProperty("hiddenEncounterToken").GetString(), reason = "Reason" })); var rid = requested.GetProperty("requestId").GetGuid();
        await OkAsync(await PostAsync(owner, Inbox + $"/{rid}/approve", new { rowVersion = requested.GetProperty("rowVersion").GetString() }));
        var paged = await GetAsync(doctor, url + "/timeline?pageSize=1"); var cursor = paged.GetProperty("nextCursor").GetString();
        var e = await GetAsync(owner, app.EncounterUrl(id));
        await OkAsync(await owner.PatchAsJsonAsync(app.EncounterUrl(id) + "/visibility", new { visibility = "Visible", rowVersion = e.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken)); await HideAsync(app, owner, id);
        using (var denied = await doctor.GetAsync(url + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal("Expired", (await GetAsync(doctor, url + $"/access-requests/{rid}")).GetProperty("effectiveStatus").GetString());
        using (var stale = await doctor.GetAsync(url + "/timeline?pageSize=1&cursor=" + Uri.EscapeDataString(cursor!), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var permission = SystemPermissionIds.For(PermissionNames.HiddenEncounterAccessRequestsCreate);
            db.RolePermissions.RemoveRange(await db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Doctor && r.PermissionId == permission).ToArrayAsync(TestContext.Current.CancellationToken)); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Assert.False((await GetAsync(doctor, url + "/overview")).GetProperty("capabilities").GetProperty("canRequestHiddenAccess").GetBoolean());
        await app.WithDbAsync(async db =>
        {
            var d = await db.Doctors.SingleAsync(d => d.Id == treating.Doctor, TestContext.Current.CancellationToken); d.Suspend("Suspended", app.DoctorUserId, app.Clock.UtcNow); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using (var denied = await doctor.GetAsync(url + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P16_04_current_prescription_and_safe_diagnosis_never_share_internal_amendment_notes_or_drafts(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception"); using var manager = await app.ClientAsync("catalog-manager"); using var patient = await app.ClientAsync("patient");
        var drug = await Phase14MedicationApiTests.CatalogDrugAsync(manager);
        var source = await Phase14MedicationApiTests.FinalizedAsync(app, owner, reception, drug.GetProperty("drugCatalogId").GetGuid()); var id = source.EncounterId; var prescription = source.Prescription.GetProperty("prescriptionId").GetGuid();
        var e = await GetAsync(owner, app.EncounterUrl(id));
        await OkAsync(await PostAsync(owner, app.EncounterUrl(id) + "/amendments", new { reason = "Internal amendment reason", encounterRowVersion = e.GetProperty("rowVersion").GetString(), changes = new[] { new { target = "Diagnosis", changeType = "Added", type = "Primary", displayText = "Safe current diagnosis", notes = "Internal diagnosis note" } } }));
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        var details = await GetAsync(doctor, url + $"/encounters/{id}"); Assert.DoesNotContain("Internal", details.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Safe current diagnosis", details.GetProperty("diagnoses")[0].GetProperty("displayText").GetString());
        var legacyPatient = await GetAsync(patient, $"/api/v1/encounters/mine/{id}");
        Assert.DoesNotContain("Internal", legacyPatient.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var diagnosis = legacyPatient.GetProperty("currentActiveDiagnoses")[0];
        Assert.Equal("Safe current diagnosis", diagnosis.GetProperty("displayText").GetString());
        Assert.True(!diagnosis.TryGetProperty("notes", out var note) || note.ValueKind == System.Text.Json.JsonValueKind.Null);
        var ownerDetails = await GetAsync(owner, app.EncounterUrl(id));
        Assert.Equal("Internal diagnosis note", ownerDetails.GetProperty("diagnoses")[0].GetProperty("notes").GetString());
        foreach (var route in new[] { url + $"/items/prescription/{prescription}", $"/api/v1/patients/me/medical-record/items/prescription/{prescription}" })
        {
            var p = await GetAsync(route.StartsWith(url, StringComparison.Ordinal) ? doctor : patient, route);
            Assert.Equal("Current", p.GetProperty("status").GetString()); Assert.Single(p.GetProperty("safeContent").GetProperty("items").EnumerateArray()); Assert.DoesNotContain("draft", p.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
