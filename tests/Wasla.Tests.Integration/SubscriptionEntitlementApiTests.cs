using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Practices;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionEntitlementApiTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Booking_only_can_configure_booking_and_read_reservations_but_cannot_use_operations_or_clinical_features(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var o = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<SubscriptionCatalogOptions>>().CurrentValue;
            o.BookingOnly.Enabled = true; o.BookingOnly.MaximumSelectablePractices = 3;
            o.BookingOnly.PricingTiers = [new() { MinPracticeCount = 1, MaxPracticeCount = 3, MonthlyPrice = 300 }];
        }
        await SubscriptionTestData.ReplaceAsync(app, selection: new("ONLINE_BOOKING_ONLY", "BOOKING_ONLY", BillingCycle.Monthly, SelectedPracticeCount: 2));
        using var doctor = await app.ClientAsync("doctor");
        var own = await GetAsync(doctor, "/api/v1/doctors/me/subscription"); Assert.Equal(7, own.GetProperty("entitlements").GetArrayLength());
        await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/reservations");
        foreach (var url in new[] { $"/api/v1/doctors/me/practices/{app.PracticeId}/encounters", $"/api/v1/practices/{app.PracticeId}/queue", "/api/v1/doctors/me/revenue/dashboard?fromDate=2026-10-01&toDate=2026-10-31", $"/api/v1/practices/{app.PracticeId}/financial-transactions" })
        { using var denied = await doctor.GetAsync(url, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
        using var mutation = await PostAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/reservations/{Guid.NewGuid()}/cancel", new { reason = "not allowed", rowVersion = Convert.ToBase64String(new byte[8]) }); Assert.Equal(HttpStatusCode.Forbidden, mutation.StatusCode);
        using var created = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "عيادة حجز", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var practiceId = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var config = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{practiceId}/configuration"); Assert.False(config.GetProperty("allowWalkIn").GetBoolean());
        var changed = JsonNode.Parse(config.GetRawText())!.AsObject(); changed["allowWalkIn"] = true;
        using var walkIn = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/practices/{practiceId}/configuration", changed, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, walkIn.StatusCode);
        using var price = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/practices/{practiceId}/booking-presentation/price", new { newConsultationPrice = 250 }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, price.StatusCode);
        using var overflow = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "فوق الحد", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 }); Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
        using var reception = await PostAsync(doctor, "/api/v1/doctors/me/receptions", new { userName = "denied-seat", email = "denied-seat@example.test", temporaryPassword = "CommercialTest123!", nameAr = "استقبال" }); Assert.Equal(HttpStatusCode.Forbidden, reception.StatusCode);
    }
    [Fact]
    public async Task Reception_identity_counts_once_and_only_account_deactivation_releases_capacity()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); await SubscriptionTestData.ReplaceAsync(app);
        using var doctor = await app.ClientAsync("doctor"); Guid receptionId = default; Guid assignmentId = default;
        await app.WithDbAsync(async db =>
        {
            var reception = await db.Receptions.SingleAsync(r => r.OwnerDoctorId == app.DoctorId); receptionId = reception.Id;
            var second = DoctorPractice.Create(Guid.NewGuid(), app.DoctorId, "عيادة قديمة", null, 1, 1, 1, "Address", 30, 31).Value; db.Add(second);
            var assignment = ReceptionPracticeAssignment.Create(Guid.NewGuid(), reception.Id, second.Id, app.DoctorUserId).Value;
            assignmentId = assignment.Id; db.Add(assignment); await db.SaveChangesAsync();
        });
        var own = await GetAsync(doctor, "/api/v1/doctors/me/subscription"); Assert.Equal(1, own.GetProperty("usage").GetProperty("receptionCount").GetInt32()); Assert.Equal(2, own.GetProperty("usage").GetProperty("practiceCount").GetInt32());
        await app.WithDbAsync(async db => { (await db.ReceptionPracticeAssignments.SingleAsync(a => a.Id == assignmentId)).Deactivate(app.DoctorUserId); await db.SaveChangesAsync(); });
        using var stillFull = await PostAsync(doctor, "/api/v1/doctors/me/receptions", new { userName = "seat-b", email = "seat-b@example.test", temporaryPassword = "CommercialTest123!", nameAr = "استقبال" }); Assert.Equal(HttpStatusCode.Conflict, stillFull.StatusCode);
        var original = await GetAsync(doctor, "/api/v1/doctors/me/receptions/" + receptionId);
        using var deactivated = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/receptions/{receptionId}/account-state", new { isActive = false, rowVersion = original.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        using var replacement = await PostAsync(doctor, "/api/v1/doctors/me/receptions", new { userName = "seat-b", email = "seat-b@example.test", temporaryPassword = "CommercialTest123!", nameAr = "استقبال" }); Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        var inactive = await JsonAsync(deactivated);
        using var reactivation = await doctor.PutAsJsonAsync($"/api/v1/doctors/me/receptions/{receptionId}/account-state", new { isActive = true, rowVersion = inactive.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, reactivation.StatusCode);
    }
    [Fact]
    public async Task Expiration_blocks_new_work_but_existing_ticket_and_clinical_visit_can_be_completed()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await SubscriptionTestData.ReplaceAsync(app);
        using var reception = await app.ClientAsync("reception"); using var doctor = await app.ClientAsync("doctor");
        // The commitment exists before the period ends, even if the state processor has not run yet.
        var ticket = await app.WalkInAsync(reception); var started = await app.StartAsync(doctor, ticket);
        await app.WithDbAsync(async db =>
        {
            var s = await db.DoctorSubscriptions.SingleAsync(s => s.Id == id);
            db.Entry(s).Property(p => p.EndsAtUtc).CurrentValue = app.Clock.UtcNow.AddSeconds(1);
            await db.SaveChangesAsync();
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(2); await SubscriptionApiTests.Process(app, id);
        var completed = await app.CompleteAsync(doctor, started);
        Assert.Equal("Completed", completed.Encounter.GetProperty("status").GetString());
        using var blocked = await PostAsync(reception, $"/api/v1/practices/{app.PracticeId}/tickets/walk-in", new { patientId = app.PatientId, segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId, paidAmount = 300, paymentMethod = "Cash" }); Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.Tickets.CountAsync(t => t.DoctorId == app.DoctorId)); Assert.Equal(1, await db.MedicalEncounters.CountAsync(e => e.DoctorId == app.DoctorId)); });
    }
}
