using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionLegacyAndRenewalTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Legacy_enrollment_uses_one_fixed_batch_preserves_usage_and_coexists_with_independent_manual_paid_contract(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); Guid legacyDoctor = default;
        await using (var scope = app.Services.CreateAsyncScope()) scope.ServiceProvider.GetRequiredService<IOptionsMonitor<SubscriptionCatalogOptions>>().CurrentValue.LegacyTransition.Enabled = true;
        await app.WithDbAsync(async db =>
        {
            var hash = (await db.ApplicationUsers.SingleAsync(u => u.Id == app.DoctorUserId)).PasswordHash;
            var user = ApplicationUser.Create(Guid.NewGuid(), "legacy", "legacy@example.test", null, hash, UserType.Doctor, false, app.Clock.UtcNow).Value;
            var doctor = Doctor.Create(Guid.NewGuid(), user.Id, "طبيب قديم", "Legacy doctor", new(1980, 1, 1), Gender.Male, null, "front", "back", "card", null, DateOnly.FromDateTime(app.Clock.UtcNow)).Value;
            var root = await db.SuperAdmins.SingleAsync(a => a.IsRootSuperAdmin); doctor.Approve("29902021234988", root.ApplicationUserId, app.Clock.UtcNow.AddDays(-1)); legacyDoctor = doctor.Id;
            db.Add(user); db.Add(doctor);
            db.Add(new UserRole(Guid.NewGuid(), user.Id, SystemRoleIds.Doctor));
            for (var i = 0; i < 4; i++) db.Add(DoctorPractice.Create(Guid.NewGuid(), doctor.Id, "عيادة قديمة " + i, null, 1, 1, 1, "Address", 30, 31).Value);
            await db.SaveChangesAsync();
        });
        using var admin = await app.ClientAsync("root"); using var enrolled = await PostAsync(admin, "/api/v1/admin/subscriptions/legacy-transition/enroll", new { }); Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode);
        var batch = await JsonAsync(enrolled); Assert.Equal(app.Clock.UtcNow.AddDays(30), batch.GetProperty("endsAtUtc").GetDateTime());
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(1); using var replay = await PostAsync(admin, "/api/v1/admin/subscriptions/legacy-transition/enroll", new { }); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(batch.GetProperty("id").GetGuid(), (await JsonAsync(replay)).GetProperty("id").GetGuid()); Guid transitionId = default;
        await app.WithDbAsync(async db =>
        { var s = await db.DoctorSubscriptions.SingleAsync(s => s.DoctorId == legacyDoctor); transitionId = s.Id; Assert.Equal(SubscriptionKind.LegacyTransition, s.SubscriptionKind); Assert.Equal(4, s.PracticeLimit); Assert.Equal(1, s.ReceptionLimit); Assert.Equal(batch.GetProperty("startedAtUtc").GetDateTime(), s.StartsAtUtc); Assert.Empty(await db.SubscriptionPayments.Where(p => p.SubscriptionId == s.Id).ToArrayAsync()); });
        Guid pendingId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var terms = await scope.ServiceProvider.GetRequiredService<ISubscriptionCatalogResolver>().ResolveAsync(new("FULL_PLATFORM", "STARTER", BillingCycle.Monthly), TestContext.Current.CancellationToken);
            var pending = scope.ServiceProvider.GetRequiredService<SubscriptionLifecycle>().Select(legacyDoctor, terms.Value).Value; pendingId = pending.Id;
            await scope.ServiceProvider.GetRequiredService<WaslaDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var detail = await GetAsync(admin, "/api/v1/admin/subscriptions/" + pendingId);
        using var activated = await PostAsync(admin, $"/api/v1/admin/subscriptions/{pendingId}/activate", new { rowVersion = detail.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" }); Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(SubscriptionStatus.Active, (await db.DoctorSubscriptions.SingleAsync(s => s.Id == transitionId)).Status); Assert.Equal(4, await db.DoctorPractices.CountAsync(p => p.DoctorId == legacyDoctor)); });
        using var legacyClient = await app.ClientAsync("legacy");
        using var over = await PostAsync(legacyClient, "/api/v1/doctors/me/practices", new { nameAr = "فوق الحد", subscriptionContractId = (await JsonAsync(activated)).GetProperty("subscription").GetProperty("subscriptionContractId").GetGuid(), governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 }); Assert.Equal(HttpStatusCode.Created, over.StatusCode);
    }
    [Fact]
    public async Task Scheduled_downgrade_retains_the_accepted_selection_without_blocking_on_new_old_period_usage()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await SubscriptionTestData.ReplaceAsync(app, "PROFESSIONAL"); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var own = await GetAsync(doctor, "/api/v1/doctors/me/subscription");
        using var requested = await PostAsync(doctor, "/api/v1/doctors/me/subscription/change-requests", new { rowVersion = own.GetProperty("rowVersion").GetString(), changeType = "Downgrade", selection = new { productCode = "FULL_PLATFORM", planCode = "STARTER", billingCycle = "Monthly" } }); Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var detail = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id); var change = detail.GetProperty("changeRequests")[0];
        using var approved = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/change-requests/{change.GetProperty("id").GetGuid()}/approve", new { rowVersion = detail.GetProperty("rowVersion").GetString(), changeRowVersion = change.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var added = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "عيادة إضافية غير نشطة", subscriptionContractId = own.GetProperty("subscription").GetProperty("subscriptionContractId").GetGuid(), governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31 }); Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        using var renewal = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/renew", new { rowVersion = (await JsonAsync(approved)).GetProperty("rowVersion").GetString(), paymentMethod = "Cash", amountReceived = 499 }); Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        Assert.Equal("Scheduled", (await JsonAsync(renewal)).GetProperty("subscription").GetProperty("status").GetString());
        await app.WithDbAsync(async db => Assert.Empty(await db.SubscriptionPayments.Where(p => p.SubscriptionId == id).ToArrayAsync()));
    }
    [Fact]
    public async Task Cancellation_keeps_paid_access_and_manual_late_renewal_starts_at_activation_time()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await SubscriptionTestData.ReplaceAsync(app); using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        var own = await GetAsync(doctor, "/api/v1/doctors/me/subscription"); using var cancelled = await PostAsync(doctor, "/api/v1/doctors/me/subscription/cancel-at-period-end", new { rowVersion = own.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledDetail = await JsonAsync(cancelled); Assert.True(cancelledDetail.GetProperty("subscription").GetProperty("willNotRenew").GetBoolean());
        app.Clock.UtcNow = cancelledDetail.GetProperty("subscription").GetProperty("endsAtUtc").GetDateTime().AddDays(2); await SubscriptionApiTests.Process(app, id);
        var expired = await GetAsync(admin, "/api/v1/admin/subscriptions/" + id);
        using var renewed = await PostAsync(admin, $"/api/v1/admin/subscriptions/{id}/renew", new { rowVersion = expired.GetProperty("rowVersion").GetString(), paymentMethod = "Cash", amountReceived = 499 }); Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Equal(app.Clock.UtcNow, (await JsonAsync(renewed)).GetProperty("subscription").GetProperty("startsAtUtc").GetDateTime());
    }
}
