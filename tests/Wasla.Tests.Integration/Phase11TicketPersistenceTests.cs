using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using Wasla.Api.Controllers;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class Phase11TicketPersistenceTests
{
    private static WaslaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=WaslaPhase11Model;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new WaslaDbContext(options);
    }

    [Fact]
    public void Phase11_model_enforces_ticket_payment_and_queue_invariants()
    {
        using var context = CreateContext();
        var ticket = context.Model.FindEntityType(typeof(Ticket))!;
        var rowVersion = ticket.FindProperty(nameof(Ticket.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.All(ticket.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Contains(ticket.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Tickets_ReservationId" &&
            index.GetFilter() == "[ReservationId] IS NOT NULL");
        Assert.Contains(ticket.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Tickets_PracticeDateNumber");
        Assert.Contains(ticket.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Tickets_OpenPatientPractice" &&
            index.GetFilter() == "[Status] IN (1,2,3,4)");
        Assert.Contains(ticket.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_Tickets_OneCalledOrInProgressPerPractice" &&
            index.GetFilter() == "[Status] IN (2,3)");

        var history = context.Model.FindEntityType(typeof(TicketHistory))!;
        Assert.All(history.GetForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        var attempts = context.Model.FindEntityType(typeof(TicketCallAttempt))!;
        Assert.Contains(attempts.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() ==
                "UX_TicketCallAttempts_TicketCycleNumber");
        var payments = context.Model.FindEntityType(typeof(Payment))!;
        Assert.Contains(payments.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UX_Payments_TicketId");
    }

    [Fact]
    public void Phase11_configuration_defaults_and_permissions_are_authoritative()
    {
        Assert.Equal(30, DoctorPracticePlatformDefaults.CheckInOpenBeforeMinutes);
        Assert.Equal(3, DoctorPracticePlatformDefaults.TicketNoShowReturnFastTrackLimit);
        Assert.Contains(PermissionNames.PracticeTicketsCheckIn, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeTicketsForceCheckIn, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeTicketsRecordPayment, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeTicketsCall, PermissionNames.ReceptionAssignmentScoped);
        Assert.Contains(PermissionNames.PracticeTicketsCancel, PermissionNames.ReceptionAssignmentScoped);
    }

    [Fact]
    public void Phase11_migration_is_additive_and_contains_runtime_constraints()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(
            "20260916153802_Phase10Hardening",
            "20260917125705_Phase11TicketQueueRuntime",
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE [Tickets]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [TicketHistories]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [TicketCallAttempts]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [TicketDailyCounters]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [TicketIdempotencyRecords]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Payments]", script, StringComparison.Ordinal);
        Assert.Contains("[Status] IN (2,3)", script, StringComparison.Ordinal);
        Assert.Contains("[ReservationId] IS NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("[CheckInOpenBeforeMinutes] int NOT NULL DEFAULT 30", script, StringComparison.Ordinal);
        Assert.Contains("[TicketNoShowReturnFastTrackLimit] int NOT NULL DEFAULT 3", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [Reservations]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE [DoctorPractices]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase11_api_exposes_practice_and_private_patient_ticket_routes()
    {
        var practiceRoute = Assert.Single(typeof(PracticeTicketsController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>());
        Assert.Equal("api/v{version:apiVersion}/practices/{practiceId:guid}", practiceRoute.Template);
        var roles = Assert.Single(typeof(PracticeTicketsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()).Roles;
        Assert.Contains(SystemRoleNames.Doctor, roles, StringComparison.Ordinal);
        Assert.Contains(SystemRoleNames.Reception, roles, StringComparison.Ordinal);

        var patientRoute = Assert.Single(typeof(MyTicketsController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>());
        Assert.Equal("api/v{version:apiVersion}/tickets", patientRoute.Template);
        Assert.Equal(SystemRoleNames.Patient, Assert.Single(typeof(MyTicketsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()).Roles);
    }

    [Fact]
    public async Task Phase11_queue_projection_orders_server_side_and_calculates_patients_ahead()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new WaslaDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA foreign_keys=OFF;", TestContext.Current.CancellationToken);

        var now = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc);
        var doctor = Doctor.Create(
            Guid.NewGuid(), Guid.NewGuid(), "طبيب", "Doctor",
            new DateOnly(1980, 1, 1), Gender.Male, null,
            "front", "back", "card", null, new DateOnly(2026, 9, 17)).Value;
        doctor.Approve("123", Guid.NewGuid(), now);
        var practice = DoctorPractice.Create(
            Guid.NewGuid(), doctor.Id, "عيادة", "Practice",
            1, 1, 1, "Address", 30, 31, Guid.NewGuid()).Value;
        practice.Activate(true, true, true, Guid.NewGuid());
        var patients = new[]
        {
            Patient.Create(Guid.NewGuid(), "باسم", "Bassem", new DateOnly(1990, 1, 1),
                Gender.Male, null, null, null, null, null, new DateOnly(2026, 9, 17)).Value,
            Patient.Create(Guid.NewGuid(), "أحمد", "Ahmed", new DateOnly(1991, 1, 1),
                Gender.Male, null, null, null, null, null, new DateOnly(2026, 9, 17)).Value,
            Patient.Create(Guid.NewGuid(), "سامي", "Sami", new DateOnly(1992, 1, 1),
                Gender.Male, null, null, null, null, null, new DateOnly(2026, 9, 17)).Value
        };
        context.Doctors.Add(doctor);
        context.DoctorPractices.Add(practice);
        context.Patients.AddRange(patients);
        var businessDate = new DateOnly(2026, 9, 17);
        var tickets = new[]
        {
            CreateQueueTicket(doctor.Id, practice.Id, patients[0].Id, businessDate, 1, 10, now),
            CreateQueueTicket(doctor.Id, practice.Id, patients[1].Id, businessDate, 2, 10, now),
            CreateQueueTicket(doctor.Id, practice.Id, patients[2].Id, businessDate, 3, 1, now.AddMinutes(-1))
        };
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readerType = typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueReader")!;
        var reader = (ITicketQueueReader)Activator.CreateInstance(readerType, context)!;
        var queue = await reader.GetPracticeQueueAsync(
            practice.Id, businessDate, TestContext.Current.CancellationToken);
        var lowPriority = await reader.GetDetailsAsync(
            tickets[2].Id, TestContext.Current.CancellationToken);

        Assert.Equal([patients[1].Id, patients[0].Id, patients[2].Id],
            queue.Waiting.Select(item => item.PatientId).ToArray());
        Assert.NotNull(lowPriority);
        Assert.Equal(2, lowPriority.PatientsAheadNow);
    }

    private static Ticket CreateQueueTicket(
        Guid doctorId,
        Guid practiceId,
        Guid patientId,
        DateOnly businessDate,
        int number,
        int priority,
        DateTime checkInUtc)
        => Ticket.CreateWalkIn(new TicketCreationSnapshot(
            Guid.NewGuid(), doctorId, practiceId, patientId, null, businessDate,
            number, TicketSource.WalkIn, Guid.NewGuid(), "شريحة", "Segment", priority,
            Guid.NewGuid(), "NewConsultation", "كشف", "Consultation", 100,
            "Africa/Cairo", checkInUtc, checkInUtc, CheckInMode.WalkIn,
            Guid.NewGuid(), null)).Value;
}
