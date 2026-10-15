using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Domain.Archives;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18PartTwoApiTests
{
    [Fact]
    public async Task Existing_assignment_is_not_backfilled_and_frontend_permission_ids_support_explicit_grant_and_revoke()
    {
        await using var app=await Phase13ApiFixture.CreateAsync();using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");
        var addedIds=Phase18PartTwoPermissionNames.ReceptionDefaults.Select(SystemPermissionIds.For).ToArray();
        await app.WithDbAsync(db=>db.ReceptionPracticeAssignmentPermissions.Where(x=>x.AssignmentId==app.ReceptionAssignmentId&&addedIds.Contains(x.PermissionId)).ExecuteDeleteAsync());
        using(var scope=app.Services.CreateScope())
        {
            var type=typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
            await (Task)type.GetMethod("SeedAsync")!.Invoke(scope.ServiceProvider.GetRequiredService(type),[TestContext.Current.CancellationToken])!;
        }
        using var denied=await reception.GetAsync($"/api/v1/reception/practices/{app.PracticeId}/archives/tickets",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        var catalog=await GetAsync(doctor,"/api/v1/doctors/me/receptions/assignable-permissions");var chosen=catalog.EnumerateArray().Where(x=>Phase18PartTwoPermissionNames.ReceptionDefaults.Contains(x.GetProperty("code").GetString()!)).Select(x=>x.GetProperty("id").GetGuid()).ToArray();Assert.Equal(3,chosen.Length);
        var staff=(await GetAsync(doctor,"/api/v1/doctors/me/receptions")).EnumerateArray().Single(x=>x.GetProperty("userName").GetString()=="reception");var staffId=staff.GetProperty("id").GetGuid();var assignment=staff.GetProperty("assignments")[0];var assignmentId=assignment.GetProperty("id").GetGuid();var practiceId=assignment.GetProperty("doctorPracticeId").GetGuid();
        var original=assignment.GetProperty("permissions").EnumerateArray().Select(x=>x.GetProperty("id").GetGuid()).ToArray();var url=$"/api/v1/doctors/me/receptions/{staffId}/assignments/{assignmentId}";
        using var grant=await doctor.PutAsJsonAsync(url,new{permissionIds=original.Concat(chosen).ToArray(),rowVersion=assignment.GetProperty("rowVersion").GetString()},TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.OK,grant.StatusCode);
        var changed=await JsonAsync(grant);await GetAsync(reception,$"/api/v1/reception/practices/{practiceId}/archives/tickets");var contexts=await GetAsync(reception,"/api/v1/reception/practices");Assert.Contains(contexts[0].GetProperty("permissionCodes").EnumerateArray(),x=>x.GetString()==Phase18PartTwoPermissionNames.TicketsAssigned);
        using var revoke=await doctor.PutAsJsonAsync(url,new{permissionIds=original,rowVersion=changed.GetProperty("rowVersion").GetString()},TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.OK,revoke.StatusCode);
        using var revoked=await reception.GetAsync($"/api/v1/reception/practices/{practiceId}/archives/tickets",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.NotFound,revoked.StatusCode);
    }
    [Fact]
    public async Task Module_only_permissions_produce_ids_without_unrelated_rights_and_restricted_search_requires_explicit_grant_reason()
    {
        await using var app=await Phase13ApiFixture.CreateAsync();using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");using var patient=await app.ClientAsync("patient");using var admin=await app.ClientAsync("root");
        var ticket=await app.StartAsync(doctor,await app.WalkInAsync(reception));
        await app.WithDbAsync(async db=>
        {
            db.RolePermissions.Add(new(Guid.NewGuid(),SystemRoleIds.SuperAdmin,SystemPermissionIds.For(Phase18PartTwoPermissionNames.SensitiveRestricted)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using var missing=await PostAsync(admin,"/api/v1/admin/audit/sensitive-access/search",new{reason="",filters=new{pageSize=20}});Assert.Equal(HttpStatusCode.UnprocessableEntity,missing.StatusCode);
        using var restricted=await PostAsync(admin,"/api/v1/admin/audit/sensitive-access/search",new{reason="Synthetic incident",filters=new{practiceId=app.PracticeId,pageSize=20}});Assert.Equal(HttpStatusCode.OK,restricted.StatusCode);var safe=await JsonAsync(restricted);Assert.NotEmpty(safe.GetProperty("items").EnumerateArray());Assert.DoesNotContain("patientId",safe.GetRawText());Assert.DoesNotContain("clinicalNotes",safe.GetRawText());
        using var unrelated=await PostAsync(admin,"/api/v1/admin/audit/sensitive-access/search",new{reason="Synthetic incident",filters=new{practiceId=app.OtherPracticeId,pageSize=20}});Assert.Equal(HttpStatusCode.OK,unrelated.StatusCode);Assert.Empty((await JsonAsync(unrelated)).GetProperty("items").EnumerateArray());
        await app.WithDbAsync(async db=>
        {
            Assert.Contains(await db.ArchiveAuditEvents.ToArrayAsync(),x=>x.Action=="RestrictedAuditSearched"&&x.ReasonHash!=null&&x.ReasonHash!="Synthetic incident");
            var doctorIds=Phase18PartTwoPermissionNames.DoctorDefaults.Select(SystemPermissionIds.For).ToArray();var patientIds=Phase18PartTwoPermissionNames.PatientDefaults.Select(SystemPermissionIds.For).ToArray();var receptionIds=Phase18PartTwoPermissionNames.ReceptionDefaults.Select(SystemPermissionIds.For).ToArray();
            await db.RolePermissions.Where(x=>x.RoleId==SystemRoleIds.Doctor&&!doctorIds.Contains(x.PermissionId)||x.RoleId==SystemRoleIds.Patient&&!patientIds.Contains(x.PermissionId)||x.RoleId==SystemRoleIds.Reception&&!receptionIds.Contains(x.PermissionId)).ExecuteDeleteAsync();
            await db.ReceptionPracticeAssignmentPermissions.Where(x=>x.AssignmentId==app.ReceptionAssignmentId&&x.PermissionId==SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsAssigned)).ExecuteDeleteAsync();
        });
        await GetAsync(doctor,"/api/v1/doctors/me/archives/practices/options");await GetAsync(doctor,$"/api/v1/doctors/me/practices/{app.PracticeId}/audit/filter-options");
        var selector=await GetAsync(reception,"/api/v1/reception/archives/practices/options");Assert.Contains(selector.GetProperty("items").EnumerateArray(),x=>x.GetProperty("practiceId").GetGuid()==app.PracticeId);
        await GetAsync(reception,$"/api/v1/reception/practices/{app.PracticeId}/archives/reservations");
        await GetAsync(patient,"/api/v1/reservations/mine/archived/patients/options");using var deniedLive=await doctor.GetAsync(app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()),TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.Forbidden,deniedLive.StatusCode);
    }
    [Fact]
    public async Task Doctor_reception_patient_admin_producers_are_readable_and_platform_is_redacted()
    {
        await using var app=await Phase13ApiFixture.CreateAsync();using var doctor=await app.ClientAsync("doctor");using var reception=await app.ClientAsync("reception");using var patient=await app.ClientAsync("patient");using var admin=await app.ClientAsync("root");
        var options=await GetAsync(doctor,"/api/v1/doctors/me/archives/practices/options");Assert.Equal(app.PracticeId,options.GetProperty("items")[0].GetProperty("practiceId").GetGuid());
        var metadata=await GetAsync(doctor,"/api/v1/archives/metadata");Assert.NotEmpty(metadata.GetProperty("ticketStatuses").EnumerateArray());
        foreach(var url in new[]{ $"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets",$"/api/v1/doctors/me/practices/{app.PracticeId}/archives/reservations"})
        {var page=await GetAsync(doctor,url);Assert.False(page.GetProperty("hasMore").GetBoolean());Assert.Empty(page.GetProperty("items").EnumerateArray());}
        await GetAsync(reception,$"/api/v1/reception/practices/{app.PracticeId}/archives/tickets");await GetAsync(patient,"/api/v1/tickets/mine/archived");await GetAsync(patient,"/api/v1/reservations/mine/archived");
        var platform=await GetAsync(admin,"/api/v1/admin/archives/practices/options");Assert.NotEmpty(platform.GetProperty("items").EnumerateArray());
        using var cross=await doctor.GetAsync($"/api/v1/doctors/me/practices/{app.OtherPracticeId}/archives/tickets",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.NotFound,cross.StatusCode);
        await app.WithDbAsync(async db=>{var denied=await db.ArchiveAuditEvents.SingleAsync(x=>x.Action=="ArchiveLookupDenied");Assert.Null(denied.EntityId);Assert.Null(denied.DoctorPracticeId);Assert.Equal("Denied",denied.Outcome);});
        using var dependent=await patient.GetAsync($"/api/v1/tickets/mine/archived?patientId={app.OtherPatientId}",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.UnprocessableEntity,dependent.StatusCode);
        using var platformPatient=await admin.GetAsync($"/api/v1/admin/archives/tickets?patientId={app.PatientId}",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.UnprocessableEntity,platformPatient.StatusCode);
        await app.WithDbAsync(async db=>{var id=SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsAssigned);db.ReceptionPracticeAssignmentPermissions.RemoveRange(await db.ReceptionPracticeAssignmentPermissions.Where(x=>x.AssignmentId==app.ReceptionAssignmentId&&x.PermissionId==id).ToArrayAsync(TestContext.Current.CancellationToken));await db.SaveChangesAsync(TestContext.Current.CancellationToken);});
        using var revoked=await reception.GetAsync($"/api/v1/reception/practices/{app.PracticeId}/archives/tickets",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.NotFound,revoked.StatusCode);
    }
    [Fact]
    public async Task Health_produces_version_pause_replays_and_stale_request_conflicts_without_root_sensitive_bypass()
    {
        await using var app=await Phase13ApiFixture.CreateAsync();using var admin=await app.ClientAsync("root");
        var health=await GetAsync(admin,"/api/v1/admin/archives/operations/health");var version=health.GetProperty("control").GetProperty("rowVersion").GetString();Assert.False(health.GetProperty("enabled").GetBoolean());
        var body=new{reason="Synthetic maintenance",rowVersion=version};var key=Guid.NewGuid().ToString("N");
        using var paused=await PostAsync(admin,"/api/v1/admin/archives/operations/pause",body,key);Assert.Equal(HttpStatusCode.OK,paused.StatusCode);var pausedBody=await JsonAsync(paused);
        using var replay=await PostAsync(admin,"/api/v1/admin/archives/operations/pause",body,key);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(pausedBody.GetProperty("rowVersion").GetString(),(await JsonAsync(replay)).GetProperty("rowVersion").GetString());
        using var stale=await PostAsync(admin,"/api/v1/admin/archives/operations/resume",new{reason="Synthetic resume",rowVersion=version});Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        using var denied=await PostAsync(admin,"/api/v1/admin/audit/sensitive-access/search",new{reason="Synthetic case",filters=new{pageSize=20}});Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        await GetAsync(admin,"/api/v1/admin/archives/operations/filter-options");await GetAsync(admin,"/api/v1/admin/archives/operations/runs");await GetAsync(admin,"/api/v1/admin/archives/operations/exceptions");
    }
    [Fact]
    public async Task Audit_list_keys_resolve_are_stable_and_reception_scope_is_own_current_activity()
    {
        await using var app=await Phase13ApiFixture.CreateAsync();using var reception=await app.ClientAsync("reception");using var doctor=await app.ClientAsync("doctor");using var admin=await app.ClientAsync("root");
        await app.WalkInAsync(reception);var url=$"/api/v1/doctors/me/practices/{app.PracticeId}/audit/events";
        await GetAsync(doctor,$"/api/v1/doctors/me/practices/{app.PracticeId}/audit/filter-options");
        var page=await GetAsync(doctor,url);var key=page.GetProperty("items")[0].GetProperty("eventKey").GetString();Assert.NotNull(key);
        var detail=await GetAsync(doctor,url+"/"+key);Assert.Equal(key,detail.GetProperty("eventKey").GetString());
        var again=await GetAsync(doctor,url);Assert.Equal(key,again.GetProperty("items")[0].GetProperty("eventKey").GetString());
        using var invalid=await doctor.GetAsync(url+"/invalid-key",TestContext.Current.CancellationToken);Assert.Equal(HttpStatusCode.NotFound,invalid.StatusCode);
        await GetAsync(reception,$"/api/v1/reception/practices/{app.PracticeId}/audit/filter-options");await GetAsync(reception,$"/api/v1/reception/practices/{app.PracticeId}/audit/my-activity");
        await GetAsync(admin,"/api/v1/admin/audit/filter-options");await GetAsync(admin,"/api/v1/admin/audit/events");
    }
    [Fact]
    public async Task Sql_atomic_move_preserves_finance_identity_history_and_authorized_producer_consumer_links()
    {
        await using var app=await Phase13ApiFixture.CreateAsync(true);using var reception=await app.ClientAsync("reception");using var doctor=await app.ClientAsync("doctor");using var patient=await app.ClientAsync("patient");using var admin=await app.ClientAsync("root");
        var ticket=await app.WalkInAsync(reception);var id=ticket.GetProperty("ticketId").GetGuid();
        using var cancel=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new{reason="Synthetic cancellation",rowVersion=ticket.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.OK,cancel.StatusCode);
        app.Clock.UtcNow=app.Clock.UtcNow.AddDays(1);
        using(var scope=app.Services.CreateScope())
        {var flags=scope.ServiceProvider.GetRequiredService<IOptions<ArchiveOptions>>().Value;flags.Enabled=true;flags.ReadModelsVerified=true;await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);var engine=scope.ServiceProvider.GetRequiredService<ArchiveEngine>();Assert.True(await engine.ReadyAsync(TestContext.Current.CancellationToken));var run=await engine.ProcessAsync(TestContext.Current.CancellationToken);Assert.NotNull(run);Assert.Equal(1,run.ArchivedTickets);}
        var list=await GetAsync(doctor,$"/api/v1/doctors/me/practices/{app.PracticeId}/archives/tickets");Assert.Equal(id,list.GetProperty("items")[0].GetProperty("ticketId").GetGuid());
        var detail=await GetAsync(doctor,list.GetProperty("items")[0].GetProperty("detailsRoute").GetString()!);Assert.True(detail.GetProperty("isArchived").GetBoolean());Assert.False(detail.GetProperty("operationalCapabilities").GetProperty("canCheckIn").GetBoolean());Assert.True(detail.GetProperty("canRefund").GetBoolean());Assert.NotEmpty(detail.GetProperty("timeline").EnumerateArray());
        await GetAsync(patient,$"/api/v1/tickets/mine/{id}");var safe=await GetAsync(admin,$"/api/v1/admin/archives/tickets/{id}");Assert.False(safe.TryGetProperty("patientId",out _));Assert.False(safe.TryGetProperty("paymentId",out _));
        await app.WithDbAsync(async db=>{Assert.False(await db.Tickets.AnyAsync(x=>x.Id==id,TestContext.Current.CancellationToken));Assert.True(await db.Set<ArchivedTicket>().AnyAsync(x=>x.Id==id,TestContext.Current.CancellationToken));Assert.True(await db.Payments.AnyAsync(x=>x.TicketId==id,TestContext.Current.CancellationToken));Assert.True(await db.TicketRegistry.AnyAsync(x=>x.Id==id&&x.IsArchived,TestContext.Current.CancellationToken));});
        using var mutation=await PostAsync(doctor,app.TicketUrl(id)+"/cancel",new{reason="Forbidden archived write",rowVersion=detail.GetProperty("rowVersion").GetString()});Assert.Equal(HttpStatusCode.NotFound,mutation.StatusCode);
    }
}
