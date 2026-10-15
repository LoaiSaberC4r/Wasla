using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16HardeningTests
{
    private const string Inbox = "/api/v1/doctors/me/medical-record-access-requests";
    [Fact]
    public async Task Sql_parallel_create_is_unique_and_approval_vs_completion_cannot_leave_effective_access()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var targetId = source.GetProperty("medicalEncounterId").GetGuid();
        await app.CompleteAsync(owner, source); await HideAsync(app, owner, targetId);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        await using var nodeB = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<MedicalRecordOptions>(o =>
            {
                o.CrossDoctorSharingEnabled = true;
                o.OpaqueEntryKey = app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.OpaqueEntryKey;
            })));
        using var secondDoctor = nodeB.CreateClient(); secondDoctor.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
        var hidden = (await GetAsync(doctor, url + "/timeline")).GetProperty("items")[0];
        var body = new { hiddenEncounterToken = hidden.GetProperty("hiddenEncounterToken").GetString(), reason = "Reason" };
        var responses = await Task.WhenAll(PostAsync(doctor, url + "/access-requests", body), PostAsync(secondDoctor, url + "/access-requests", body));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var request = await JsonAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Created)); foreach (var r in responses) r.Dispose();
        var id = request.GetProperty("requestId").GetGuid();
        var eUrl = $"/api/v1/doctors/me/practices/{app.OtherPracticeId}/encounters/{treating.Encounter}";
        var e = await GetAsync(doctor, eUrl);
        using var notes = await doctor.PatchAsJsonAsync(eUrl + "/clinical-notes", new { clinicalNotes = "Treatment finished", rowVersion = e.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken); e = await JsonAsync(notes);
        var ticketUrl = $"/api/v1/practices/{app.OtherPracticeId}/tickets/{treating.Ticket}";
        var ticket = await GetAsync(doctor, ticketUrl); var review = await GetAsync(owner, Inbox + $"/{id}");
        var race = await Task.WhenAll(PostAsync(owner, Inbox + $"/{id}/approve", new { rowVersion = review.GetProperty("rowVersion").GetString() }),
            PostAsync(secondDoctor, ticketUrl + "/complete", new { ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = e.GetProperty("rowVersion").GetString() }));
        Assert.Contains(race[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }); Assert.Equal(HttpStatusCode.OK, race[1].StatusCode);
        foreach (var r in race) r.Dispose();
        using (var denied = await doctor.GetAsync(url + $"/encounters/{targetId}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var expired = await GetAsync(doctor, url + $"/access-requests/{id}"); Assert.Equal("Expired", expired.GetProperty("effectiveStatus").GetString()); Assert.False(expired.TryGetProperty("grantedEncounterId", out _));
        await app.WithDbAsync(async db =>
        {
            Assert.Single(await db.Set<HiddenEncounterAccessRequest>().ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(HiddenAccessRequestStatus.Expired, (await db.Set<HiddenEncounterAccessRequest>().SingleAsync(TestContext.Current.CancellationToken)).Status);
            Assert.Equal(1, await db.Set<HiddenEncounterAccessRequestHistory>().CountAsync(h => h.RequestId == id && h.Action == "Expired", TestContext.Current.CancellationToken));
        });
    }
    [Theory]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    public async Task P16_04_05_10_11_late_official_media_inherits_visibility_and_revoke_invalidates_new_transfers(string kind, bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var doctor = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var targetId = source.GetProperty("medicalEncounterId").GetGuid();
        var order = await AddAsync(app, owner, targetId, kind, "Late official output"); var orderId = order.GetProperty("requestId").GetGuid(); var itemId = order.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        await CompleteAsync(app, owner, source, kind == "lab" ? order : null, kind == "radiology" ? order : null); await HideAsync(app, owner, targetId);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        // This official result is issued after the original completed encounter became Hidden.
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(2);
        order = await GetAsync(owner, Url(kind, $"-requests/{orderId}"));
        var published = await OkAsync(await UploadAsync(owner, Url(kind, $"-requests/{orderId}/results"), kind, [itemId], token: order.GetProperty("rowVersion").GetString()));
        var result = published.GetProperty("result"); var rid = result.GetProperty("resultId").GetGuid(); var attachment = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        var route = $"/items/{kind}-result/{rid}";
        var personal = await GetAsync(patient, "/api/v1/patients/me/medical-record" + route); Assert.Equal("Official", personal.GetProperty("status").GetString());
        Assert.DoesNotContain("privateMediaKey", personal.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using (var content = await patient.GetAsync("/api/v1/patients/me/medical-record" + route + $"/attachments/{attachment}/content", TestContext.Current.CancellationToken))
        { Assert.Equal(HttpStatusCode.OK, content.StatusCode); Assert.True(content.Headers.CacheControl?.NoStore); Assert.Equal("application/pdf", content.Content.Headers.ContentType?.MediaType); }
        using (var denied = await doctor.GetAsync(url + route, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await doctor.GetAsync(Url(kind, $"-results/{rid}"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var hidden = (await GetAsync(doctor, url + "/timeline")).GetProperty("items")[0]; Assert.Equal(1, (await GetAsync(doctor, url + "/timeline")).GetProperty("items").GetArrayLength());
        var requested = await OkAsync(await PostAsync(doctor, url + "/access-requests", new { hiddenEncounterToken = hidden.GetProperty("hiddenEncounterToken").GetString(), reason = "Review prior output" }));
        var requestId = requested.GetProperty("requestId").GetGuid(); var approved = await OkAsync(await PostAsync(owner, Inbox + $"/{requestId}/approve", new { rowVersion = requested.GetProperty("rowVersion").GetString() }));
        var shared = await GetAsync(doctor, url + route); Assert.Equal("Official", shared.GetProperty("status").GetString());
        var download = url + route + $"/attachments/{attachment}/content";
        // SQLite fixtures use one shared in-memory connection and cannot model independent SQL Server transactions.
        var race = sql
            ? await Task.WhenAll(doctor.GetAsync(download, TestContext.Current.CancellationToken), PostAsync(owner, Inbox + $"/{requestId}/revoke", new { rowVersion = approved.GetProperty("rowVersion").GetString() }))
            : new[] { await doctor.GetAsync(download, TestContext.Current.CancellationToken), await PostAsync(owner, Inbox + $"/{requestId}/revoke", new { rowVersion = approved.GetProperty("rowVersion").GetString() }) };
        Assert.Contains(race[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }); Assert.Equal(HttpStatusCode.OK, race[1].StatusCode); foreach (var r in race) r.Dispose();
        using (var denied = await doctor.GetAsync(download, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var wrong = await patient.GetAsync("/api/v1/patients/me/medical-record" + route + $"/attachments/{Guid.NewGuid()}/content", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
    }
    [Fact]
    public async Task Sql_audit_failure_releases_no_record_and_seeding_is_repeatable_without_admin_clinical_permissions()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var patient = await app.ClientAsync("patient");
        await using (var seedScope = app.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext>();
            var permissionCount = await db.Permissions.CountAsync(TestContext.Current.CancellationToken);
            var mappingCount = await db.RolePermissions.CountAsync(TestContext.Current.CancellationToken);
            var type = typeof(Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
            var seeder = seedScope.ServiceProvider.GetRequiredService(type);
            await (Task)type.GetMethod("SeedAsync")!.Invoke(seeder, [TestContext.Current.CancellationToken])!;
            await (Task)type.GetMethod("SeedAsync")!.Invoke(seeder, [TestContext.Current.CancellationToken])!;
            Assert.Equal(permissionCount, await db.Permissions.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(mappingCount, await db.RolePermissions.CountAsync(TestContext.Current.CancellationToken));
        }
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalRecordAccessAudits] ADD CONSTRAINT [CK_Phase16AuditFailure] CHECK (1=0)", TestContext.Current.CancellationToken));
        using (var failed = await patient.GetAsync("/api/v1/patients/me/medical-record/overview", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode); var body = await failed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("timeline", body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("nameAr", body, StringComparison.OrdinalIgnoreCase);
        }
        await app.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [MedicalRecordAccessAudits] DROP CONSTRAINT [CK_Phase16AuditFailure]", TestContext.Current.CancellationToken);
            Assert.Empty(await db.Set<MedicalRecordAccessAudit>().ToArrayAsync(TestContext.Current.CancellationToken));
            foreach (var role in new[] { SystemRoleIds.SuperAdmin, SystemRoleIds.Reception, SystemRoleIds.MedicalCatalogManager, SystemRoleIds.DrugCatalogManager })
                Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleId == role && r.PermissionId == SystemPermissionIds.For(PermissionNames.MedicalRecordsViewOwn), TestContext.Current.CancellationToken));
        });
        Assert.True((await GetAsync(patient, "/api/v1/patients/me/medical-record/overview")).GetProperty("capabilities").GetProperty("canView").GetBoolean());
    }
    [Fact]
    public async Task Sql_legacy_migration_keeps_old_encounters_hidden_with_baseline_history_and_current_clients_visible()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(true); using var owner = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid(); await app.CompleteAsync(owner, source);
        await app.WithDbAsync(async db =>
        {
            var ct = TestContext.Current.CancellationToken;
            var previous = db.Database.GetMigrations().TakeWhile(m => !m.Contains("Phase16", StringComparison.Ordinal)).Last();
            // Rehearse on a fresh legacy copy. The approved archive cutover has no automatic Down path.
            await SqlLegacyMigrationRehearsal.RunAsync(db, previous, async migrated =>
            {
            var legacy = migrated;
            var e = await legacy.MedicalEncounters.AsNoTracking().SingleAsync(e => e.Id == id, ct);
            Assert.Equal(EncounterVisibility.Hidden, e.Visibility); Assert.Equal(0, e.VisibilityRevision);
            Assert.Equal("Clinical documentation", e.ClinicalNotes);
            var h = await legacy.Set<EncounterVisibilityHistory>().SingleAsync(h => h.MedicalEncounterId == id, ct);
            Assert.Equal("LegacyMigration", h.Source); Assert.Null(h.OldVisibility); Assert.Null(h.ActorApplicationUserId);
            });
        });
        var next = await app.StartAsync(owner, await app.WalkInAsync(reception));
        Assert.Equal("Visible", (await GetAsync(owner, app.EncounterUrl(next.GetProperty("medicalEncounterId").GetGuid()))).GetProperty("visibility").GetString());
    }
}
