using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Seeding;

namespace Wasla.Tests.Integration;

internal static class SubscriptionTestData
{
    internal sealed class EntitledPermissions : ISubscriptionPermissionGuard
    {
        public Task<bool> AllowedForUserAsync(Guid userId, string permission, CancellationToken ct, SubscriptionResource? resource = null) => Task.FromResult(true);
        public Task<bool> AllowedAsync(Wasla.Application.Persistence.UserAccessSnapshot user, string permission, CancellationToken ct, SubscriptionResource? resource = null) => Task.FromResult(true);
    }
    internal sealed class EntitledService : ISubscriptionEntitlementService
    {
        public Task<BuildingBlock.Domain.Results.Result<SubscriptionEntitlementResult>> EvaluateAsync(Guid doctorId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken cancellationToken) =>
            Task.FromResult(BuildingBlock.Domain.Results.Result<SubscriptionEntitlementResult>.Ok(new(Guid.NewGuid(), "FULL_PLATFORM", "PREMIUM", featureCode, FeatureAccessLevel.Full)));
        public Task<BuildingBlock.Domain.Results.Result<DoctorSubscription>> ActiveAsync(Guid doctorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BuildingBlock.Domain.Results.Result> ValidateCapacityAsync(Guid doctorId, bool reception, CancellationToken ct) => Task.FromResult(BuildingBlock.Domain.Results.Result.Ok());
        public Task<BuildingBlock.Domain.Results.Result> ValidateOnboardingPracticeCapacityAsync(Guid doctorId, CancellationToken ct) => Task.FromResult(BuildingBlock.Domain.Results.Result.Ok());
    }
    // Existing healthcare scenarios explicitly have a paid commercial account. Tests still exercise real entitlement checks.
    internal static async Task SeedAsync(IServiceProvider services, WaslaDbContext db, CancellationToken ct)
    {
        await services.GetRequiredService<SubscriptionCatalogSeeder>().SeedAsync(ct);
        var resolver = services.GetRequiredService<ISubscriptionCatalogResolver>();
        var now = services.GetRequiredService<IDateTimeProvider>().UtcNow;
        var terms = await resolver.ResolveAsync(new("FULL_PLATFORM", "PREMIUM", BillingCycle.Annual), ct);
        Assert.True(terms.IsSuccess);
        var actor = await db.ApplicationUsers.Where(u => u.UserType == UserType.SuperAdmin).Select(u => u.Id).FirstAsync(ct);
        var enrolled = await db.DoctorSubscriptions.Select(s => s.DoctorId).Distinct().ToArrayAsync(ct);
        foreach (var doctor in await db.Doctors.Where(d => d.ApprovalStatus == DoctorApprovalStatus.Approved).ToArrayAsync(ct))
        {
            if (enrolled.Contains(doctor.Id)) continue;
            var s = DoctorSubscription.Select(doctor.Id, terms.Value, now).Value;
            s.Activate(now.AddDays(-1), actor); db.Add(s);
            foreach (var feature in terms.Value.Features) db.Add(new SubscriptionFeatureSnapshot(Guid.NewGuid(), s.Id, feature.FeatureId, feature.Code, feature.AccessLevel, now));
        }
        await db.SaveChangesAsync(ct);
    }

    internal static ISubscriptionEntitlementService Entitlements(WaslaDbContext db)
    {
        var general = (Wasla.Application.Persistence.IWaslaDataStore)Activator.CreateInstance(typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDataStore")!, [db])!;
        return new SubscriptionEntitlementService(new SubscriptionDataStore(db), general, new FixedClock());
    }
    private sealed class FixedClock : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }

    internal static async Task<Guid> ReplaceAsync(Phase13ApiFixture app, string plan = "STARTER", SubscriptionKind kind = SubscriptionKind.Paid,
        bool activate = true, BillingCycle cycle = BillingCycle.Monthly, SubscriptionSelection? selection = null)
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        foreach (var existing in await db.DoctorSubscriptions.Where(s => s.DoctorId == app.DoctorId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending)).ToArrayAsync(ct)) existing.Supersede(app.Clock.UtcNow);
        await db.SaveChangesAsync(ct);
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SubscriptionCatalogOptions>>().CurrentValue;
        var terms = await scope.ServiceProvider.GetRequiredService<ISubscriptionCatalogResolver>().ResolveAsync(selection ?? new("FULL_PLATFORM", plan,
            kind == SubscriptionKind.Trial ? null : cycle, kind == SubscriptionKind.Trial ? options.FullPlatform.Trial.OfferCode : null), ct);
        Assert.True(terms.IsSuccess); var lifecycle = scope.ServiceProvider.GetRequiredService<SubscriptionLifecycle>();
        var selected = lifecycle.Select(app.DoctorId, terms.Value).Value;
        if (activate) selected.Activate(app.Clock.UtcNow, (await db.SuperAdmins.SingleAsync(a => a.IsRootSuperAdmin, ct)).ApplicationUserId);
        if (activate && kind == SubscriptionKind.Trial) await lifecycle.ScheduleRemindersAsync(selected, ct);
        await db.SaveChangesAsync(ct); return selected.Id;
    }
}
