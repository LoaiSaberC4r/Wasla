using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
namespace Wasla.Tests.Integration;

public sealed class FamilyMigrationRehearsalTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Populated_pre_epic_families_ids_and_permissions_survive_upgrade_and_backup_restore_without_authority_backfill()
    {
        var supplied = Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING"); Assert.SkipWhen(string.IsNullOrWhiteSpace(supplied), "Disposable SQL Server configuration required.");
        var name = "WaslaFamilyUpgrade_" + Guid.NewGuid().ToString("N"); Assert.Matches("^WaslaFamilyUpgrade_[0-9a-f]{32}$", name);
        var builder = new SqlConnectionStringBuilder(supplied) { InitialCatalog = name }; var administrative = new SqlConnectionStringBuilder(supplied) { InitialCatalog = "master" };
        var opts = new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(builder.ConnectionString, s => s.CommandTimeout(120)).Options;
        string? backup = null; var created = false; var patients = new List<Guid>(); var requests = new List<Guid>(); var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc); var today = DateOnly.FromDateTime(now);
        try
        {
            await using (var db = new WaslaDbContext(opts))
            {
                await db.Database.GetService<IMigrator>().MigrateAsync("20261105111042_CommercialContractsAndPracticeAllocations", TestContext.Current.CancellationToken); created = true;
                var reviewer = ApplicationUser.Create(Guid.NewGuid(), "migration-reviewer", "reviewer@migration.test", null, "synthetic-password-hash", UserType.SuperAdmin, false, now).Value;
                var owner = ApplicationUser.Create(Guid.NewGuid(), "migration-parent", "parent@migration.test", null, "synthetic-password-hash", UserType.Patient, false, now).Value;
                db.AddRange(reviewer, owner);
                foreach (var permission in PermissionNames.All.Take(337)) db.Add(Permission.Create(SystemPermissionIds.For(permission), permission, true).Value);
                for (var i = 0; i < 2; i++)
                {
                    var parent = Patient.Create(Guid.NewGuid(), "والد سابق " + i, null, today.AddYears(-40), Gender.Male, null, null, null, null, null, today).Value;
                    var child = Patient.Create(Guid.NewGuid(), "طفل سابق " + i, null, today.AddYears(-8 - i), Gender.Male, null, null, null, null, null, today).Value;
                    db.AddRange(parent, child); patients.Add(parent.Id); patients.Add(child.Id);
                    if (i == 0) db.Add(PatientAccountLink.Create(Guid.NewGuid(), owner.Id, parent.Id, PatientAccountLinkSource.SelfRegistration, now).Value);
                    var family = Family.Create(Guid.NewGuid(), owner.Id).Value;
                    var request = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.CreateFamily, null, parent.Id, child.Id, FamilyMemberRole.Father, FamilyMemberRole.Child, owner.Id, now).Value;
                    Assert.True(request.AssignCreatedFamily(family.Id).IsSuccess); Assert.True(request.Approve(reviewer.Id, now).IsSuccess);
                    family.AddMember(Guid.NewGuid(), parent.Id, FamilyMemberRole.Father, now, reviewer.Id, request.Id); family.AddMember(Guid.NewGuid(), child.Id, FamilyMemberRole.Child, now, reviewer.Id, request.Id);
                    requests.Add(request.Id); db.AddRange(family, request);
                    db.Add(new FamilyRelationshipDocument(Guid.NewGuid(), request.Id, 1, FamilyRelationshipDocumentType.BirthCertificate, "legacy-private/proof-" + i + ".pdf", "proof.pdf", "application/pdf", 100, owner.Id, now));
                }
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                await using var master = new SqlConnection(administrative.ConnectionString); await master.OpenAsync(TestContext.Current.CancellationToken);
                await using var folder = master.CreateCommand(); folder.CommandText = "SELECT CONVERT(nvarchar(4000), COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')))"; var root = (string)(await folder.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
                backup = Path.Combine(root, name + ".bak"); await using var copy = master.CreateCommand(); copy.CommandTimeout = 120; copy.CommandText = $"BACKUP DATABASE [{name}] TO DISK=@path WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;"; copy.Parameters.AddWithValue("@path", backup); await copy.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }
            await Verify(); SqlConnection.ClearAllPools();
            await using (var master = new SqlConnection(administrative.ConnectionString))
            {
                await master.OpenAsync(TestContext.Current.CancellationToken); await using var restore = master.CreateCommand(); restore.CommandTimeout = 120;
                restore.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{name}] FROM DISK=@path WITH REPLACE, CHECKSUM; ALTER DATABASE [{name}] SET MULTI_USER;"; restore.Parameters.AddWithValue("@path", backup); await restore.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            await using (var db = new WaslaDbContext(opts)) await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await Verify();
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (created && System.Text.RegularExpressions.Regex.IsMatch(name, "^WaslaFamilyUpgrade_[0-9a-f]{32}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                await using var master = new SqlConnection(administrative.ConnectionString); await master.OpenAsync(CancellationToken.None); await using var drop = master.CreateCommand(); drop.CommandTimeout = 120; drop.CommandText = $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END"; await drop.ExecuteNonQueryAsync(CancellationToken.None);
            }
            if (backup is not null && Path.GetFileName(backup) == name + ".bak" && File.Exists(backup)) File.Delete(backup);
        }
        async Task Verify()
        {
            await using var db = new WaslaDbContext(opts); Assert.Equal(2, await db.Families.CountAsync()); Assert.Equal(4, await db.FamilyMembers.CountAsync());
            Assert.Equal(patients.Order(), (await db.Patients.Select(p => p.Id).ToArrayAsync()).Order()); Assert.Equal(requests.Order(), (await db.FamilyRelationshipRequests.Select(r => r.Id).ToArrayAsync()).Order());
            Assert.Equal(2, await db.FamilyRelationshipDocuments.CountAsync()); Assert.Single(await db.PatientAccountLinks.ToArrayAsync());
            Assert.Empty(await db.Set<ParentChildOperationalAuthority>().ToArrayAsync()); Assert.Empty(await db.Set<FamilyGovernanceCase>().ToArrayAsync()); Assert.Empty(await db.Set<PatientHistoricalDocument>().ToArrayAsync());
            var permissions = await db.Permissions.ToDictionaryAsync(p => p.Name, p => p.Id); Assert.Equal(337, permissions.Count); foreach (var permission in PermissionNames.All.Take(337)) Assert.Equal(SystemPermissionIds.For(permission), permissions[permission]);
        }
    }
}
