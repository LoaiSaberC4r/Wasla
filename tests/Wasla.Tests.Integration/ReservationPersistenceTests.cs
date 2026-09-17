using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Application.Persistence;
using Wasla.Domain.Reservations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Wasla.Api.Controllers;
using Wasla.Domain.Security;
using Wasla.Api.ProblemDetails;
using Wasla.Domain.Practices;
using System.Text.Json;

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
    public void Metadata_route_is_common_authenticated_while_patient_routes_remain_patient_only()
    {
        var metadataController = typeof(ReservationMetadataController);
        var metadataAuthorization = Assert.Single(metadataController
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Null(metadataAuthorization.Roles);
        Assert.Equal(
            "api/v{version:apiVersion}/reservations/metadata",
            Assert.Single(metadataController.GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>()).Template);

        var patientAuthorization = Assert.Single(typeof(ReservationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(SystemRoleNames.Patient, patientAuthorization.Roles);
    }

    [Fact]
    public void Future_reservation_conflict_maps_to_structured_problem_details()
    {
        var reservationId = Guid.NewGuid();
        var details = JsonSerializer.Serialize(new
        {
            AffectedCount = 1,
            Reservations = new[]
            {
                new
                {
                    ReservationId = reservationId,
                    ReservationReference = "WSL-R-ABC234",
                    PatientId = Guid.NewGuid(),
                    NameAr = "مريض",
                    NameEn = "Patient",
                    BusinessDate = new DateOnly(2026, 9, 20),
                    ScheduledTime = new TimeOnly(18, 20)
                }
            }
        });

        var result = new[] { DoctorPracticeErrors.FutureReservationsExist(details) }
            .ToWaslaActionProblem(TestContext.Current.CancellationToken);
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("DoctorPractice.FutureReservationsExist", problem.Extensions["code"]);
        Assert.Equal(1, problem.Extensions["affectedReservationsCount"]);
        var payload = JsonSerializer.Serialize(problem.Extensions["affectedReservations"]);
        Assert.Contains("\"patientNameAr\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"details\"", payload, StringComparison.OrdinalIgnoreCase);
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
        var history = context.Model.FindEntityType(typeof(ReservationHistory))!;
        Assert.All(history.GetForeignKeys(), foreignKey =>
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

    [Fact]
    public void Phase10_hardening_migration_protects_history_normalizes_permissions_and_adds_projection_outbox()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(
            "20260916141909_Phase10ReservationLifecycle",
            "20260916153802_Phase10Hardening",
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE [ReservationProjectionInvalidations]", script, StringComparison.Ordinal);
        Assert.Contains("ON DELETE NO ACTION", script, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM [ReceptionPracticeAssignmentPermissions]", script, StringComparison.Ordinal);
        Assert.Contains("000000000073", script, StringComparison.Ordinal);
        Assert.Contains("000000000094", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [ReservationHistories]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [Reservations]", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reservation_list_query_is_translatable_by_the_sql_server_provider()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer(
                "Server=translation-probe.invalid;Database=WaslaTranslationProbe;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .AddInterceptors(new TranslationProbeConnectionInterceptor())
            .Options;
        await using var context = new WaslaDbContext(options);
        var storeType = typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaDataStore",
            throwOnError: true)!;
        var store = (IWaslaDataStore)Activator.CreateInstance(
            storeType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [context],
            culture: null)!;

        await Assert.ThrowsAsync<TranslationProbeReachedConnectionException>(() =>
            store.ListReservationViewsAsync(
                [Guid.NewGuid()],
                doctorId: null,
                practiceId: null,
                status: null,
                bookingSource: null,
                fromDate: null,
                toDate: null,
                scheduledFromUtc: DateTime.UtcNow,
                scheduledBeforeUtc: null,
                segmentId: null,
                isLate: null,
                utcNow: DateTime.UtcNow,
                search: null,
                pageNumber: 1,
                pageSize: 20,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private sealed class TranslationProbeConnectionInterceptor : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<InterceptionResult>(
                new TranslationProbeReachedConnectionException());
    }

    private sealed class TranslationProbeReachedConnectionException : Exception;
}
