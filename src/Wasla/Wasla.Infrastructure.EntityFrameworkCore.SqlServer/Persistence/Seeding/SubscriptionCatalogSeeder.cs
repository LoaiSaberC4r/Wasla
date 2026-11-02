using System.Security.Cryptography;
using System.Text;
using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Seeding;

public sealed class SubscriptionCatalogSeeder(WaslaDbContext db, IDateTimeProvider clock)
{
    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("Wasla.Commercial.v1:" + key)).AsSpan(0, 16));
    public async Task SeedAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var (code, ar, en) in new[] { ("FULL_PLATFORM", "المنصة الكاملة", "Full Platform"), ("ONLINE_BOOKING_ONLY", "الحجز الإلكتروني فقط", "Online Booking Only") })
            if (!await db.CommercialProducts.AnyAsync(x => x.Code == code, ct)) db.Add(new CommercialProduct(StableId(code), code, ar, en, null, null, true, true, code == "FULL_PLATFORM" ? 0 : 1, now, null));
        await db.SaveChangesAsync(ct);
        var products = await db.CommercialProducts.ToDictionaryAsync(p => p.Code, ct);
        var planCodes = new[] { "STARTER", "PROFESSIONAL", "PREMIUM", "BOOKING_ONLY", "LEGACY_TRANSITION" };
        var arabic = new[] { "الأساسية", "الاحترافية", "المتميزة", "الحجز فقط", "الفترة الانتقالية" };
        for (var i = 0; i < planCodes.Length; i++)
        {
            var code = planCodes[i]; var productId = products[code == "BOOKING_ONLY" ? "ONLINE_BOOKING_ONLY" : "FULL_PLATFORM"].Id;
            if (!await db.CommercialPlans.AnyAsync(p => p.ProductId == productId && p.Code == code, ct))
                db.Add(new CommercialPlan(StableId("plan:" + code), productId, code, arabic[i], code, null, null, true, code != "LEGACY_TRANSITION", i, now, null));
        }
        for (var i = 0; i < CommercialFeatures.All.Count; i++)
        {
            var f = CommercialFeatures.All[i];
            if (!await db.CommercialFeatures.AnyAsync(x => x.Code == f.Code, ct))
                db.Add(new CommercialFeature(StableId("feature:" + f.Code), f.Code, f.NameAr, f.NameEn, null, null, f.Group,
                    FeatureImplementationStatus.Implemented, FeatureReleaseReadiness.NeedsReview, true, true, i, now, null));
        }
        await db.SaveChangesAsync(ct);
        var features = await db.CommercialFeatures.ToDictionaryAsync(f => f.Code, ct);
        foreach (var plan in await db.CommercialPlans.ToArrayAsync(ct))
            foreach (var definition in CommercialFeatures.All)
            {
                var access = CommercialFeatures.SeedAccess(plan.Code, definition); var feature = features[definition.Code];
                if (access.HasValue && !await db.PlanFeatureEntitlements.AnyAsync(e => e.PlanId == plan.Id && e.FeatureId == feature.Id, ct))
                    db.Add(new PlanFeatureEntitlement(StableId($"entitlement:{plan.Code}:{feature.Code}"), plan.Id, feature.Id, access.Value, now));
            }
        foreach (var p in await db.Permissions.ToArrayAsync(ct))
        {
            var featureCode = CommercialFeatures.FeatureForPermission(p.Name);
            if (featureCode is not null && !await db.FeaturePermissionMappings.AnyAsync(m => m.PermissionId == p.Id, ct))
                db.Add(new FeaturePermissionMapping(StableId("permission:" + p.Name), features[featureCode].Id, p.Id, CommercialFeatures.RequiredAccess(p.Name), now));
        }
        await db.SaveChangesAsync(ct);
    }
}
