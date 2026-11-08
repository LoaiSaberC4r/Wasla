using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Net;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Subscriptions;

namespace Wasla.Tests.Integration;

public sealed class CommercialV12MigrationRehearsalTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Existing_overcapacity_data_and_proven_origins_survive_cutover_and_backup_restore_rollforward()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        // SqlConnection removes the password from ConnectionString after it is opened.
        // Retain the original test configuration for both administrative connections.
        var masterConnection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING"))
            { InitialCatalog = "master" }.ConnectionString;
        app.Clock.UtcNow = DateTime.UtcNow;
        var periodId = await SubscriptionTestData.ReplaceAsync(app);
        Guid reservationId = default, ambiguousId = default; string[] accepted = []; int practices = 0; int payments = 0;
        await app.WithDbAsync(async db =>
        {
            for (var i = 0; i < 3; i++) db.Add(DoctorPractice.Create(Guid.NewGuid(), app.DoctorId, "Legacy " + i, null, 1, 1, 1, "Address", 30, 31).Value);
            var now = app.Clock.UtcNow; var scheduled = now.AddDays(1);
            var reservation = Reservation.Create(new ReservationCreationSnapshot(Guid.NewGuid(), "MIGRATION-PROOF", app.DoctorId, app.PracticeId,
                app.PatientId, app.SegmentId, app.ConsultationTypeId, ReservationBookingSource.Patient,
                (await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient")).Id, null, scheduled, DateTime.SpecifyKind(scheduled, DateTimeKind.Unspecified),
                DateOnly.FromDateTime(scheduled), "Africa/Cairo", 30, "Segment", null, 0, "NewConsultation", "Consultation", null, 300, null, now)).Value;
            reservationId = reservation.Id; db.Add(reservation);
            var ambiguous = Reservation.Create(new ReservationCreationSnapshot(Guid.NewGuid(), "MIGRATION-UNPROVEN", app.DoctorId, app.PracticeId,
                app.PatientId, app.SegmentId, app.ConsultationTypeId, ReservationBookingSource.Patient,
                (await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient")).Id, null, scheduled.AddDays(1), DateTime.SpecifyKind(scheduled.AddDays(1), DateTimeKind.Unspecified),
                DateOnly.FromDateTime(scheduled.AddDays(1)), "Africa/Cairo", 30, "Segment", null, 0, "NewConsultation", "Consultation", null, 300, null, now.AddDays(-60))).Value;
            ambiguousId = ambiguous.Id; db.Add(ambiguous);
            var admin = await db.SuperAdmins.SingleAsync(a => a.IsRootSuperAdmin);
            db.Add(new SubscriptionPayment(Guid.NewGuid(), periodId, SubscriptionPaymentType.Initial, SubscriptionPaymentMethod.Cash, 499, "EGP", now,
                admin.ApplicationUserId, null, null, "migration-proof-payment", now));
            await db.SaveChangesAsync();
            accepted = await db.DoctorSubscriptions.OrderBy(p => p.Id).Select(p => p.TermsSnapshotJson).ToArrayAsync();
            practices = await db.DoctorPractices.CountAsync(); payments = await db.SubscriptionPayments.CountAsync();
        });
        string? backup = null; string? databaseName = null;
        await app.WithDbAsync(async db =>
        {
            var migrator = db.Database.GetService<IMigrator>();
            var connection = (SqlConnection)db.Database.GetDbConnection();
            databaseName = connection.Database;
            Assert.StartsWith("WaslaPhase13Tests_", databaseName);
            // Rehearse the earlier commercial cutover without rolling back the
            // later forward-only family/source-history migrations. Those additive
            // tables and their migration history stay intact on this disposable DB.
            Assert.Empty(await db.Set<Wasla.Domain.Patients.PatientHistoricalDocument>().ToArrayAsync());
            Assert.Empty(await db.Set<Wasla.Domain.Families.ChildOnboardingRequest>().ToArrayAsync());
            var commercialRollback = migrator.GenerateScript("20261105111042_CommercialContractsAndPracticeAllocations", "20261102190031_CommercialSubscriptionFoundation");
            foreach (var batch in System.Text.RegularExpressions.Regex.Split(commercialRollback, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Where(b => !string.IsNullOrWhiteSpace(b)))
                await db.Database.ExecuteSqlRawAsync(batch, TestContext.Current.CancellationToken);
            await using var master = new SqlConnection(masterConnection); await master.OpenAsync(TestContext.Current.CancellationToken);
            await using var folderCommand = master.CreateCommand();
            folderCommand.CommandText = "SELECT CONVERT(nvarchar(4000), COALESCE(SERVERPROPERTY('InstanceDefaultBackupPath'), SERVERPROPERTY('InstanceDefaultDataPath')))";
            var folder = (string)(await folderCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
            backup = folder.TrimEnd('\\', '/') + (folder.Contains('\\') ? "\\" : "/") + "WaslaCommercialV12_" + Guid.NewGuid().ToString("N") + ".bak";
            await using var command = master.CreateCommand(); command.CommandTimeout = 120;
            command.CommandText = $"BACKUP DATABASE [{databaseName}] TO DISK=@path WITH COPY_ONLY, INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;";
            command.Parameters.AddWithValue("@path", backup); await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        });
        await VerifyAsync();
        // Rehearse restore to the previous schema, then repeat the forward migration.
        await app.WithDbAsync(async db =>
        {
            await db.Database.CloseConnectionAsync(); SqlConnection.ClearPool((SqlConnection)db.Database.GetDbConnection());
            await using var master = new SqlConnection(masterConnection); await master.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = master.CreateCommand(); command.CommandTimeout = 120;
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{databaseName}] FROM DISK=@path WITH REPLACE, CHECKSUM; ALTER DATABASE [{databaseName}] SET MULTI_USER;";
            command.Parameters.AddWithValue("@path", backup); await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            db.ChangeTracker.Clear();
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        });
        await VerifyAsync();
        if (File.Exists(backup)) File.Delete(backup);
        using var doctor = await app.ClientAsync("doctor"); using var admin = await app.ClientAsync("root");
        using var fifth = await PostAsync(doctor, "/api/v1/doctors/me/practices", new { nameAr = "Fifth", governorateId = 1, cityId = 1, areaId = 1, detailedAddress = "Address", latitude = 30, longitude = 31, subscriptionContractId = periodId });
        Assert.Equal(HttpStatusCode.Conflict, fifth.StatusCode);
        var details = await GetAsync(admin, "/api/v1/admin/subscriptions/" + periodId);
        using var noncompliant = await PostAsync(admin, $"/api/v1/admin/subscriptions/{periodId}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.Conflict, noncompliant.StatusCode);
        var allocation = (await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/subscription-allocation")).GetProperty("allocation");
        using var selection = await PostAsync(doctor, $"/api/v1/doctors/me/subscriptions/{periodId}/renewal-selection", new
        {
            rowVersion = details.GetProperty("rowVersion").GetString(),
            retainedAllocations = new[] { new { practiceId = app.PracticeId, allocationId = allocation.GetProperty("id").GetGuid(), receptionIds = Array.Empty<Guid>() } }
        });
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        using var compliant = await PostAsync(admin, $"/api/v1/admin/subscriptions/{periodId}/renew", new { rowVersion = details.GetProperty("rowVersion").GetString(), amountReceived = 499, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.OK, compliant.StatusCode);
        Assert.Equal(periodId, (await JsonAsync(compliant)).GetProperty("subscription").GetProperty("subscriptionContractId").GetGuid());
        await app.WithDbAsync(async db => Assert.Equal(practices, await db.DoctorPractices.CountAsync()));

        async Task VerifyAsync()
        {
            await app.WithDbAsync(async db =>
            {
                Assert.Equal(accepted, await db.DoctorSubscriptions.OrderBy(p => p.Id).Select(p => p.TermsSnapshotJson).ToArrayAsync());
                Assert.Equal(practices, await db.DoctorPractices.CountAsync()); Assert.Equal(payments, await db.SubscriptionPayments.CountAsync());
                Assert.True(await db.Reservations.AnyAsync(r => r.Id == reservationId));
                var period = await db.DoctorSubscriptions.SingleAsync(p => p.Id == periodId);
                var allocations = await db.PracticeSubscriptionAllocations.Where(a => a.SubscriptionContractId == period.SubscriptionContractId && a.ReleasedAtUtc == null).ToArrayAsync();
                Assert.Equal(4, allocations.Length); Assert.All(allocations, a => Assert.True(a.LegacyGrandfathered));
                var origin = await db.ReservationCommercialProvenances.SingleAsync(p => p.Id == reservationId);
                Assert.Equal(periodId, origin.SubscriptionId); Assert.True(origin.Grants("MEDICAL_ENCOUNTER", FeatureAccessLevel.Full));
                Assert.False(await db.SubscriptionMigrationIssues.AnyAsync(i => i.ResourceId == reservationId));
                Assert.True(await db.Reservations.AnyAsync(r => r.Id == ambiguousId));
                Assert.False(await db.ReservationCommercialProvenances.AnyAsync(p => p.Id == ambiguousId));
                Assert.True(await db.SubscriptionMigrationIssues.AnyAsync(i => i.ResourceId == ambiguousId && i.IssueCode == "OriginalEntitlementNotProven"));
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
                Assert.Single(await db.ReservationCommercialProvenances.Where(p => p.Id == reservationId).ToArrayAsync());
            });
        }
    }
}
