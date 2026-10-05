using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase14MedicationApiTests
{
    internal const string Password = "Phase13TestPass123!";
    internal static async Task<Phase13ApiFixture> CreateAsync(bool sql)
    {
        var app = await Phase13ApiFixture.CreateAsync(sql);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var hash = await scope.ServiceProvider.GetRequiredService<IPasswordService>().HashAsync(Password, TestContext.Current.CancellationToken);
        var manager = ApplicationUser.Create(Guid.NewGuid(), "catalog-manager", "catalog-manager@example.test", null, hash, UserType.DrugCatalogManager, false, app.Clock.UtcNow).Value;
        var admin = ApplicationUser.Create(Guid.NewGuid(), "admin", "admin@example.test", null, hash, UserType.SuperAdmin, false, app.Clock.UtcNow).Value;
        db.ApplicationUsers.AddRange(manager, admin);
        db.UserRoles.AddRange(new UserRole(Guid.NewGuid(), manager.Id, SystemRoleIds.DrugCatalogManager), new UserRole(Guid.NewGuid(), admin.Id, SystemRoleIds.SuperAdmin));
        db.SuperAdmins.Add(SuperAdmin.Create(Guid.NewGuid(), admin.Id, "Admin", "Admin", false, app.DoctorUserId).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return app;
    }
    internal static object CompleteItem(string? token = null, Guid? drugId = null, string dose = "1 Tablet")
        => new { drugCatalogId = drugId, prescriptionRowVersion = token, strength = "400 mg", dosageForm = "Tablet", route = "Oral", dose,
            frequencyCode = "TwiceDaily", frequencyText = "Twice daily", durationType = "Fixed", durationValue = 7, durationUnit = "Days", quantityValue = 14,
            quantityUnit = "Tablet", instructions = "After food" };
    internal static async Task<JsonElement> CatalogDrugAsync(HttpClient manager, string name = "BRUFEN 400 MG 30 TABS.")
    {
        using var response = await PostAsync(manager, "/api/v1/admin/drug-catalog", new { commercialNameEn = name, commercialNameAr = "بروفين",
            scientificName = "Ibuprofen", manufacturer = "Maker", route = "ORAL", strengthText = "400 mg", dosageForm = "Tablet", priceEgp = 50 });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await JsonAsync(response)).GetProperty("drug").Clone();
    }
    internal static async Task<JsonElement> AddAsync(HttpClient doctor, string url, object body, string? key = null)
    {
        using var response = await PostAsync(doctor, url, body, key);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await JsonAsync(response);
    }
    internal static async Task<(JsonElement Ticket, JsonElement Prescription, Guid EncounterId)> FinalizedAsync(Phase13ApiFixture app, HttpClient doctor, HttpClient reception, Guid drugId)
    {
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid();
        var p = await AddAsync(doctor, app.EncounterUrl(e) + "/prescription/items", CompleteItem(drugId: drugId));
        var encounter = await GetAsync(doctor, app.EncounterUrl(e));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(e) + "/clinical-notes", new { clinicalNotes = "Clinical documentation", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        using var complete = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new
        { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = p.GetProperty("rowVersion").GetString() });
        Assert.True(complete.StatusCode == HttpStatusCode.OK, await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var result = await JsonAsync(complete); Assert.Equal(p.GetProperty("prescriptionId").GetGuid(), result.GetProperty("prescriptionId").GetGuid());
        return (result, await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{p.GetProperty("prescriptionId").GetGuid()}"), e);
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task One_call_missing_drug_completion_correction_and_void_preserve_patient_visibility(bool sql)
    {
        await using var app = await CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor");
        using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var drug = await CatalogDrugAsync(manager);
        var search = await GetAsync(doctor, "/api/v1/doctors/me/drug-catalog?search=بروفين"); var found = search.GetProperty("items")[0];
        Assert.Equal(drug.GetProperty("drugCatalogId").GetGuid(), found.GetProperty("drugCatalogId").GetGuid()); Assert.Equal("400 mg", found.GetProperty("strength").GetString());
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var e = ticket.GetProperty("medicalEncounterId").GetGuid(); var url = app.EncounterUrl(e) + "/prescription/items";
        // Add directly from search output: no drug-details lookup.
        var p = await AddAsync(doctor, url, new { drugCatalogId = found.GetProperty("drugCatalogId").GetGuid() });
        var id = p.GetProperty("prescriptionId").GetGuid(); var key = Guid.NewGuid().ToString("N");
        var missing = new { newMedication = new { medicationName = "Missing medication" }, prescriptionRowVersion = p.GetProperty("rowVersion").GetString() };
        p = await AddAsync(doctor, url, missing, key); var replay = await AddAsync(doctor, url, missing, key);
        Assert.Equal(p.GetProperty("rowVersion").GetString(), replay.GetProperty("rowVersion").GetString()); Assert.Equal(2, replay.GetProperty("draft").GetProperty("items").GetArrayLength());
        Assert.True(p.GetProperty("capabilities").GetProperty("canManageDraft").GetBoolean()); Assert.NotEmpty(p.GetProperty("completionBlockers").EnumerateArray());
        var requestId = p.GetProperty("draft").GetProperty("items")[1].GetProperty("drugCatalogRequestId").GetGuid();
        using (var hidden = await patient.GetAsync($"/api/v1/prescriptions/mine/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var encounter = await GetAsync(doctor, app.EncounterUrl(e)); Assert.False(encounter.GetProperty("capabilities").GetProperty("canComplete").GetBoolean());
        using (var invalid = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = p.GetProperty("rowVersion").GetString() }))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode); var errors = await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("PrescriptionItem.DoseRequired", errors, StringComparison.Ordinal); Assert.Contains("PrescriptionItem.DurationRequired", errors, StringComparison.Ordinal);
            Assert.Contains("MedicalEncounter.ClinicalNotesRequired", errors, StringComparison.Ordinal);
        }
        foreach (var item in p.GetProperty("draft").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray())
        {
            using var updated = await doctor.PutAsJsonAsync(url + "/" + item, CompleteItem(p.GetProperty("rowVersion").GetString()), TestContext.Current.CancellationToken);
            Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); p = await JsonAsync(updated);
        }
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(e) + "/clinical-notes", new { clinicalNotes = "Documented visit", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode); encounter = await JsonAsync(notes);
        using var complete = await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = p.GetProperty("rowVersion").GetString() });
        Assert.True(complete.StatusCode == HttpStatusCode.OK, await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var patientView = await GetAsync(patient, $"/api/v1/prescriptions/mine/{id}"); Assert.Equal(1, patientView.GetProperty("versionNumber").GetInt32());
        Assert.False(patientView.TryGetProperty("correctionReason", out _)); Assert.False(patientView.GetProperty("items")[1].TryGetProperty("drugCatalogRequestId", out _));
        // Later request approval must not rewrite the original submitted prescription item.
        var request = await GetAsync(manager, $"/api/v1/admin/drug-catalog-requests/{requestId}");
        await AddAsync(manager, $"/api/v1/admin/drug-catalog-requests/{requestId}/approve", new { rowVersion = request.GetProperty("rowVersion").GetString(), approvedData = new { commercialNameEn = "Approved missing medication", scientificName = "science" } });
        p = await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}"); Assert.Equal("DoctorSubmitted", p.GetProperty("current").GetProperty("items")[1].GetProperty("medicationSource").GetString());
        var correctionUrl = $"/api/v1/doctors/me/prescriptions/{id}/correction-draft";
        p = await AddAsync(doctor, correctionUrl, new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "correct dose" });
        var resumed = await AddAsync(doctor, correctionUrl, new { rowVersion = "stale", reason = "resume" }); Assert.Equal(p.GetProperty("draft").GetProperty("versionId").GetGuid(), resumed.GetProperty("draft").GetProperty("versionId").GetGuid());
        Assert.Equal(1, (await GetAsync(patient, $"/api/v1/prescriptions/mine/{id}")).GetProperty("versionNumber").GetInt32());
        using (var ordinary = await doctor.PutAsJsonAsync(url + "/" + p.GetProperty("current").GetProperty("items")[0].GetProperty("itemId").GetGuid(), CompleteItem(p.GetProperty("rowVersion").GetString()), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Conflict, ordinary.StatusCode);
        using (var blockedVoid = await PostAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/void", new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "void" })) Assert.Equal(HttpStatusCode.Conflict, blockedVoid.StatusCode);
        var cloneId = p.GetProperty("draft").GetProperty("items")[0].GetProperty("itemId").GetGuid();
        using var edit = await doctor.PutAsJsonAsync(correctionUrl + "/items/" + cloneId, CompleteItem(p.GetProperty("rowVersion").GetString(), dose: "2 Tablets"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode); p = await JsonAsync(edit);
        p = await AddAsync(doctor, correctionUrl + "/finalize", new { rowVersion = p.GetProperty("rowVersion").GetString() });
        Assert.Equal(2, (await GetAsync(patient, $"/api/v1/prescriptions/mine/{id}")).GetProperty("versionNumber").GetInt32());
        var versions = await GetAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/versions"); Assert.Equal("Superseded", versions[0].GetProperty("status").GetString()); Assert.Equal("1 Tablet", versions[0].GetProperty("items")[0].GetProperty("clinical").GetProperty("dose").GetString());
        p = await AddAsync(doctor, $"/api/v1/doctors/me/prescriptions/{id}/void", new { rowVersion = p.GetProperty("rowVersion").GetString(), reason = "entered in error" });
        Assert.Equal("Voided", (await GetAsync(patient, $"/api/v1/prescriptions/mine/{id}")).GetProperty("status").GetString());
        Assert.Equal(1, (await GetAsync(patient, "/api/v1/prescriptions/mine")).GetProperty("totalCount").GetInt64());
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task Root_only_account_governance_and_all_clinical_actor_boundaries_are_enforced(bool sql)
    {
        await using var app = await CreateAsync(sql); using var root = await app.ClientAsync("root"); using var manager = await app.ClientAsync("catalog-manager");
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        using var admin = await app.ClientAsync("admin"); using var otherDoctor = await app.ClientAsync("other-doctor"); using var otherPatient = await app.ClientAsync("other-patient");
        var account = new { userName = "manager2", email = "manager2@example.test", initialPassword = Password, confirmPassword = Password };
        foreach (var denied in new[] { admin, manager, doctor, reception, patient })
        {
            using var response = await PostAsync(denied, "/api/v1/admin/drug-catalog-managers", account); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var created = await PostAsync(root, "/api/v1/admin/drug-catalog-managers", account); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var accountJson = await JsonAsync(created); Assert.True(accountJson.GetProperty("isFirstLogin").GetBoolean()); var userId = accountJson.GetProperty("id").GetGuid();
        using (var deniedRead = await admin.GetAsync($"/api/v1/admin/drug-catalog-managers/{userId}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        using (var deniedUpdate = await admin.PutAsJsonAsync($"/api/v1/admin/drug-catalog-managers/{userId}", new { email = "forbidden@example.test" }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, deniedUpdate.StatusCode);
        foreach (var action in new[] { "activate", "deactivate" })
        {
            using var deniedAction = await PostAsync(admin, $"/api/v1/admin/drug-catalog-managers/{userId}/{action}", new { }); Assert.Equal(HttpStatusCode.Forbidden, deniedAction.StatusCode);
        }
        Assert.Equal(2, (await GetAsync(root, "/api/v1/admin/drug-catalog-managers")).GetProperty("totalCount").GetInt64());
        using var manager2 = await app.ClientAsync("manager2");
        using (var firstLogin = await manager2.GetAsync("/api/v1/admin/drug-catalog", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, firstLogin.StatusCode);
        using (var changePassword = await PostAsync(manager2, "/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "ChangedManagerPass123!", confirmPassword = "ChangedManagerPass123!" })) Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);
        using (var updated = await root.PutAsJsonAsync($"/api/v1/admin/drug-catalog-managers/{userId}", new { email = "updated@example.test", phoneNumber = "01012345678" }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using (var deactivate = await PostAsync(root, $"/api/v1/admin/drug-catalog-managers/{userId}/deactivate", new { })) Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        using (var activate = await PostAsync(root, $"/api/v1/admin/drug-catalog-managers/{userId}/activate", new { })) Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        using (var unsafeMapping = await root.PutAsJsonAsync($"/api/v1/admin/roles/{SystemRoleIds.DrugCatalogManager}/permissions", new { permissionIds = new[] { SystemPermissionIds.For(PermissionNames.PrescriptionsViewOwn) } }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, unsafeMapping.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var mappings = await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                where mapping.RoleId == SystemRoleIds.DrugCatalogManager select permission.Name).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(PermissionNames.DrugCatalogManagerDefaults.Order(), mappings.Order());
            foreach (var role in new[] { SystemRoleIds.Reception, SystemRoleIds.SuperAdmin })
                Assert.False(await (from mapping in db.RolePermissions join permission in db.Permissions on mapping.PermissionId equals permission.Id
                    where mapping.RoleId == role && permission.Name.StartsWith("Prescriptions.") select mapping).AnyAsync(TestContext.Current.CancellationToken));
        });
        var drug = await CatalogDrugAsync(manager); var completed = await FinalizedAsync(app, doctor, reception, drug.GetProperty("drugCatalogId").GetGuid()); var id = completed.Prescription.GetProperty("prescriptionId").GetGuid();
        foreach (var denied in new[] { reception, manager, admin, root, patient })
        {
            using var response = await denied.GetAsync($"/api/v1/doctors/me/prescriptions/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var action = await PostAsync(denied, $"/api/v1/doctors/me/prescriptions/{id}/void", new { rowVersion = completed.Prescription.GetProperty("rowVersion").GetString(), reason = "unauthorized" }); Assert.Equal(HttpStatusCode.Forbidden, action.StatusCode);
        }
        using (var denied = await otherDoctor.GetAsync($"/api/v1/doctors/me/prescriptions/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await otherPatient.GetAsync($"/api/v1/prescriptions/mine/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal("Finalized", (await GetAsync(patient, $"/api/v1/prescriptions/mine/{id}")).GetProperty("status").GetString());
        foreach (var denied in new[] { manager, admin, root, reception, doctor })
        {
            using var response = await denied.GetAsync($"/api/v1/prescriptions/mine/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
    internal static object SourceRow(string name, string? scientific = "science", string route = "UNKNOWN", decimal price = 10)
        => new { commercial_name_en = name, commercial_name_ar = name, scientific_name = scientific, manufacturer = "maker", drug_class = ".", route, price_egp = price };
    internal static async Task<JsonElement> PreviewAsync(HttpClient manager, params object[] rows)
    {
        using var multipart = new MultipartFormDataContent(); using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(rows));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); multipart.Add(content, "file", "snapshot.json");
        using var response = await manager.PostAsync("/api/v1/admin/drug-catalog/imports/preview", multipart, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(response);
    }
    [Theory, InlineData(false), InlineData(true)]
    public async Task Import_stages_once_classifies_preserves_overrides_and_marks_missing_without_deleting(bool sql)
    {
        await using var app = await CreateAsync(sql); using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor");
        var initial = await PreviewAsync(manager, SourceRow("Clean"), SourceRow("Needs review", null), SourceRow("CANCELLED"), SourceRow("ILLEGAL IMPORT"), SourceRow("Clean"));
        Assert.Equal(5, initial.GetProperty("totalRecords").GetInt32()); Assert.Equal(1, initial.GetProperty("exactDuplicateRecords").GetInt32());
        await app.WithDbAsync(async db => Assert.Equal(0, await db.DrugCatalogs.CountAsync(TestContext.Current.CancellationToken)));
        var batchId = initial.GetProperty("batchId").GetGuid(); var apply = $"/api/v1/admin/drug-catalog/imports/{batchId}/apply";
        await AddAsync(manager, apply, new { }); await AddAsync(manager, apply, new { });
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(4, await db.DrugCatalogs.CountAsync(TestContext.Current.CancellationToken));
            var clean = await db.DrugCatalogs.SingleAsync(d => d.CommercialNameEn == "Clean", TestContext.Current.CancellationToken); Assert.Equal(DrugCatalogStatus.Active, clean.Status); Assert.Equal("UNKNOWN", clean.Route);
            Assert.Equal(DrugCatalogStatus.NeedsReview, (await db.DrugCatalogs.SingleAsync(d => d.CommercialNameEn == "Needs review", TestContext.Current.CancellationToken)).Status);
            Assert.Equal("SourceCancelled", (await db.DrugCatalogs.SingleAsync(d => d.CommercialNameEn == "CANCELLED", TestContext.Current.CancellationToken)).StatusReason);
        });
        var cleanResult = (await GetAsync(doctor, "/api/v1/doctors/me/drug-catalog?search=Clean")).GetProperty("items")[0]; var cleanId = cleanResult.GetProperty("drugCatalogId").GetGuid();
        Assert.Equal(JsonValueKind.Null, cleanResult.GetProperty("route").ValueKind);
        var priceBatch = await PreviewAsync(manager, SourceRow("Clean", price: 20), SourceRow("Clean", route: "Oral"));
        Assert.Equal(1, priceBatch.GetProperty("priceChanges").GetInt32()); Assert.Equal(1, priceBatch.GetProperty("possibleDuplicateRecords").GetInt32());
        await AddAsync(manager, $"/api/v1/admin/drug-catalog/imports/{priceBatch.GetProperty("batchId").GetGuid()}/apply", new { });
        var detail = await GetAsync(manager, $"/api/v1/admin/drug-catalog/{cleanId}"); Assert.Equal(20, detail.GetProperty("drug").GetProperty("referencePriceEgp").GetDecimal());
        using var overridden = await manager.PutAsJsonAsync($"/api/v1/admin/drug-catalog/{cleanId}", new { rowVersion = detail.GetProperty("drug").GetProperty("rowVersion").GetString(), data = new { commercialNameEn = "Clean", commercialNameAr = "Clean", scientificName = "science", manufacturer = "maker", drugClass = ".", route = "UNKNOWN", priceEgp = 77 } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        var later = await PreviewAsync(manager, SourceRow("Clean", price: 30)); await AddAsync(manager, $"/api/v1/admin/drug-catalog/imports/{later.GetProperty("batchId").GetGuid()}/apply", new { });
        await app.WithDbAsync(async db =>
        {
            var clean = await db.DrugCatalogs.SingleAsync(d => d.Id == cleanId, TestContext.Current.CancellationToken); Assert.Equal(77, clean.PriceEgp); Assert.False(clean.IsMissingFromLatestSource);
            var missing = await db.DrugCatalogs.SingleAsync(d => d.CommercialNameEn == "CANCELLED", TestContext.Current.CancellationToken); Assert.True(missing.IsMissingFromLatestSource); Assert.Equal(DrugCatalogStatus.Inactive, missing.Status);
            Assert.Equal(5, await db.DrugCatalogs.CountAsync(TestContext.Current.CancellationToken));
        });
        Assert.Equal(3, (await GetAsync(manager, "/api/v1/admin/drug-catalog/imports")).GetProperty("totalCount").GetInt64());
        var changes = await GetAsync(manager, $"/api/v1/admin/drug-catalog/imports/{batchId}/changes?changeType=ExactDuplicate"); Assert.Equal(1, changes.GetProperty("totalCount").GetInt64());
    }
}
