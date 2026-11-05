using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class CommercialV12ApiTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Trial_is_doctor_started_after_a_year_independent_of_paid_and_replays_original_dates(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor");
        var eligibility = await GetAsync(doctor, "/api/v1/doctors/me/subscriptions/trial/eligibility");
        Assert.Equal("TrialAvailable", eligibility.GetProperty("status").GetString());
        await app.WithDbAsync(async db => Assert.False(await db.DoctorSubscriptions.AnyAsync(p => p.SubscriptionKind == SubscriptionKind.Trial)));
        app.Clock.UtcNow = app.Clock.UtcNow.AddYears(1);
        var started = app.Clock.UtcNow;
        using var first = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/trial/activate", new { }, "v12-trial");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var trial = (await JsonAsync(first)).GetProperty("subscription");
        Assert.Equal(started, trial.GetProperty("startsAtUtc").GetDateTime());
        Assert.Equal(started.AddDays(20), trial.GetProperty("endsAtUtc").GetDateTime());
        app.Clock.UtcNow = started.AddDays(1);
        using var replay = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/trial/activate", new { }, "v12-trial");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(trial.GetProperty("id").GetGuid(), (await JsonAsync(replay)).GetProperty("subscription").GetProperty("id").GetGuid());
        using var secondKey = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/trial/activate", new { }, "v12-second-trial");
        Assert.Equal(HttpStatusCode.Conflict, secondKey.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Single(await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId && p.SubscriptionKind == SubscriptionKind.Trial).ToArrayAsync());
            Assert.Equal(SubscriptionStatus.Active, (await db.DoctorSubscriptions.SingleAsync(p => p.DoctorId == app.DoctorId && p.SubscriptionKind == SubscriptionKind.Paid)).Status);
            Assert.Equal(4, await db.SubscriptionReminderSchedules.CountAsync(r => r.SubscriptionId == trial.GetProperty("id").GetGuid()));
            Assert.Single(await db.EmailOutboxMessages.Where(e => e.IdempotencyKey == "subscription-trial-started:" + trial.GetProperty("id").GetGuid()).ToArrayAsync());
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Same_plan_contracts_are_independent_and_selection_key_is_bound_to_intent(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var first = await SelectAndActivateAsync(doctor, admin, "STARTER", "first");
        var second = await SelectAndActivateAsync(doctor, admin, "STARTER", "second");
        Assert.NotEqual(first.GetProperty("subscriptionContractId").GetGuid(), second.GetProperty("subscriptionContractId").GetGuid());
        using var ambiguous = await doctor.GetAsync("/api/v1/doctors/me/subscription", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, ambiguous.StatusCode);
        using var reused = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/selection", new { productCode = "FULL_PLATFORM", planCode = "PREMIUM", billingCycle = "Monthly" }, "first");
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        await app.WithDbAsync(async db => Assert.Equal(3, await db.DoctorSubscriptions.CountAsync(p => p.DoctorId == app.DoctorId && p.Status == SubscriptionStatus.Active)));
    }

    [Theory][InlineData(1, 50)][InlineData(5, 250)][InlineData(10, 500)][InlineData(2147483647, 107374182350)]
    public async Task Booking_seat_quote_is_linear_without_a_fixed_business_maximum(int count, decimal amount)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var doctor = await app.ClientAsync("doctor");
        using var selected = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/selection", new { productCode = "ONLINE_BOOKING_ONLY", planCode = "BOOKING_ONLY", billingCycle = "Monthly", selectedPracticeCount = count });
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var period = (await JsonAsync(selected)).GetProperty("subscription");
        Assert.Equal(amount, period.GetProperty("priceAmount").GetDecimal()); Assert.Equal(count, period.GetProperty("practiceLimit").GetInt32());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Practice_creation_consumes_the_selected_contract_seat_even_while_inactive(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var period = await SelectAndActivateAsync(doctor, admin, "STARTER", "create-seat");
        var contractId = period.GetProperty("subscriptionContractId").GetGuid();
        var body = new { nameAr = "عيادة جديدة", nameEn = "New practice", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30m, longitude = 31m, subscriptionContractId = contractId };
        using var created = await PostAsync(doctor, "/api/v1/doctors/me/practices", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.False((await JsonAsync(created)).GetProperty("isActive").GetBoolean());
        using var noSeat = await PostAsync(doctor, "/api/v1/doctors/me/practices", body);
        Assert.Equal(HttpStatusCode.Conflict, noSeat.StatusCode);
        await app.WithDbAsync(async db => Assert.Single(await db.PracticeSubscriptionAllocations.Where(a => a.SubscriptionContractId == contractId && a.ReleasedAtUtc == null).ToArrayAsync()));
    }

    internal static async Task<JsonElement> SelectAndActivateAsync(HttpClient doctor, HttpClient admin, string plan, string key)
    {
        using var selected = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/selection", new { productCode = "FULL_PLATFORM", planCode = plan, billingCycle = "Monthly" }, key);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var pending = await JsonAsync(selected); var periodId = pending.GetProperty("subscription").GetProperty("id").GetGuid();
        using var activated = await PostAsync(admin, $"/api/v1/admin/subscriptions/{periodId}/activate", new { rowVersion = pending.GetProperty("rowVersion").GetString(), paymentMethod = "Cash", amountReceived = plan == "STARTER" ? 499 : 1799 }, key + "-payment");
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode); return (await JsonAsync(activated)).GetProperty("subscription");
    }
}
