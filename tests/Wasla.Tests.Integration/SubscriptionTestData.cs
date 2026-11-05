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
    // Explicit commercial proof for healthcare fixtures that deliberately insert a valid
    // confirmed reservation without exercising the booking API. Never used by product code.
    internal static async Task CaptureKnownOriginAsync(WaslaDbContext db, Wasla.Domain.Reservations.Reservation reservation)
    {
        var at = reservation.CreatedOnUtc;
        var candidates = await (from a in db.PracticeSubscriptionAllocations
                                join p in db.DoctorSubscriptions on a.SubscriptionContractId equals p.SubscriptionContractId
                                where a.DoctorPracticeId == reservation.DoctorPracticeId && a.AllocatedFromUtc <= at && (a.ReleasedAtUtc == null || at < a.ReleasedAtUtc) &&
                                    p.DoctorId == reservation.DoctorId && p.StartsAtUtc <= at && at < p.EndsAtUtc && (p.Status == SubscriptionStatus.Active || p.Status == SubscriptionStatus.Scheduled)
                                select new { Allocation = a, Period = p }).ToArrayAsync();
        var proof = Assert.Single(candidates);
        db.Add(new ReservationCommercialProvenance(reservation.Id, reservation.DoctorId, reservation.DoctorPracticeId, proof.Allocation.Id,
            proof.Period.SubscriptionContractId, proof.Period.Id, at, proof.Period.Terms));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
    internal sealed class EntitledPermissions : ISubscriptionPermissionGuard
    {
        public Task<bool> AllowedForUserAsync(Guid userId, string permission, CancellationToken ct, SubscriptionResource? resource = null) => Task.FromResult(true);
        public Task<bool> AllowedAsync(Wasla.Application.Persistence.UserAccessSnapshot user, string permission, CancellationToken ct, SubscriptionResource? resource = null) => Task.FromResult(true);
        public Task<IReadOnlyDictionary<string, SubscriptionPermissionDecision>> EvaluateManyAsync(Wasla.Application.Persistence.UserAccessSnapshot user,
            IReadOnlyCollection<string> permissions, CancellationToken ct, SubscriptionResource? resource = null) =>
            Task.FromResult<IReadOnlyDictionary<string, SubscriptionPermissionDecision>>(permissions.ToDictionary(p => p, _ => new SubscriptionPermissionDecision(true, ReadScope: new(true, []))));
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
        foreach (var practice in await db.DoctorPractices.ToArrayAsync(ct))
        {
            if (await db.PracticeSubscriptionAllocations.AnyAsync(a => a.DoctorPracticeId == practice.Id && a.ReleasedAtUtc == null, ct)) continue;
            var period = await db.DoctorSubscriptions.SingleOrDefaultAsync(p => p.DoctorId == practice.DoctorId && p.Status == SubscriptionStatus.Active, ct);
            if (period is not null) db.Add(new PracticeSubscriptionAllocation(practice.Id, period.SubscriptionContractId, period.StartsAtUtc!.Value, actor));
        }
        await db.SaveChangesAsync(ct);
    }

    internal static async Task<System.Text.Json.JsonElement> OwnPeriodAsync(HttpClient client, Phase13ApiFixture app, Guid periodId)
    {
        Guid contractId = default;
        await app.WithDbAsync(async db => contractId = await db.DoctorSubscriptions.Where(p => p.Id == periodId).Select(p => p.SubscriptionContractId).SingleAsync());
        var contracts = await Phase13ApiFixture.GetAsync(client, "/api/v1/doctors/me/subscriptions/" + contractId);
        return contracts[0].GetProperty("periods").EnumerateArray().Single(p => p.GetProperty("subscription").GetProperty("id").GetGuid() == periodId).Clone();
    }

    internal static ISubscriptionEntitlementService Entitlements(WaslaDbContext db)
    {
        var general = (Wasla.Application.Persistence.IWaslaDataStore)Activator.CreateInstance(typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDataStore")!, [db])!;
        return new SubscriptionEntitlementService(new SubscriptionDataStore(db), general, new FixedClock());
    }
    private sealed class FixedClock : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }

    internal static async Task<Guid> BookingOnlyAsync(Phase13ApiFixture app, bool preserveHistory = false)
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SubscriptionCatalogOptions>>().CurrentValue;
            options.BookingOnly.Enabled = true; options.BookingOnly.MaximumSelectablePractices = 3;
            options.BookingOnly.PricingTiers = [new() { MinPracticeCount = 1, MaxPracticeCount = 3, MonthlyPrice = 300 }];
        }
        var id = await ReplaceAsync(app, selection: new("ONLINE_BOOKING_ONLY", "BOOKING_ONLY", BillingCycle.Monthly, SelectedPracticeCount: 2));
        if (!preserveHistory)
            await app.WithDbAsync(async db =>
            {
                var old = await db.DoctorSubscriptions.Where(s => s.DoctorId == app.DoctorId && s.Id != id).Select(s => s.Id).ToArrayAsync();
                db.RemoveRange(await db.SubscriptionFeatureSnapshots.Where(f => old.Contains(f.SubscriptionId)).ToArrayAsync());
                db.RemoveRange(await db.SubscriptionHistories.Where(h => old.Contains(h.SubscriptionId)).ToArrayAsync());
                db.RemoveRange(await db.DoctorSubscriptions.Where(s => old.Contains(s.Id)).ToArrayAsync());
                await db.SaveChangesAsync();
            });
        return id;
    }

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
        var sameSeries = await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId && p.SubscriptionKind == terms.Value.Kind && p.ProductId == terms.Value.ProductId)
            .OrderByDescending(p => p.CreatedOnUtc).Select(p => (Guid?)p.SubscriptionContractId).FirstOrDefaultAsync(ct);
        var selected = lifecycle.Select(app.DoctorId, terms.Value, sameSeries).Value;
        if (activate) selected.Activate(app.Clock.UtcNow, (await db.SuperAdmins.SingleAsync(a => a.IsRootSuperAdmin, ct)).ApplicationUserId);
        if (activate && kind == SubscriptionKind.Trial) await lifecycle.ScheduleRemindersAsync(selected, ct);
        foreach (var allocation in await db.PracticeSubscriptionAllocations.Where(a => a.ReleasedAtUtc == null && db.DoctorPractices.Any(p => p.Id == a.DoctorPracticeId && p.DoctorId == app.DoctorId)).ToArrayAsync(ct))
            allocation.Release(app.Clock.UtcNow, app.DoctorUserId, "Test fixture replacement");
        await db.SaveChangesAsync(ct);
        foreach (var practice in await db.DoctorPractices.Where(p => p.DoctorId == app.DoctorId).ToArrayAsync(ct))
            db.Add(new PracticeSubscriptionAllocation(practice.Id, selected.SubscriptionContractId, app.Clock.UtcNow, app.DoctorUserId));
        await db.SaveChangesAsync(ct); return selected.Id;
    }
}
