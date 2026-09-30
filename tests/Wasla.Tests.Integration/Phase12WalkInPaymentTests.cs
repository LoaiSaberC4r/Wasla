using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Bootstrap;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Features.Finance.Common;
using Wasla.Application.Features.Practices;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.CheckInReservation;
using Wasla.Application.Features.Tickets.CancelTicket;
using Wasla.Application.Features.Tickets.CreateWalkIn;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class Phase12WalkInPaymentTests
{
    [Fact]
    public async Task Walk_in_and_reservation_check_in_persist_numbered_EGP_payments_with_tickets()
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

        var now = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var receptionActorId = Guid.NewGuid();
        var doctor = Doctor.Create(
            Guid.NewGuid(), Guid.NewGuid(), "طبيب", "Doctor",
            new DateOnly(1980, 1, 1), Gender.Male, null,
            "front", "back", "card", null, new DateOnly(2026, 9, 30)).Value;
        doctor.Approve("123", Guid.NewGuid(), now);
        var practice = DoctorPractice.Create(
            Guid.NewGuid(), doctor.Id, "عيادة", "Practice",
            1, 1, 1, "Address", 30, 31, Guid.NewGuid()).Value;
        practice.Activate(true, true, true, Guid.NewGuid());
        var configuration = DoctorPracticeConfiguration.CreateDefault(
            Guid.NewGuid(), practice.Id, receptionActorId).Value;
        var patient = Patient.Create(
            Guid.NewGuid(), "مريض", "Patient", new DateOnly(1990, 1, 1),
            Gender.Male, null, null, null, null, null,
            new DateOnly(2026, 9, 30)).Value;
        var reservedPatient = Patient.Create(
            Guid.NewGuid(), "مريض آخر", "Another patient", new DateOnly(1991, 1, 1),
            Gender.Male, null, null, null, null, null,
            new DateOnly(2026, 9, 30)).Value;
        var segment = DoctorPracticeSegment.CreateDefault(
            Guid.NewGuid(), practice.Id, receptionActorId).Value;
        var visitType = DoctorPracticeVisitType.CreateDefault(
            Guid.NewGuid(), practice.Id, DoctorPracticeVisitTypeCode.NewConsultation,
            receptionActorId).Value;
        var price = DoctorPracticeSegmentVisitTypePrice.Create(
            Guid.NewGuid(), practice.Id, segment.Id, visitType.Id,
            500m, receptionActorId).Value;
        var reservation = Reservation.Create(new ReservationCreationSnapshot(
            Guid.NewGuid(), "PHASE12-RESERVATION", doctor.Id, practice.Id,
            reservedPatient.Id, segment.Id, visitType.Id,
            ReservationBookingSource.Reception, receptionActorId, null,
            now.AddMinutes(10), new DateTime(2026, 9, 30, 11, 10, 0),
            new DateOnly(2026, 9, 30), "Africa/Cairo", 20,
            segment.NameAr, segment.NameEn, segment.Priority,
            visitType.Type.ToString(), visitType.NameAr, visitType.NameEn,
            700m, null, now)).Value;
        context.Doctors.Add(doctor);
        context.DoctorPractices.Add(practice);
        context.DoctorPracticeConfigurations.Add(configuration);
        context.Patients.AddRange(patient, reservedPatient);
        context.DoctorPracticeSegments.Add(segment);
        context.DoctorPracticeVisitTypes.Add(visitType);
        context.DoctorPracticeSegmentVisitTypePrices.Add(price);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWaslaApplication();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockEntityFrameworkCore<WaslaWritePersistence>();
        services.AddBuildingBlockDbContext<WaslaReadPersistence, WaslaDbContext>();
        services.AddBuildingBlockDbContext<WaslaWritePersistence, WaslaDbContext>();
        services.AddScoped(_ => new WaslaDbContext(options));
        services.AddScoped<ICurrentUser>(_ => new ReceptionCurrentUser(receptionActorId));
        services.AddScoped<IReceptionPracticeAuthorizationService, AllowedReceptionAuthorization>();
        services.AddScoped<IReservationProjectionInvalidationOutbox, UnusedProjectionOutbox>();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(now));
        var persistenceAssembly = typeof(WaslaDbContext).Assembly;
        services.AddScoped(typeof(ITicketQueueReader), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueReader")!);
        services.AddScoped(typeof(ITicketQueueLock), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueLock")!);
        services.AddScoped(typeof(ITicketNumberAllocator), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketNumberAllocator")!);
        services.AddScoped(typeof(IReservationNoShowRuntimeReader), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.ReservationNoShowRuntimeReader")!);
        services.AddScoped(typeof(IFinancialNumberAllocator), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.FinancialNumberAllocator")!);
        using var provider = services.BuildServiceProvider();
        var command = new CreateWalkInCommand(
            practice.Id, patient.Id, segment.Id, visitType.Id,
            500m, "phase12-walk-in", PaymentMethod.Card, "CARD-123", "Collected at desk");
        BuildingBlock.Domain.Results.Result<TicketDetailsResponse> result;
        using (var scope = provider.CreateScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(command, TestContext.Current.CancellationToken);
        }
        Assert.True(result.IsSuccess);

        var ticket = await context.Tickets.AsNoTracking().SingleAsync(
            TestContext.Current.CancellationToken);
        var payment = await context.Payments.AsNoTracking().SingleAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(ticket.Id, payment.TicketId);
        Assert.Equal(500m, ticket.PriceSnapshot);
        Assert.Equal(ticket.PriceSnapshot, payment.Amount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentMethod.Card, payment.PaymentMethod);
        Assert.Equal("EGP", payment.CurrencyCode);
        Assert.Equal("CARD-123", payment.ReferenceNumber);
        Assert.StartsWith("PAY-", payment.TransactionNumber, StringComparison.Ordinal);
        Assert.Equal(1, payment.SequenceNumber);

        BuildingBlock.Domain.Results.Result<TicketDetailsResponse> replay;
        using (var scope = provider.CreateScope())
        {
            replay = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(command, TestContext.Current.CancellationToken);
        }
        Assert.True(replay.IsSuccess);
        Assert.Equal(result.Value.TicketId, replay.Value.TicketId);
        Assert.Equal(1, await context.Tickets.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.Payments.CountAsync(TestContext.Current.CancellationToken));

        BuildingBlock.Domain.Results.Result<TicketDetailsResponse> checkIn;
        using (var scope = provider.CreateScope())
        {
            checkIn = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CheckInReservationCommand(
                    practice.Id, reservation.Id, 700m, "phase12-check-in",
                    PaymentMethod.Wallet, "WALLET-456"), TestContext.Current.CancellationToken);
        }
        Assert.True(checkIn.IsSuccess, string.Join("; ", checkIn.Errors));
        var reservationPayment = await context.Payments.AsNoTracking().SingleAsync(
            item => item.ReservationId == reservation.Id, TestContext.Current.CancellationToken);
        Assert.Equal(checkIn.Value.TicketId, reservationPayment.TicketId);
        Assert.Equal(700m, reservationPayment.Amount);
        Assert.Equal(PaymentMethod.Wallet, reservationPayment.PaymentMethod);
        Assert.Equal("EGP", reservationPayment.CurrencyCode);
        Assert.StartsWith("PAY-", reservationPayment.TransactionNumber, StringComparison.Ordinal);
        Assert.Equal(2, reservationPayment.SequenceNumber);
        Assert.Equal(ReservationStatus.ConvertedToTicket, (await context.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservation.Id, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(2, await context.Tickets.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.Payments.CountAsync(TestContext.Current.CancellationToken));

        BuildingBlock.Domain.Results.Result<TicketDetailsResponse> cancelled;
        using (var scope = provider.CreateScope())
        {
            cancelled = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CancelTicketCommand(practice.Id, ticket.Id,
                    "Patient requested cancellation", Convert.ToBase64String(ticket.RowVersion),
                    "phase12-cancel"), TestContext.Current.CancellationToken);
        }
        Assert.True(cancelled.IsSuccess, string.Join("; ", cancelled.Errors));
        Assert.True(cancelled.Value.CanRefund);
        Assert.False(cancelled.Value.IsRefunded);
        Assert.Equal(500m, cancelled.Value.RefundableAmount);
        Assert.Equal("EGP", cancelled.Value.CurrencyCode);
    }

    private sealed class FixedClock(DateTime nowUtc) : IDateTimeProvider
    {
        public DateTime UtcNow => nowUtc;
    }

    private sealed class ReceptionCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "phase12-reception";
        public string? Email => "phase12-reception@example.test";
        public IReadOnlyCollection<string> Roles => [SystemRoleNames.Reception];
        public IReadOnlyCollection<string> Permissions =>
            [PermissionNames.PracticeTicketsCreateWalkIn, PermissionNames.PracticeTicketsRecordPayment];
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
    }

    private sealed class AllowedReceptionAuthorization : IReceptionPracticeAuthorizationService
    {
        public Task<Result> AuthorizeAsync(
            Guid doctorPracticeId, string permissionCode, CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok());
    }

    private sealed class UnusedProjectionOutbox : IReservationProjectionInvalidationOutbox
    {
        public Task QueueAsync(
            QueueReservationProjectionInvalidation invalidation,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
