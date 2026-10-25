using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;
using Wasla.Domain.Clinical;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17ClinicalNotificationTests
{
    [Fact]
    public async Task C09_R16_current_family_booking_authority_and_parent_optin_do_not_create_dependent_clinical_delivery()
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(); var app = family.App;
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var gates = app.Services.GetRequiredService<IOptions<NotificationPrivacyOptions>>().Value;
        gates.ClinicalEmailReleaseApproved = true; gates.ClinicalInAppReleaseApproved = true;
        Guid parentUser = default;
        await app.WithDbAsync(async db =>
        {
            parentUser = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            await TrustEmailAsync(db, parentUser, app.Clock.UtcNow);
            db.NotificationPreferences.Add(new(parentUser, NotificationCategory.ClinicalAvailability, NotificationChannel.Email, true, "ar", app.Clock.UtcNow));
            await db.SaveChangesAsync();
        });
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception, patientId: family.ChildId));
        var request = await AddAsync(app, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), "lab", "PRIVATE_DEPENDENT_TEST");
        await CompleteAsync(app, doctor, ticket, request);
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == request.GetProperty("requestId").GetGuid() && e.EventType == "Lab.RequestAvailable");
            Assert.Equal(family.ChildId, e.PatientId);
            Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.RecipientApplicationUserId == parentUser));
            Assert.False(await db.UserNotifications.AnyAsync(n => n.ApplicationUserId == parentUser && n.Category == NotificationCategory.ClinicalAvailability));
        });
    }
    [Theory]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    public async Task C03_C04_C05_C06_C07_C08_C09_C11_E12_E13_diagnostic_transitions_are_durable_generic_and_self_only(string kind, bool submission)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var doctor = await app.ClientAsync("doctor");
        using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var gates = app.Services.GetRequiredService<IOptions<NotificationPrivacyOptions>>().Value;
        gates.ClinicalInAppReleaseApproved = true;
        await app.WithDbAsync(async db => { var link = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId); await TrustEmailAsync(db, link.ApplicationUserId, app.Clock.UtcNow); await db.SaveChangesAsync(); });
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounter = ticket.GetProperty("medicalEncounterId").GetGuid();
        var request = await AddAsync(app, doctor, encounter, kind, "PRIVATE_TEST_NAME"); var requestId = request.GetProperty("requestId").GetGuid();
        await app.WithDbAsync(async db => Assert.False(await db.NotificationEvents.AnyAsync(e => e.SourceId == requestId)));
        await CompleteAsync(app, doctor, ticket, kind == "lab" ? request : null, kind == "radiology" ? request : null);
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == requestId);
            Assert.Equal(kind == "lab" ? "Lab.RequestAvailable" : "Radiology.RequestAvailable", e.EventType);
            Assert.True(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.Channel == NotificationChannel.Email && d.Status == NotificationDeliveryStatus.Suppressed));
            Assert.True(await db.UserNotifications.AnyAsync(n => n.EventType == e.EventType));
        });
        var preferences = await GetAsync(patient, "/api/v1/notifications/me/preferences");
        using (var updated = await patient.PutAsJsonAsync("/api/v1/notifications/me/preferences", new { preferredLanguage = "ar", preferences = new[] { new { category = "ClinicalAvailability", channel = "Email", enabled = true } }, rowVersion = preferences.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var itemIds = request.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray();
        System.Text.Json.JsonElement official;
        if (submission)
        {
            var pending = await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{requestId}/submissions", kind));
            await app.WithDbAsync(async db => Assert.False(await db.NotificationEvents.AnyAsync(e => e.EventType.EndsWith("OfficialResultAvailable"))));
            var submitted = pending.GetProperty("submission");
            object body = kind == "lab" ? new { coveredLabRequestItemIds = itemIds, rowVersion = submitted.GetProperty("rowVersion").GetString() } :
                new { coveredRadiologyRequestItemIds = itemIds, rowVersion = submitted.GetProperty("rowVersion").GetString() };
            official = await OkAsync(await PostAsync(doctor, Url(kind, $"-result-submissions/{submitted.GetProperty("submissionId").GetGuid()}/accept"), body));
        }
        else
        {
            var published = await GetAsync(doctor, Url(kind, $"-requests/{requestId}"));
            official = await OkAsync(await UploadAsync(doctor, Url(kind, $"-requests/{requestId}/results"), kind, itemIds, published.GetProperty("rowVersion").GetString()));
        }
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.EventType == (kind == "lab" ? "Lab.OfficialResultAvailable" : "Radiology.OfficialResultAvailable"));
            Assert.True(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.SafeFailureCode == "Clinical.EmailReleaseRequired"));
            Assert.All(await db.NotificationDeliveries.Where(d => d.NotificationEventId == e.Id).ToArrayAsync(), d => Assert.Equal(app.PatientId, d.RecipientPatientId));
        });
        gates.ClinicalEmailReleaseApproved = true;
        var result = official.GetProperty("result"); var resultId = result.GetProperty("resultId").GetGuid();
        var corrected = await OkAsync(await UploadAsync(doctor, Url(kind, $"-results/{resultId}/corrections"), kind, itemIds, result.GetProperty("rowVersion").GetString(), "PRIVATE_CORRECTION_REASON"));
        await DispatchAsync(app);
        var voided = await OkAsync(await PostAsync(doctor, Url(kind, $"-results/{resultId}/void"), new { rowVersion = corrected.GetProperty("result").GetProperty("rowVersion").GetString(), reason = "PRIVATE_VOID_REASON" }));
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.Single(await db.NotificationEvents.Where(e => e.SourceId == resultId && e.EventType == "DiagnosticResult.Corrected").ToArrayAsync());
            Assert.Single(await db.NotificationEvents.Where(e => e.SourceId == resultId && e.EventType == "DiagnosticResult.Voided").ToArrayAsync());
            var messages = await db.EmailOutboxMessages.Where(m => m.NotificationDeliveryId != null).ToArrayAsync();
            Assert.NotEmpty(messages);
            Assert.All(messages, m => { Assert.DoesNotContain("PRIVATE_", m.HtmlBody, StringComparison.Ordinal); Assert.DoesNotContain("Attachments", m.HtmlBody, StringComparison.Ordinal); });
            Assert.All(await db.UserNotifications.Where(n => n.Category == NotificationCategory.ClinicalAvailability).ToArrayAsync(), n => { Assert.Equal("MedicalRecord", n.TargetResourceType); Assert.Null(n.TargetResourceKey); });
        });
        var inbox = await GetAsync(patient, "/api/v1/notifications/mine");
        Assert.DoesNotContain("PRIVATE_", inbox.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(encounter.ToString(), inbox.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task C01_C02_C10_prescription_draft_finalize_hidden_navigation_and_source_reauthorization()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false);
        using var manager = await app.ClientAsync("catalog-manager"); using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        using var patient = await app.ClientAsync("patient"); using var other = await app.ClientAsync("other-patient");
        app.Services.GetRequiredService<IOptions<NotificationPrivacyOptions>>().Value.ClinicalInAppReleaseApproved = true;
        var drug = await Phase14MedicationApiTests.CatalogDrugAsync(manager);
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception)); var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var prescription = await Phase14MedicationApiTests.AddAsync(doctor, app.EncounterUrl(encounterId) + "/prescription/items", Phase14MedicationApiTests.CompleteItem(drugId: drug.GetProperty("drugCatalogId").GetGuid()));
        var id = prescription.GetProperty("prescriptionId").GetGuid();
        await app.WithDbAsync(async db => Assert.False(await db.NotificationEvents.AnyAsync(e => e.SourceId == id)));
        var encounter = await GetAsync(doctor, app.EncounterUrl(encounterId));
        using var notes = await doctor.PatchAsJsonAsync(app.EncounterUrl(encounterId) + "/clinical-notes", new { clinicalNotes = "PRIVATE_CLINICAL_NOTES", rowVersion = encounter.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        encounter = await JsonAsync(notes);
        await OkAsync(await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", new { visibility = "Hidden", ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = encounter.GetProperty("rowVersion").GetString(), prescriptionRowVersion = prescription.GetProperty("rowVersion").GetString() }));
        await DispatchAsync(app);
        var inbox = await GetAsync(patient, "/api/v1/notifications/mine"); Assert.Contains("Prescription.Available", inbox.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CLINICAL_NOTES", inbox.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(id.ToString(), inbox.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var denied = await other.GetAsync($"/api/v1/prescriptions/mine/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        app.Services.GetRequiredService<IOptions<NotificationPrivacyOptions>>().Value.ClinicalInAppReleaseApproved = false;
        Assert.Empty((await GetAsync(patient, "/api/v1/notifications/mine")).GetProperty("items").EnumerateArray());
    }
}
