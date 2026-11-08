using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Domain.Families;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class DependentArchivedTicketTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Archived_child_ticket_is_read_only_and_requires_current_pair_plus_archive_permission()
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(true); var f = family.App;
        using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var completed = await f.CompleteAsync(doctor, await f.StartAsync(doctor, await f.WalkInAsync(reception, patientId: family.SecondChildId)));
        var id = completed.Ticket.GetProperty("ticketId").GetGuid(); var path = $"/api/v1/tickets/dependents/{family.SecondChildId}/{id}";
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1); var flags = f.Services.GetRequiredService<IOptions<ArchiveOptions>>().Value; flags.Enabled = true; flags.ReadModelsVerified = true;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ArchiveOperationalDays>().ProcessAsync(TestContext.Current.CancellationToken);
            var moved = await scope.ServiceProvider.GetRequiredService<ArchiveEngine>().ProcessAsync(TestContext.Current.CancellationToken); Assert.Equal(1, moved!.ArchivedTickets);
        }
        var safe = await GetAsync(family.Father, path); Assert.True(safe.GetProperty("isArchived").GetBoolean()); Assert.False(safe.GetProperty("patientsAheadNowIsCurrent").GetBoolean()); Assert.Equal(0, safe.GetProperty("patientsAheadNow").GetInt32());
        foreach (var forbidden in new[] { "medicalEncounterId", "diagnoses", "prescriptionId", "paymentId", "refundId", "rowVersion", "canRefund" }) Assert.False(safe.TryGetProperty(forbidden, out _));
        using var wrongChild = await family.Father.GetAsync($"/api/v1/tickets/dependents/{family.ChildId}/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, wrongChild.StatusCode);
        await f.WithDbAsync(db => db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Patient && p.PermissionId == SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsOwn)).ExecuteDeleteAsync());
        using var revokedPermission = await family.Father.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, revokedPermission.StatusCode);
        await f.WithDbAsync(async db => { db.Add(new RolePermission(Guid.NewGuid(), SystemRoleIds.Patient, SystemPermissionIds.For(Phase18PartTwoPermissionNames.TicketsOwn))); await db.SaveChangesAsync(); });
        using var restored = await family.Father.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        await f.WithDbAsync(async db => { var proof = await db.Set<ParentChildOperationalAuthority>().SingleAsync(p => p.ParentPatientId == f.PatientId && p.ChildPatientId == family.SecondChildId && p.IsActive); var actor = await db.ApplicationUsers.SingleAsync(u => u.UserName == "admin"); proof.Revoke(actor.Id, f.Clock.UtcNow, "Reviewed revocation", Guid.NewGuid().ToString("N")); db.Add(new OperationalAuthorityHistory(proof.Id, actor.Id, "Revoked", f.Clock.UtcNow)); await db.SaveChangesAsync(); Assert.True(await db.TicketRegistry.AnyAsync(t => t.Id == id && t.IsArchived)); });
        using var revokedPair = await family.Father.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, revokedPair.StatusCode);
    }
}
