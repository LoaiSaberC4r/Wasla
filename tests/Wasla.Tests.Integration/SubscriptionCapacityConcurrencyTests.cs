using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionCapacityConcurrencyTests
{
    [Theory][InlineData("practice")][InlineData("reception")][InlineData("activation")][InlineData("extension")][InlineData("upgrade")][InlineData("renewal")]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Real_SQL_Server_parallel_commands_serialize_the_final_capacity_or_state_transition(string scenario)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var id = await SubscriptionTestData.ReplaceAsync(app, scenario is "practice" or "reception" ? "PROFESSIONAL" : "STARTER",
            scenario == "extension" ? SubscriptionKind.Trial : SubscriptionKind.Paid, activate: scenario != "activation");
        using var admin1 = await app.ClientAsync("root"); using var admin2 = await app.ClientAsync("root");
        using var doctor1 = await app.ClientAsync("doctor"); using var doctor2 = await app.ClientAsync("doctor");
        if (scenario == "practice")
            await app.WithDbAsync(async db => { db.Add(DoctorPractice.Create(Guid.NewGuid(), app.DoctorId, "عيادة غير نشطة", null, 1, 1, 1, "Address", 30, 31).Value); await db.SaveChangesAsync(); });
        if (scenario == "reception")
            await app.WithDbAsync(async db =>
            {
                for (var i = 0; i < 3; i++)
                {
                    var user = ApplicationUser.Create(Guid.NewGuid(), "seat-" + i, $"seat-{i}@example.test", null, "hash", UserType.Reception, false, app.Clock.UtcNow).Value;
                    db.Add(user); db.Add(Reception.Create(Guid.NewGuid(), user.Id, app.DoctorId, "استقبال", null, app.DoctorUserId).Value);
                }
                await db.SaveChangesAsync();
            });
        Guid? changeId = null; string? changeRow = null;
        if (scenario == "upgrade")
        {
            var own = await GetAsync(doctor1, "/api/v1/doctors/me/subscription");
            using var requested = await PostAsync(doctor1, "/api/v1/doctors/me/subscription/change-requests", new { rowVersion = own.GetProperty("rowVersion").GetString(), changeType = "Upgrade", selection = new { productCode = "FULL_PLATFORM", planCode = "PROFESSIONAL", billingCycle = "Monthly" } });
            Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
            var change = (await JsonAsync(requested)).GetProperty("changeRequests")[0]; changeId = change.GetProperty("id").GetGuid(); changeRow = change.GetProperty("rowVersion").GetString();
        }
        var details = await GetAsync(admin1, "/api/v1/admin/subscriptions/" + id); var row = details.GetProperty("rowVersion").GetString();
        string url; object first; object second;
        switch (scenario)
        {
            case "practice":
                url = "/api/v1/doctors/me/practices";
                first = new { nameAr = "عيادة جديدة أ", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 };
                second = new { nameAr = "عيادة جديدة ب", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 }; break;
            case "reception":
                url = "/api/v1/doctors/me/receptions";
                first = new { userName = "new-seat-a", email = "new-seat-a@example.test", temporaryPassword = "SubscriptionTest123!", nameAr = "استقبال أ" };
                second = new { userName = "new-seat-b", email = "new-seat-b@example.test", temporaryPassword = "SubscriptionTest123!", nameAr = "استقبال ب" }; break;
            case "extension": url = $"/api/v1/admin/subscriptions/{id}/trial-extend"; first = second = new { rowVersion = row, additionalDays = 5, reason = "Approved exception" }; break;
            case "upgrade": url = $"/api/v1/admin/subscriptions/{id}/change-requests/{changeId}/approve"; first = second = new { rowVersion = row, changeRowVersion = changeRow, paymentMethod = "Cash", amountReceived = 500 }; break;
            default: url = $"/api/v1/admin/subscriptions/{id}/" + (scenario == "activation" ? "activate" : "renew"); first = second = new { rowVersion = row, paymentMethod = "Cash", amountReceived = 499 }; break;
        }
        var clients = scenario is "practice" or "reception" ? new[] { doctor1, doctor2 } : [admin1, admin2];
        var responses = await Task.WhenAll(PostAsync(clients[0], url, first), PostAsync(clients[1], url, second));
        try
        {
            Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
            Assert.All(responses.Where(r => !r.IsSuccessStatusCode), r => Assert.True(r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity, r.StatusCode.ToString()));
            await app.WithDbAsync(async db =>
            {
                if (scenario == "practice") Assert.Equal(3, await db.DoctorPractices.CountAsync(p => p.DoctorId == app.DoctorId));
                if (scenario == "reception") Assert.Equal(5, await db.Receptions.CountAsync(r => r.OwnerDoctorId == app.DoctorId));
                if (scenario == "extension") Assert.Equal(1, (await db.DoctorSubscriptions.SingleAsync(s => s.Id == id)).TrialExtensionCount);
                if (scenario is "activation" or "upgrade") Assert.Single(await db.SubscriptionPayments.Where(p => p.SubscriptionId == id).ToArrayAsync());
                if (scenario == "renewal") Assert.Equal(1, await db.DoctorSubscriptions.CountAsync(s => s.DoctorId == app.DoctorId && s.Status == SubscriptionStatus.Scheduled));
            });
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
