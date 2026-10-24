using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase16FamilyFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16FamilyHardeningTests
{
    [Fact]
    public async Task Sql_two_hosts_serialize_duplicate_open_cases_competing_reviews_withdraw_and_new_reads()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(true);
        await using var secondHost = f.App.WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.PostConfigure<FamilyMedicalPolicyOptions>(ConfigurePolicy); s.PostConfigure<MedicalRecordOptions>(o => o.GuardianSharingEnabled = true);
        }));
        using var fatherB = secondHost.CreateClient(); fatherB.DefaultRequestHeaders.Authorization = f.Father.DefaultRequestHeaders.Authorization;
        using var adminB = secondHost.CreateClient(); adminB.DefaultRequestHeaders.Authorization = f.Admin.DefaultRequestHeaders.Authorization;
        using var motherB = secondHost.CreateClient(); motherB.DefaultRequestHeaders.Authorization = f.Mother.DefaultRequestHeaders.Authorization;
        var submissions = await Task.WhenAll(SubmitAsync(f.Father, f.ChildId, f.App.OtherPatientId), SubmitAsync(fatherB, f.ChildId, f.App.OtherPatientId));
        Assert.Single(submissions, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(submissions, r => r.StatusCode == HttpStatusCode.Conflict);
        var request = await JsonAsync(submissions.Single(r => r.StatusCode == HttpStatusCode.Created)); foreach (var response in submissions) response.Dispose();
        var id = request.GetProperty("requestId").GetGuid(); var body = new { rowVersion = request.GetProperty("rowVersion").GetString(), basisReference = "TEST-ONLY-restriction-mandate", reason = "Reject" };
        var decisions = await Task.WhenAll(PostAsync(f.Admin, Inbox + $"/{id}/approve", body), PostAsync(adminB, Inbox + $"/{id}/reject", body));
        Assert.Single(decisions, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(decisions, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var r in decisions) r.Dispose();
        var decided = await GetAsync(f.Father, Own + $"/{id}");
        if (decided.GetProperty("status").GetString() == "Approved")
        { using var denied = await motherB.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        else await GetAsync(motherB, f.Record() + "/overview");
        using var next = await SubmitAsync(f.Father, f.SecondChildId, f.App.OtherPatientId); Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        request = await JsonAsync(next); id = request.GetProperty("requestId").GetGuid();
        var row = new { rowVersion = request.GetProperty("rowVersion").GetString(), basisReference = "TEST-ONLY-restriction-mandate" };
        var race = await Task.WhenAll(PostAsync(f.Father, Own + $"/{id}/withdraw", row), PostAsync(adminB, Inbox + $"/{id}/approve", row));
        Assert.Single(race, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(race, r => r.StatusCode == HttpStatusCode.Conflict); foreach (var r in race) r.Dispose();
        await f.App.WithDbAsync(async db => {
            Assert.Equal(2, await db.Set<FamilyMedicalAccessRequest>().CountAsync());
            var approvedCount = await db.Set<FamilyMedicalAccessRequest>().CountAsync(r => r.Status == FamilyMedicalRequestStatus.Approved);
            Assert.Equal(4 + approvedCount, await db.Set<FamilyMedicalAccessRequestHistory>().CountAsync());
            Assert.Equal(approvedCount, await db.Set<FamilyMedicalAccessRequestHistory>().CountAsync(h => h.Action == "RestrictionGroundReviewed" && h.ReasonOrMessage == "TEST-ONLY-restriction-mandate"));
            var indexes = db.Model.FindEntityType(typeof(FamilyMedicalAccessRequest))!.GetIndexes();
            Assert.Contains(indexes, i => i.IsUnique && i.GetFilter() == "[Status] IN (1,2)");
        });
    }
    [Fact]
    public async Task Sql_migration_rehearsal_backfills_only_pending_pairs_preserves_patients_and_seed_roles()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(true, false, adultRole: FamilyMemberRole.Guardian);
        await f.App.WithDbAsync(async db => {
            var patients = await db.Patients.OrderBy(p => p.Id).Select(p => new { p.Id, p.NameAr, p.DateOfBirth }).ToArrayAsync();
            // Start from a fresh legacy schema rather than reversing the non-reversible archive cutover.
            await SqlLegacyMigrationRehearsal.RunAsync(db, "20261012103620_Phase16MedicalRecordAudits", async migrated =>
            {
                var reviews = await migrated.Set<FamilyMedicalEligibilityReview>().AsNoTracking().ToArrayAsync(); Assert.Equal(4, reviews.Length);
                Assert.All(reviews, r => Assert.Equal(FamilyMedicalEligibilityStatus.PendingReview, r.Status));
                Assert.Empty(await migrated.Set<FamilyMedicalAccessRestriction>().ToArrayAsync());
                Assert.Equal(patients, await migrated.Patients.OrderBy(p => p.Id).Select(p => new { p.Id, p.NameAr, p.DateOfBirth }).ToArrayAsync());
            });
            var type = typeof(Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
            var counts = (await db.Permissions.CountAsync(), await db.RolePermissions.CountAsync());
            await using var scope = f.App.Services.CreateAsyncScope(); var seeder = scope.ServiceProvider.GetRequiredService(type);
            await (Task)type.GetMethod("SeedAsync")!.Invoke(seeder, [TestContext.Current.CancellationToken])!;
            await (Task)type.GetMethod("SeedAsync")!.Invoke(seeder, [TestContext.Current.CancellationToken])!;
            Assert.Equal(counts, (await db.Permissions.CountAsync(), await db.RolePermissions.CountAsync()));
            foreach (var role in new[] { SystemRoleIds.Doctor, SystemRoleIds.Reception, SystemRoleIds.DrugCatalogManager, SystemRoleIds.MedicalCatalogManager, SystemRoleIds.Patient })
                Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleId == role && r.PermissionId == SystemPermissionIds.For(PermissionNames.FamilyMedicalAccessRequestsDecide)));
            Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleId == SystemRoleIds.SuperAdmin && r.PermissionId == SystemPermissionIds.For(PermissionNames.MedicalRecordsViewDependent)));
            // Real database filtered uniqueness cannot be bypassed by an independent writer.
            var duplicate = FamilyMedicalEligibilityReview.Pending(await db.FamilyMembers.SingleAsync(m => m.PatientId == f.App.PatientId), await db.FamilyMembers.SingleAsync(m => m.PatientId == f.ChildId), f.App.Clock.UtcNow);
            db.Add(duplicate); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    [Fact]
    public async Task All_21_new_routes_and_existing_dependent_contracts_are_discovered_in_OpenAPI()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync();
        using var client = f.App.CreateClient(); var spec = await GetAsync(client, "/swagger/v1/swagger.json"); var paths = spec.GetProperty("paths");
        var routes = new List<(string Path, string Method)> { (Own, "post"), (Own + "/mine", "get"), (Own + "/{requestId}", "get"),
            (Own + "/{requestId}/history", "get"), (Own + "/{requestId}/documents/{documentId}/content", "get"),
            (Own + "/{requestId}/withdraw", "post"), (Own + "/{requestId}/resubmit", "post"),
            ("/api/v1/families/mine/dependents/{childPatientId}/medical-access", "get"), (Inbox + "/summary", "get"), (Inbox, "get"),
            (Inbox + "/{requestId}", "get"), (Inbox + "/{requestId}/documents/{documentId}/content", "get"), (Inbox + "/{requestId}/history", "get"),
            (Eligibility + "/reviews", "get"), (Eligibility + "/{reviewId}", "get") };
        foreach (var action in new[] { "approve", "reject", "request-modification" }) routes.Add((Inbox + "/{requestId}/" + action, "post"));
        foreach (var action in new[] { "approve", "reject", "revoke" }) routes.Add((Eligibility + "/{reviewId}/" + action, "post"));
        Assert.Equal(21, routes.Count);
        foreach (var route in routes)
        { var responses = paths.GetProperty(route.Path).GetProperty(route.Method).GetProperty("responses"); foreach (var code in new[] { "401", "404", "422", "429" }) Assert.True(responses.TryGetProperty(code, out _), route.Path + " " + code); }
        var schemas = spec.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("FamilyMemberResponse").GetProperty("properties").TryGetProperty("medicalAccess", out _));
        var decision = schemas.GetProperty("FamilyMedicalDecisionBody").GetProperty("properties");
        foreach (var field in new[] { "rowVersion", "reason", "message", "decisionNote", "policyVersion", "basisReference", "authorityRelationshipRequestId" })
            Assert.True(decision.TryGetProperty(field, out _), field);
        Assert.True(schemas.GetProperty("FamilyMedicalEligibilityResponse").GetProperty("properties").TryGetProperty("authorityRelationshipRequestId", out _));
        Assert.Equal(1, (int)FamilyRelationshipRequestType.CreateFamily); Assert.Equal(2, (int)FamilyRelationshipRequestType.AddFamilyMember);
        Assert.Equal(3, (int)FamilyRelationshipRequestType.VerifyParentChild); Assert.Equal(4, (int)FamilyRelationshipRequestType.VerifyGuardianChild);
        Assert.DoesNotContain("mediaKey", schemas.GetProperty("FamilyMedicalDocumentResponse").GetRawText());
    }
}
