using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Seeding;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionApiTests
{
    [Fact]
    public async Task Approved_flat_change_contract_resolves_current_product_cycle_and_server_price_without_client_amounts()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await SubscriptionTestData.ReplaceAsync(app);
        using var doctor = await app.ClientAsync("doctor");
        using var requested = await PostAsync(doctor, "/api/v1/doctors/me/subscription/change-requests", new { changeType = "UpgradePlan", targetPlanCode = "PREMIUM", targetPracticeCount = (int?)null, targetBillingCycle = (string?)null, reason = "Need more clinic capacity" });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var change = (await JsonAsync(requested)).GetProperty("changeRequests")[0];
        Assert.Equal("PREMIUM", change.GetProperty("targetPlanCode").GetString()); Assert.Equal("Monthly", change.GetProperty("targetBillingCycle").GetString());
        Assert.Equal(1300m, change.GetProperty("differenceAmount").GetDecimal());
        using var second = await PostAsync(doctor, "/api/v1/doctors/me/subscription/change-requests", new { changeType = "UpgradePlan", targetPlanCode = "PROFESSIONAL" }); Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await app.WithDbAsync(async db => Assert.Empty(await db.SubscriptionPayments.Where(p => p.SubscriptionId == id).ToArrayAsync()));
    }
    [Fact]
    public async Task Catalog_is_public_idempotently_seeded_and_hides_internal_transition()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<SubscriptionCatalogSeeder>().SeedAsync(TestContext.Current.CancellationToken);
        using var client = app.CreateClient(); var catalog = await GetAsync(client, "/api/v1/public/commercial/catalog");
        Assert.Equal(2, catalog.GetProperty("products").GetArrayLength()); Assert.DoesNotContain("LEGACY_TRANSITION", catalog.GetRawText());
        Assert.True(catalog.GetProperty("bookingOnly").GetProperty("enabled").GetBoolean());
        Assert.Equal(50m, catalog.GetProperty("bookingOnly").GetProperty("monthlyUnitPrice").GetDecimal());
        await app.WithDbAsync(async db => { Assert.Equal(28, await db.CommercialFeatures.CountAsync()); Assert.Equal(5, await db.CommercialPlans.CountAsync()); Assert.Equal(2, await db.CommercialProducts.CountAsync()); });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Pending_payment_activation_is_exact_cash_only_idempotent_and_preserves_snapshot(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); var id = await SubscriptionTestData.ReplaceAsync(app, activate: false);
        using var admin = await app.ClientAsync("root"); using var doctor = await app.ClientAsync("doctor");
        var pending = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id); var row = pending.GetProperty("rowVersion").GetString();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var changed = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<SubscriptionCatalogOptions>>().CurrentValue;
            changed.CatalogVersion = "2.0.0"; var starter = changed.FullPlatform.Plans.Single(p => p.Code == "STARTER");
            starter.MonthlyPrice = 599; starter.AnnualPrice = 5990; starter.PracticeLimit = 2; starter.ReceptionLimit = 2;
            changed.FullPlatform.Trial.PracticeLimit = 2; changed.FullPlatform.Trial.ReceptionLimit = 2;
        }
        using var operational = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.PracticeId}/segments", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, operational.StatusCode);
        using var badMethod = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/activate", new { rowVersion = row, paymentMethod = "Card", amountReceived = 499 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badMethod.StatusCode);
        using var partial = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/activate", new { rowVersion = row, paymentMethod = "Cash", amountReceived = 498 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, partial.StatusCode);
        var key = Guid.NewGuid().ToString(); var body = new { rowVersion = row, paymentMethod = "Cash", amountReceived = 499 };
        using var activated = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/activate", body, key); Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        using var replay = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/activate", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var s = await db.DoctorSubscriptions.SingleAsync(s => s.Id == id); Assert.Equal(SubscriptionStatus.Active, s.Status);
            Assert.Equal(499, s.PriceAmount); Assert.Equal(1, s.PracticeLimit); Assert.Equal("2026-11-05-v1.2", s.CatalogVersion);
            Assert.Equal(app.Clock.UtcNow, s.StartsAtUtc); Assert.Equal(app.Clock.UtcNow.AddMonths(1), s.EndsAtUtc);
            Assert.Single(await db.SubscriptionPayments.Where(p => p.SubscriptionId == id).ToArrayAsync());
            Assert.Single(await db.EmailOutboxMessages.Where(e => e.IdempotencyKey == "subscription-activated:" + id).ToArrayAsync());
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Trial_reminders_extension_and_expiration_are_durable_and_idempotent(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); var id = await SubscriptionTestData.ReplaceAsync(app, kind: SubscriptionKind.Trial);
        using var admin = await app.ClientAsync("root"); var started = app.Clock.UtcNow;
        app.Clock.UtcNow = started.AddDays(15);
        await Process(app, id); await Process(app, id);
        var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id);
        using var extended = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/trial-extend", new { rowVersion = details.GetProperty("rowVersion").GetString(), additionalDays = 5, reason = "Approved commercial exception" }); Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var s = await db.DoctorSubscriptions.SingleAsync(s => s.Id == id); Assert.Equal(started.AddDays(25), s.TrialEndsAtUtc);
            Assert.Equal(3, await db.SubscriptionReminderSchedules.CountAsync(r => r.SubscriptionId == id && r.Status == SubscriptionReminderStatus.Cancelled));
            Assert.Equal(4, await db.SubscriptionReminderSchedules.CountAsync(r => r.SubscriptionId == id && r.TrialRevision == 1));
            Assert.Empty(await db.SubscriptionPayments.Where(p => p.SubscriptionId == id).ToArrayAsync());
        });
        app.Clock.UtcNow = started.AddDays(25); await Process(app, id); await Process(app, id);
        await app.WithDbAsync(async db =>
        { Assert.Equal(SubscriptionStatus.Expired, (await db.DoctorSubscriptions.SingleAsync(s => s.Id == id)).Status); Assert.Single(await db.SubscriptionHistories.Where(h => h.SubscriptionId == id && h.EventType == "Expired").ToArrayAsync()); });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Upgrade_is_server_priced_and_keeps_period_end_then_renewal_schedules_next_paid_period(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); var id = await SubscriptionTestData.ReplaceAsync(app);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var before = await GetAsync(doctor, "/api/v1/doctors/me/subscription");
        using var requested = await PostAsync(doctor, "/api/v1/doctors/me/subscription/change-requests", new { rowVersion = before.GetProperty("rowVersion").GetString(), changeType = "Upgrade", selection = new { productCode = "FULL_PLATFORM", planCode = "PROFESSIONAL", billingCycle = "Monthly" } });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var detail = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id); var change = detail.GetProperty("changeRequests")[0];
        Assert.Equal(500, change.GetProperty("differenceAmount").GetDecimal());
        var body = new { rowVersion = detail.GetProperty("rowVersion").GetString(), changeRowVersion = change.GetProperty("rowVersion").GetString(), paymentMethod = "Cash", amountReceived = 500 };
        using var approved = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/change-requests/{change.GetProperty("id").GetGuid()}/approve", body);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode); var upgraded = await JsonAsync(approved);
        Assert.Equal(before.GetProperty("subscription").GetProperty("endsAtUtc").GetDateTime(), upgraded.GetProperty("subscription").GetProperty("endsAtUtc").GetDateTime());
        using var renewed = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/renew", new { rowVersion = upgraded.GetProperty("rowVersion").GetString(), paymentMethod = "Cash", amountReceived = 999 });
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode); var next = (await JsonAsync(renewed)).GetProperty("subscription");
        Assert.Equal("Scheduled", next.GetProperty("status").GetString()); Assert.Equal(upgraded.GetProperty("subscription").GetProperty("endsAtUtc").GetDateTime(), next.GetProperty("startsAtUtc").GetDateTime());
        app.Clock.UtcNow = next.GetProperty("startsAtUtc").GetDateTime(); await Process(app, next.GetProperty("id").GetGuid());
        await app.WithDbAsync(async db => Assert.Equal(1, await db.DoctorSubscriptions.CountAsync(s => s.DoctorId == app.DoctorId && s.Status == SubscriptionStatus.Active)));
    }
    internal static async Task Process(Phase13ApiFixture app, Guid id)
    {
        await using var scope = app.Services.CreateAsyncScope(); var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessSubscriptionLifecycleCommand(id), TestContext.Current.CancellationToken); Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Lifecycle_batch_rolls_back_failed_email_history_and_continues_other_doctors_then_retries_once()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await SubscriptionTestData.ReplaceAsync(app);
        await app.WithDbAsync(async db =>
        {
            var last = await db.DoctorSubscriptions.Where(s => s.Status == SubscriptionStatus.Active).MaxAsync(s => s.EndsAtUtc);
            app.Clock.UtcNow = last!.Value.AddSeconds(1);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_subscription_email BEFORE INSERT ON EmailOutboxMessages WHEN NEW.RecipientEmail = 'doctor@example.test' BEGIN SELECT RAISE(FAIL, 'test-subscription-outbox-failure'); END;", TestContext.Current.CancellationToken);
        });
        await using (var scope = app.Services.CreateAsyncScope())
        { Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Subscriptions.SubscriptionLifecycleProcessor>().ProcessBatchAsync(TestContext.Current.CancellationToken)); }
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(SubscriptionStatus.Active, (await db.DoctorSubscriptions.SingleAsync(s => s.Id == id)).Status);
            Assert.Empty(await db.SubscriptionHistories.Where(h => h.SubscriptionId == id && h.EventType == "Expired").ToArrayAsync());
            Assert.Equal(1, await db.DoctorSubscriptions.CountAsync(s => s.Status == SubscriptionStatus.Expired));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_subscription_email;", TestContext.Current.CancellationToken);
        });
        await using (var scope = app.Services.CreateAsyncScope())
        { Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Subscriptions.SubscriptionLifecycleProcessor>().ProcessBatchAsync(TestContext.Current.CancellationToken)); }
        await app.WithDbAsync(async db => Assert.Single(await db.SubscriptionHistories.Where(h => h.SubscriptionId == id && h.EventType == "Expired").ToArrayAsync()));
    }
}
