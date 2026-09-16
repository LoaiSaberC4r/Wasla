using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Domain.Reservations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class ReservationPersistenceTests
{
    private static WaslaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=WaslaReservationModel;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new WaslaDbContext(options);
    }

    [Fact]
    public void Reservation_model_has_rowversion_restrict_fks_and_filtered_uniqueness()
    {
        using var context = CreateContext();
        var reservation = context.Model.FindEntityType(typeof(Reservation))!;
        var rowVersion = reservation.FindProperty(nameof(Reservation.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.All(reservation.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Contains(reservation.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Reservations_Reference");
        Assert.Contains(reservation.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Reservations_ConsumingPracticeSlot" &&
            index.GetFilter() == "[Status] IN (1,5)");
        Assert.Contains(reservation.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Reservations_ActivePatientPracticeDate" &&
            index.GetFilter() == "[Status] = 1");

        var idempotency = context.Model.FindEntityType(typeof(ReservationIdempotencyRecord))!;
        Assert.Contains(idempotency.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_ReservationIdempotency_ActorOperationKey");
    }

    [Fact]
    public void Phase10_migration_generates_additive_reservation_schema_and_permission_backfill()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(
            "20260915085818_OptimizePublicDoctorSearchRanking",
            "20260916141909_Phase10ReservationLifecycle",
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE [Reservations]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [ReservationHistories]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [ReservationIdempotencyRecords]", script, StringComparison.Ordinal);
        Assert.Contains("[NoShowAfterPassedPatientsCount] int NOT NULL DEFAULT 3", script, StringComparison.Ordinal);
        Assert.Contains("[Status] IN (1,5)", script, StringComparison.Ordinal);
        Assert.Contains("[Status] = 1", script, StringComparison.Ordinal);
        Assert.Contains("'Reservations.ViewOwn'", script, StringComparison.Ordinal);
        Assert.Contains("'PracticeReservations.RestoreNoShow'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [DoctorPractices]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", script, StringComparison.Ordinal);
    }
}
