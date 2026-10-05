using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase14MedicationApiTests;

namespace Wasla.Tests.Integration;

public sealed class Phase14LifecycleApiTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task Empty_initial_draft_is_removed_and_visit_completes_without_prescription(bool sql)
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid();
        var url = app.EncounterUrl(e) + "/prescription/items"; var key = Guid.NewGuid().ToString("N"); var body = new { drugCatalogId = drug.GetProperty("drugCatalogId").GetGuid() }; var p = await AddAsync(doctor, url, body, key);
        using var removed = await doctor.DeleteAsync(url + "/" + p.GetProperty("draft").GetProperty("items")[0].GetProperty("itemId").GetGuid() + "?rowVersion=" + Uri.EscapeDataString(p.GetProperty("rowVersion").GetString()!), TestContext.Current.CancellationToken);
        Assert.True(removed.StatusCode == HttpStatusCode.OK, await removed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var state = await JsonAsync(removed);
        Assert.Equal(JsonValueKind.Null, state.GetProperty("prescriptionId").ValueKind); Assert.Equal(JsonValueKind.Null, state.GetProperty("rowVersion").ValueKind);
        var replay = await AddAsync(doctor, url, body, key); Assert.Equal(JsonValueKind.Null, replay.GetProperty("prescriptionId").ValueKind); Assert.Equal(JsonValueKind.Null, replay.GetProperty("rowVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await GetAsync(doctor, app.EncounterUrl(e))).GetProperty("prescription").ValueKind);
        var completed = await app.CompleteAsync(doctor, ticket); Assert.Equal(JsonValueKind.Null, completed.Ticket.GetProperty("prescriptionId").ValueKind);
        await app.WithDbAsync(async db => { Assert.Empty(await db.Prescriptions.ToArrayAsync(TestContext.Current.CancellationToken)); Assert.Single(await db.PrescriptionAuditEvents.ToArrayAsync(TestContext.Current.CancellationToken)); });
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task Request_more_info_update_resubmits_and_duplicate_rejection_is_private_and_linked(bool sql)
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var otherDoctor = await app.ClientAsync("other-doctor");
        var drug = await CatalogDrugAsync(manager);
        using var created = await PostAsync(doctor, "/api/v1/doctors/me/drug-catalog-requests", new { medicationName = "new medicine" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var request = await JsonAsync(created); var id = request.GetProperty("requestId").GetGuid(); var url = $"/api/v1/admin/drug-catalog-requests/{id}";
        using (var noReason = await PostAsync(manager, url + "/request-more-info", new { rowVersion = request.GetProperty("rowVersion").GetString() })) Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
        request = await AddAsync(manager, url + "/request-more-info", new { rowVersion = request.GetProperty("rowVersion").GetString(), reason = "manufacturer?" }); Assert.Equal("NeedsMoreInfo", request.GetProperty("status").GetString());
        using var update = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/drug-catalog-requests/{id}", new { rowVersion = request.GetProperty("rowVersion").GetString(), data = new { medicationName = "new medicine", manufacturer = "Maker" } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode); request = await JsonAsync(update); Assert.Equal("Pending", request.GetProperty("status").GetString());
        request = await AddAsync(manager, url + "/reject", new { rowVersion = request.GetProperty("rowVersion").GetString(), duplicateOfDrugCatalogId = drug.GetProperty("drugCatalogId").GetGuid() });
        Assert.Equal("Rejected", request.GetProperty("status").GetString()); Assert.Equal("duplicate", request.GetProperty("currentReviewReason").GetString());
        var own = await GetAsync(doctor, $"/api/v1/doctors/me/drug-catalog-requests/{id}"); Assert.Equal(drug.GetProperty("drugCatalogId").GetGuid(), own.GetProperty("duplicateOfDrugCatalogId").GetGuid());
        Assert.Equal(drug.GetProperty("commercialNameEn").GetString(), own.GetProperty("duplicateDrug").GetProperty("commercialNameEn").GetString());
        Assert.False(request.TryGetProperty("patientId", out _)); Assert.False(request.TryGetProperty("encounterId", out _));
        using var other = await otherDoctor.GetAsync($"/api/v1/doctors/me/drug-catalog-requests/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var immutable = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/drug-catalog-requests/{id}", new { rowVersion = own.GetProperty("rowVersion").GetString(), data = new { medicationName = "edited" } }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task Deactivated_search_selection_returns_conflict_and_manual_merge_preserves_prescription_snapshot(bool sql)
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var id = drug.GetProperty("drugCatalogId").GetGuid();
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        drug = (await AddAsync(manager, $"/api/v1/admin/drug-catalog/{id}/deactivate", new { rowVersion = drug.GetProperty("rowVersion").GetString(), reason = "source review" })).GetProperty("drug").Clone();
        using (var staleSelection = await PostAsync(doctor, app.EncounterUrl(encounterId) + "/prescription/items", new { drugCatalogId = id }))
        { Assert.Equal(HttpStatusCode.Conflict, staleSelection.StatusCode); Assert.Contains("DrugCatalog.NotActive", await staleSelection.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal); }
        Assert.Equal(0, (await GetAsync(doctor, "/api/v1/doctors/me/drug-catalog?search=BRUFEN")).GetProperty("totalCount").GetInt64());
        drug = (await AddAsync(manager, $"/api/v1/admin/drug-catalog/{id}/activate", new { rowVersion = drug.GetProperty("rowVersion").GetString(), reason = "reviewed" })).GetProperty("drug").Clone();
        var p = await AddAsync(doctor, app.EncounterUrl(encounterId) + "/prescription/items", CompleteItem(drugId: id));
        var target = await CatalogDrugAsync(manager, "Canonical medication");
        await AddAsync(manager, $"/api/v1/admin/drug-catalog/{id}/merge", new { rowVersion = drug.GetProperty("rowVersion").GetString(), reason = "duplicate", targetDrugCatalogId = target.GetProperty("drugCatalogId").GetGuid() });
        var own = await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{p.GetProperty("prescriptionId").GetGuid()}"); var item = own.GetProperty("draft").GetProperty("items")[0];
        Assert.Equal(id, item.GetProperty("drugCatalogId").GetGuid()); Assert.Equal("BRUFEN 400 MG 30 TABS.", item.GetProperty("medicationName").GetString());
        Assert.Equal(0, (await GetAsync(doctor, "/api/v1/doctors/me/drug-catalog?search=BRUFEN")).GetProperty("totalCount").GetInt64());
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task Stale_root_token_and_idempotency_payload_reuse_are_conflicts_and_discard_returns_full_state(bool sql)
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var drug = await CatalogDrugAsync(manager); var finalized = await FinalizedAsync(app, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid());
        var p = finalized.Prescription; var id = p.GetProperty("prescriptionId").GetGuid(); var url = $"/api/v1/doctors/me/prescriptions/{id}/correction-draft"; var old = p.GetProperty("rowVersion").GetString(); var key = Guid.NewGuid().ToString("N");
        p = await AddAsync(doctor, url, new { rowVersion = old, reason = "correction" }, key);
        using (var reused = await PostAsync(doctor, url, new { rowVersion = old, reason = "different payload" }, key)) Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        using (var stale = await doctor.PutAsJsonAsync(url + "/items/" + p.GetProperty("draft").GetProperty("items")[0].GetProperty("itemId").GetGuid(), CompleteItem(old), TestContext.Current.CancellationToken))
        { Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("Prescription.ConcurrencyConflict", await stale.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal); }
        p = await AddAsync(doctor, url + "/discard", new { rowVersion = p.GetProperty("rowVersion").GetString() });
        Assert.Equal(JsonValueKind.Null, p.GetProperty("draft").ValueKind); Assert.Equal("Finalized", p.GetProperty("current").GetProperty("status").GetString());
        Assert.True(p.GetProperty("capabilities").GetProperty("canVoid").GetBoolean()); Assert.Empty(p.GetProperty("completionBlockers").EnumerateArray()); Assert.NotEqual(old, p.GetProperty("rowVersion").GetString());
        var replay = await AddAsync(doctor, url, new { rowVersion = old, reason = "correction" }, key);
        Assert.Equal(JsonValueKind.Null, replay.GetProperty("draft").ValueKind); Assert.Equal(p.GetProperty("rowVersion").GetString(), replay.GetProperty("rowVersion").GetString());
        await app.WithDbAsync(async db => Assert.Equal(1, await db.PrescriptionVersions.IgnoreAutoIncludes().CountAsync(v => v.PrescriptionId == id, TestContext.Current.CancellationToken)));
        p = await AddAsync(doctor, url, new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "new correction" });
        Assert.Equal(3, p.GetProperty("draft").GetProperty("versionNumber").GetInt32());
        await app.WithDbAsync(async db =>
        {
            var mapping = await db.RolePermissions.SingleAsync(r => r.RoleId == SystemRoleIds.Doctor && r.PermissionId == SystemPermissionIds.For(PermissionNames.PrescriptionsCorrectOwn), TestContext.Current.CancellationToken);
            db.RolePermissions.Remove(mapping); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        // Current database permissions override a still-valid token issued before revocation.
        p = await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}");
        Assert.False(p.GetProperty("capabilities").GetProperty("canManageDraft").GetBoolean());
        Assert.False(p.GetProperty("capabilities").GetProperty("canRequestNewMedication").GetBoolean());
        using var denied = await doctor.PutAsJsonAsync(url + "/items/" + p.GetProperty("draft").GetProperty("items")[0].GetProperty("itemId").GetGuid(), CompleteItem(p.GetProperty("rowVersion").GetString()), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
