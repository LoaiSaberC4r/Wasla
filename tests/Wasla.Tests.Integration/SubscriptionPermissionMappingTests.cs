using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Seeding;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionPermissionMappingTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Seeder_repairs_original_PR36_mappings_then_replays_without_changing_terms_or_permission_ids(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        string[] accepted = []; Guid[] permissionIds = [];
        await app.WithDbAsync(async db =>
        {
            accepted = await db.DoctorSubscriptions.OrderBy(s => s.Id).Select(s => s.TermsSnapshotJson).ToArrayAsync();
            permissionIds = await db.Permissions.OrderBy(p => p.Name).Select(p => p.Id).ToArrayAsync();
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Wasla.sln"))) root = root.Parent;
            var original = File.ReadAllLines(Path.Combine(root!.FullName, "docs", "subscriptions", "permission-coverage.csv")).Skip(1)
                .Select(line => line.Split(',')).Where(columns => columns[7].Length != 0).ToArray();
            var features = await db.CommercialFeatures.ToDictionaryAsync(f => f.Code);
            db.RemoveRange(await db.FeaturePermissionMappings.ToArrayAsync()); await db.SaveChangesAsync();
            foreach (var columns in original)
                db.Add(new FeaturePermissionMapping(SubscriptionCatalogSeeder.StableId("permission:" + columns[0]), features[columns[7]].Id,
                    SystemPermissionIds.For(columns[0]), Enum.Parse<FeatureAccessLevel>(columns[8]), app.Clock.UtcNow));
            await db.SaveChangesAsync();
        });
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<SubscriptionCatalogSeeder>();
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(accepted, await db.DoctorSubscriptions.OrderBy(s => s.Id).Select(s => s.TermsSnapshotJson).ToArrayAsync());
            Assert.Equal(permissionIds, await db.Permissions.OrderBy(p => p.Name).Select(p => p.Id).ToArrayAsync());
            var mappings = await (from m in db.FeaturePermissionMappings join p in db.Permissions on m.PermissionId equals p.Id
                                  join f in db.CommercialFeatures on m.FeatureId equals f.Id
                                  select new { m.Id, p.Name, f.Code, m.RequiredAccessLevel }).ToArrayAsync();
            var expected = CommercialPermissionCatalog.All.Where(p => p.Value.Category == SubscriptionPermissionCategory.CommercialFeature).ToArray();
            Assert.Equal(expected.Length, mappings.Length);
            Assert.Equal(mappings.Length, mappings.Select(m => m.Name).Distinct().Count());
            foreach (var (permission, rule) in expected)
            {
                var mapping = Assert.Single(mappings, m => m.Name == permission);
                Assert.Equal(SubscriptionCatalogSeeder.StableId("permission:" + permission), mapping.Id);
                Assert.Equal(rule.FeatureCode, mapping.Code); Assert.Equal(rule.RequiredAccess, mapping.RequiredAccessLevel);
            }
            Assert.Equal(2, await db.CommercialProducts.CountAsync()); Assert.Equal(5, await db.CommercialPlans.CountAsync());
            Assert.Equal(28, await db.CommercialFeatures.CountAsync());
            // Retiring catalog metadata must not revoke an already accepted immutable feature snapshot.
            var ticketFeature = await db.CommercialFeatures.SingleAsync(f => f.Code == "TICKET_QUEUE");
            db.Entry(ticketFeature).Property(f => f.IsActive).CurrentValue = false; await db.SaveChangesAsync();
        });
        using var doctor = await app.ClientAsync("doctor");
        using var retained = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Missing_archive_mapping_or_feature_snapshot_denies_direct_service_and_HTTP_without_data(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor");
        await app.WithDbAsync(db => db.FeaturePermissionMappings.Where(m => m.PermissionId == SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsPractice)).ExecuteDeleteAsync());
        await using (var direct = app.Services.CreateAsyncScope())
        {
            var user = await direct.ServiceProvider.GetRequiredService<Wasla.Application.Persistence.IWaslaDataStore>()
                .GetAccessSnapshotAsync(app.DoctorUserId, TestContext.Current.CancellationToken);
            Assert.False(await direct.ServiceProvider.GetRequiredService<ISubscriptionPermissionGuard>()
                .AllowedAsync(user!, Phase18PartTwoPermissionNames.TicketsPractice, TestContext.Current.CancellationToken));
        }
        using var denied = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("items", body); Assert.DoesNotContain("patientName", body); Assert.DoesNotContain("SqlException", body);
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SubscriptionCatalogSeeder>().SeedAsync(TestContext.Current.CancellationToken);
        await app.WithDbAsync(db => db.SubscriptionFeatureSnapshots.Where(f => f.FeatureCodeSnapshot == "TICKET_QUEUE" && db.DoctorSubscriptions.Any(s => s.Id == f.SubscriptionId && s.DoctorId == app.DoctorId)).ExecuteDeleteAsync());
        using var noSnapshot = await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, noSnapshot.StatusCode);
    }
}
