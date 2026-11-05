using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class CommercialV12ScopeTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Booking_origin_cannot_borrow_clinical_rights_after_transfer_to_an_effective_full_contract(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root"); using var patient = await app.ClientAsync("patient");
        Guid fullContract = default;
        await app.WithDbAsync(async db => fullContract = await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId).Select(p => p.SubscriptionContractId).SingleAsync());
        var booking = await BookingContractAsync(doctor, admin, 5);
        var bookingContract = booking.GetProperty("subscriptionContractId").GetGuid();
        await MoveAsync(doctor, app.PracticeId, bookingContract);
        using var reservation = await PostAsync(patient, "/api/v1/reservations", SubscriptionReservationCapabilityTests.Booking(app));
        Assert.Equal(HttpStatusCode.Created, reservation.StatusCode);
        var reservationId = (await JsonAsync(reservation)).GetProperty("reservationId").GetGuid();
        await MoveAsync(doctor, app.PracticeId, fullContract);
        await using var scope = app.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<ISubscriptionPermissionGuard>();
        Assert.False(await guard.AllowedForUserAsync(app.DoctorUserId, PermissionNames.MedicalEncountersStartOwn,
            TestContext.Current.CancellationToken, new(SubscriptionResourceKind.Reservation, reservationId)));
        Assert.True((await scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>()
            .EvaluateForPracticeAsync(app.DoctorId, app.PracticeId, "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsSuccess);
        await app.WithDbAsync(async db =>
        {
            var origin = await db.ReservationCommercialProvenances.SingleAsync(p => p.Id == reservationId);
            Assert.Equal(bookingContract, origin.SubscriptionContractId); Assert.False(origin.Grants("MEDICAL_ENCOUNTER", FeatureAccessLevel.Full));
            Assert.Single(await db.PracticeSubscriptionAllocations.Where(a => a.DoctorPracticeId == app.PracticeId && a.ReleasedAtUtc == null).ToArrayAsync());
            Assert.Equal(3, await db.PracticeSubscriptionAllocations.CountAsync(a => a.DoctorPracticeId == app.PracticeId));
        });
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Proven_full_reservation_continues_after_expiry_even_with_an_unrelated_active_contract(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root"); using var patient = await app.ClientAsync("patient");
        using var reservation = await PostAsync(patient, "/api/v1/reservations", SubscriptionReservationCapabilityTests.Booking(app));
        Assert.Equal(HttpStatusCode.Created, reservation.StatusCode);
        var reservationId = (await JsonAsync(reservation)).GetProperty("reservationId").GetGuid();
        Guid periodId = default;
        await app.WithDbAsync(async db =>
        {
            var period = await db.DoctorSubscriptions.SingleAsync(p => p.DoctorId == app.DoctorId); periodId = period.Id;
            db.Entry(period).Property(p => p.EndsAtUtc).CurrentValue = app.Clock.UtcNow.AddSeconds(1); await db.SaveChangesAsync();
        });
        await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "unrelated");
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(2);
        await using var scope = app.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<ISubscriptionPermissionGuard>();
        Assert.True(await guard.AllowedForUserAsync(app.DoctorUserId, PermissionNames.MedicalEncountersStartOwn,
            TestContext.Current.CancellationToken, new(SubscriptionResourceKind.Reservation, reservationId)));
        Assert.False(await guard.AllowedForUserAsync(app.DoctorUserId, PermissionNames.MedicalEncountersStartOwn,
            TestContext.Current.CancellationToken, new(SubscriptionResourceKind.Practice, app.PracticeId)));
        Assert.False((await scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>()
            .EvaluateForPracticeAsync(app.DoctorId, app.PracticeId, "ONLINE_BOOKING", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsSuccess);
        await app.WithDbAsync(async db => Assert.Equal(periodId, (await db.ReservationCommercialProvenances.SingleAsync(p => p.Id == reservationId)).SubscriptionId));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Renewal_downgrade_selection_is_effective_at_boundary_without_worker_and_preserves_manual_state(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        Guid periodId = default, contractId = default;
        await app.WithDbAsync(async db => { var p = await db.DoctorSubscriptions.SingleAsync(p => p.DoctorId == app.DoctorId); periodId = p.Id; contractId = p.SubscriptionContractId; });
        using var created = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "عيادة غير محتفظ بها", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = contractId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var extraId = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var allocation = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation");
        var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + periodId);
        using var selected = await PostAsync(doctor, $"/api/v1/doctors/me/subscriptions/{contractId}/renewal-selection", new
        {
            rowVersion = details.GetProperty("rowVersion").GetString(), targetPlanCode = "STARTER", targetBillingCycle = "Monthly",
            retainedAllocations = new[] { new { practiceId = app.PracticeId, allocationId = allocation.GetProperty("allocation").GetProperty("id").GetGuid(), receptionIds = Array.Empty<Guid>() } }
        });
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using var paid = await PostAsync(admin, $"/api/v1/admin/subscriptions/{periodId}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode); var next = (await JsonAsync(paid)).GetProperty("subscription");
        await using (var before = app.Services.CreateAsyncScope()) Assert.True((await before.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>()
            .EvaluateForPracticeAsync(app.DoctorId, extraId, "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsSuccess);
        app.Clock.UtcNow = next.GetProperty("startsAtUtc").GetDateTime();
        await using (var after = app.Services.CreateAsyncScope())
        {
            var entitlement = after.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>();
            Assert.True((await entitlement.EvaluateForPracticeAsync(app.DoctorId, app.PracticeId, "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsSuccess);
            Assert.True((await entitlement.EvaluateForPracticeAsync(app.DoctorId, extraId, "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsFailure);
        }
        await SubscriptionApiTests.Process(app, next.GetProperty("id").GetGuid());
        await app.WithDbAsync(async db =>
        {
            Assert.False((await db.DoctorPractices.SingleAsync(p => p.Id == extraId)).IsActive);
            Assert.True((await db.DoctorPractices.SingleAsync(p => p.Id == app.PracticeId)).IsActive);
            Assert.False(await db.PracticeSubscriptionAllocations.AnyAsync(a => a.DoctorPracticeId == extraId && a.ReleasedAtUtc == null));
            Assert.Equal(contractId, (await db.DoctorSubscriptions.SingleAsync(p => p.Id == next.GetProperty("id").GetGuid())).SubscriptionContractId);
        });
    }

    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Parallel_transfers_to_the_final_seat_have_one_winner_without_releasing_the_losing_source()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var one = await app.ClientAsync("doctor"); using var two = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        Guid source = default;
        await app.WithDbAsync(async db => source = await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId).Select(p => p.SubscriptionContractId).SingleAsync());
        using var created = await PostAsync(one, "/api/v1/doctors/me/practices", new { nameAr = "عيادة أخرى", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = source });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var second = (await JsonAsync(created)).GetProperty("id").GetGuid();
        var target = (await CommercialV12ApiTests.SelectAndActivateAsync(one, admin, "STARTER", "transfer-final-seat")).GetProperty("subscriptionContractId").GetGuid();
        var firstToken = (await GetAsync(one, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation")).GetProperty("allocation").GetProperty("rowVersion").GetString();
        var secondToken = (await GetAsync(two, $"/api/v1/doctors/me/practices/{second}/subscription-allocation")).GetProperty("allocation").GetProperty("rowVersion").GetString();
        var results = await Task.WhenAll(
            PostAsync(one, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation/move", new { targetSubscriptionContractId = target, continuedReceptionIds = Array.Empty<Guid>(), rowVersion = firstToken }),
            PostAsync(two, $"/api/v1/doctors/me/practices/{second}/subscription-allocation/move", new { targetSubscriptionContractId = target, continuedReceptionIds = Array.Empty<Guid>(), rowVersion = secondToken }));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var result in results) result.Dispose(); }
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.PracticeSubscriptionAllocations.CountAsync(a => a.SubscriptionContractId == target && a.ReleasedAtUtc == null));
            Assert.Equal(1, await db.PracticeSubscriptionAllocations.CountAsync(a => a.SubscriptionContractId == source && a.ReleasedAtUtc == null));
            Assert.Equal(3, await db.PracticeSubscriptionAllocations.CountAsync(a => a.DoctorPracticeId == app.PracticeId || a.DoctorPracticeId == second));
        });
    }

    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Parallel_creation_for_last_contract_seat_has_one_winner_on_sql_server()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var doctor = await app.ClientAsync("doctor"); using var other = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var period = await CommercialV12ApiTests.SelectAndActivateAsync(doctor, admin, "STARTER", "last-seat");
        var contractId = period.GetProperty("subscriptionContractId").GetGuid();
        var body = new { nameAr = "عيادة", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = contractId };
        var responses = await Task.WhenAll(PostAsync(doctor, "/api/v1/doctors/me/practices", body), PostAsync(other, "/api/v1/doctors/me/practices", body));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var r in responses) r.Dispose(); }
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Booking_quantity_five_to_four_renews_at_two_hundred_with_exact_retention_and_immutable_old_price(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var old = await BookingContractAsync(doctor, admin, 5); var id = old.GetProperty("id").GetGuid(); var contract = old.GetProperty("subscriptionContractId").GetGuid();
        await MoveAsync(doctor, app.PracticeId, contract);
        for (var i = 0; i < 4; ++i)
        {
            using var created = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "عيادة", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = contract });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        RetainedPracticeAllocation[] retained = []; Guid excluded = default;
        await app.WithDbAsync(async db =>
        {
            var allocations = await db.PracticeSubscriptionAllocations.Where(a => a.SubscriptionContractId == contract && a.ReleasedAtUtc == null).OrderBy(a => a.Id).ToArrayAsync();
            Assert.Equal(5, allocations.Length); excluded = allocations[4].DoctorPracticeId;
            retained = allocations.Take(4).Select(a => new RetainedPracticeAllocation(a.DoctorPracticeId, a.Id, [])).ToArray();
        });
        var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id);
        using var selected = await PostAsync(doctor, $"/api/v1/doctors/me/subscriptions/{contract}/renewal-selection", new { rowVersion = details.GetProperty("rowVersion").GetString(), targetPracticeCount = 4, retainedAllocations = retained });
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using var paid = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 200, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode); var next = (await JsonAsync(paid)).GetProperty("subscription");
        Assert.Equal(200, next.GetProperty("priceAmount").GetDecimal()); Assert.Equal(4, next.GetProperty("practiceLimit").GetInt32());
        var dashboard = (await GetAsync(doctor, "/api/v1/doctors/me/subscriptions/" + contract))[0];
        Assert.Equal("RenewalConfirmed", dashboard.GetProperty("upcomingStatus").GetString());
        Assert.Equal(200, dashboard.GetProperty("renewalPreview").GetProperty("priceAmount").GetDecimal());
        app.Clock.UtcNow = next.GetProperty("startsAtUtc").GetDateTime();
        await using var scope = app.Services.CreateAsyncScope(); var entitlement = scope.ServiceProvider.GetRequiredService<ISubscriptionEntitlementService>();
        Assert.True((await entitlement.EvaluateForPracticeAsync(app.DoctorId, retained[0].PracticeId, "ONLINE_BOOKING", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsSuccess);
        Assert.True((await entitlement.EvaluateForPracticeAsync(app.DoctorId, excluded, "ONLINE_BOOKING", FeatureAccessLevel.Full, TestContext.Current.CancellationToken)).IsFailure);
        await app.WithDbAsync(async db => { var original = await db.DoctorSubscriptions.SingleAsync(p => p.Id == id); Assert.Equal(250, original.PriceAmount); Assert.Equal(5, original.PracticeLimit); });
    }

    [Theory, Trait("Category", "SQLServerConcurrency")][InlineData(true)][InlineData(false)]
    public async Task Parallel_trial_starts_preserve_one_initial_period_and_one_outbox_event(bool sameKey)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); using var one = await app.ClientAsync("doctor"); using var two = await app.ClientAsync("doctor");
        var responses = await Task.WhenAll(PostAsync(one, "/api/v1/doctors/me/subscriptions/trial/activate", new { }, "trial-race"), PostAsync(two, "/api/v1/doctors/me/subscriptions/trial/activate", new { }, sameKey ? "trial-race" : "trial-race-2"));
        try { Assert.Contains(responses, r => r.IsSuccessStatusCode); Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict)); }
        finally { foreach (var r in responses) r.Dispose(); }
        await app.WithDbAsync(async db =>
        {
            var trial = Assert.Single(await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId && p.SubscriptionKind == SubscriptionKind.Trial).ToArrayAsync());
            Assert.Equal(app.Clock.UtcNow, trial.StartsAtUtc); Assert.Equal(app.Clock.UtcNow.AddDays(20), trial.EndsAtUtc);
            Assert.Single(await db.EmailOutboxMessages.Where(e => e.IdempotencyKey == "subscription-trial-started:" + trial.Id).ToArrayAsync());
        });
    }

    private static async Task<JsonElement> BookingContractAsync(HttpClient doctor, HttpClient admin, int quantity)
    {
        using var selected = await PostAsync(doctor, "/api/v1/doctors/me/subscriptions/selection", new { productCode = "ONLINE_BOOKING_ONLY", planCode = "BOOKING_ONLY", billingCycle = "Monthly", selectedPracticeCount = quantity });
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode); var pending = await JsonAsync(selected);
        using var activated = await PostAsync(admin, $"/api/v1/admin/subscriptions/{pending.GetProperty("subscription").GetProperty("id").GetGuid()}/activate", new { rowVersion = pending.GetProperty("rowVersion").GetString(), amountReceived = quantity * 50, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode); return (await JsonAsync(activated)).GetProperty("subscription");
    }
    private static async Task MoveAsync(HttpClient doctor, Guid practiceId, Guid contractId)
    {
        var details = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{practiceId}/subscription-allocation");
        using var moved = await PostAsync(doctor, $"/api/v1/doctors/me/practices/{practiceId}/subscription-allocation/move", new
        { targetSubscriptionContractId = contractId, continuedReceptionIds = Array.Empty<Guid>(), rowVersion = details.GetProperty("allocation").GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
    }
}
