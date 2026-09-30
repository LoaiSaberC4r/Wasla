using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Features.Finance;
using Wasla.Application.Features.Finance.Common;
using Wasla.Application.Features.Practices;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.GetTicketDetails;
using Wasla.Application.Features.Tickets.RestoreNoShow;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class Phase12TicketRefundProjectionTests
{
    [Fact]
    public async Task Two_stale_refund_writers_cannot_persist_a_second_refund()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setup = new WaslaDbContext(options);
        await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await setup.Database.ExecuteSqlRawAsync(
            "PRAGMA foreign_keys=OFF;", TestContext.Current.CancellationToken);

        var now = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var payment = Payment.RecordPaid(new PaymentRecordSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), 500m, 500m, "PAY-TEST-20260930-000001", 1,
            new DateOnly(2026, 9, 30), PaymentMethod.Card, null, null,
            Guid.NewGuid(), now)).Value;
        setup.Payments.Add(payment);
        await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var firstWriter = new WaslaDbContext(options);
        await using var secondWriter = new WaslaDbContext(options);
        var firstPayment = await firstWriter.Payments.SingleAsync(
            item => item.Id == payment.Id, TestContext.Current.CancellationToken);
        var secondPayment = await secondWriter.Payments.SingleAsync(
            item => item.Id == payment.Id, TestContext.Current.CancellationToken);
        var firstRefund = Refund.Record(Guid.NewGuid(), firstPayment,
            new FinancialTransactionNumber(1, "REF-TEST-20260930-000001"),
            new DateOnly(2026, 9, 30), PaymentMethod.Wallet,
            RefundReasonCode.OperationalError, null, null, null,
            Guid.NewGuid(), now.AddMinutes(1)).Value;
        var secondRefund = Refund.Record(Guid.NewGuid(), secondPayment,
            new FinancialTransactionNumber(2, "REF-TEST-20260930-000002"),
            new DateOnly(2026, 9, 30), PaymentMethod.Cash,
            RefundReasonCode.OperationalError, null, null, null,
            Guid.NewGuid(), now.AddMinutes(1)).Value;

        firstWriter.Refunds.Add(firstRefund);
        secondWriter.Refunds.Add(secondRefund);
        await firstWriter.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await secondWriter.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await setup.Refunds.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancelled_ticket_refund_is_idempotent_and_details_reflect_financial_state()
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
        var actorId = Guid.NewGuid();
        var user = ApplicationUser.Create(
            actorId, "phase12-doctor", "phase12-doctor@example.test", null,
            "hash", UserType.Doctor, false, now).Value;
        var doctor = Doctor.Create(
            Guid.NewGuid(), actorId, "طبيب", "Doctor",
            new DateOnly(1980, 1, 1), Gender.Male, null,
            "front", "back", "card", null, new DateOnly(2026, 9, 30)).Value;
        doctor.Approve("123", Guid.NewGuid(), now);
        var practice = DoctorPractice.Create(
            Guid.NewGuid(), doctor.Id, "عيادة", "Practice",
            1, 1, 1, "Address", 30, 31, Guid.NewGuid()).Value;
        practice.Activate(true, true, true, Guid.NewGuid());
        var configuration = DoctorPracticeConfiguration.CreateDefault(
            Guid.NewGuid(), practice.Id, actorId).Value;
        var patient = Patient.Create(
            Guid.NewGuid(), "مريض", "Patient", new DateOnly(1990, 1, 1),
            Gender.Male, null, null, null, null, null,
            new DateOnly(2026, 9, 30)).Value;
        var ticket = Ticket.CreateWalkIn(new TicketCreationSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null,
            new DateOnly(2026, 9, 30), 1, TicketSource.WalkIn, Guid.NewGuid(),
            "شريحة", "Segment", 1, Guid.NewGuid(), "NewConsultation",
            "كشف جديد", "New consultation", 500m, "Africa/Cairo",
            now, now, CheckInMode.WalkIn, actorId, null)).Value;
        var payment = Payment.RecordPaid(new PaymentRecordSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null, ticket.Id,
            500m, ticket.PriceSnapshot, "PAY-TEST-20260930-000001", 1,
            ticket.BusinessDate, PaymentMethod.Card, "CARD-123", null,
            actorId, now)).Value;
        Assert.True(ticket.Cancel(actorId, "Patient requested cancellation", now.AddMinutes(1)).IsSuccess);

        context.ApplicationUsers.Add(user);
        context.Doctors.Add(doctor);
        context.DoctorPractices.Add(practice);
        context.DoctorPracticeConfigurations.Add(configuration);
        context.Patients.Add(patient);
        context.Tickets.Add(ticket);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readerType = typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueReader")!;
        var reader = (ITicketQueueReader)Activator.CreateInstance(readerType, context)!;

        var cancelled = await reader.GetDetailsAsync(ticket.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(cancelled);
        Assert.False(cancelled.CanRefund); // The caller must still pass the refund permission/scope check.
        Assert.False(cancelled.IsRefunded);
        Assert.Equal(500m, cancelled.RefundableAmount);
        Assert.Equal("EGP", cancelled.CurrencyCode);
        Assert.Equal(payment.Id, cancelled.PaymentId);
        Assert.Equal(payment.TransactionNumber, cancelled.PaymentTransactionNumber);

        var services = new ServiceCollection();
        services.AddBuildingBlockEntityFrameworkCore<WaslaWritePersistence>();
        services.AddBuildingBlockDbContext<WaslaReadPersistence, WaslaDbContext>();
        services.AddBuildingBlockDbContext<WaslaWritePersistence, WaslaDbContext>();
        services.AddScoped(_ => context);
        services.AddWaslaApplication();
        services.AddScoped<ICurrentUser>(_ => new DoctorCurrentUser(actorId));
        services.AddScoped<IReceptionPracticeAuthorizationService, UnusedReceptionAuthorization>();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(now.AddMinutes(2)));
        var persistenceAssembly = typeof(WaslaDbContext).Assembly;
        services.AddScoped(typeof(ITicketQueueReader), readerType);
        services.AddScoped(typeof(ITicketQueueLock), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.TicketQueueLock")!);
        services.AddScoped(typeof(IFinancialNumberAllocator), persistenceAssembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.FinancialNumberAllocator")!);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var detailsHandlerType = typeof(GetTicketDetailsQuery).Assembly.GetType(
            "Wasla.Application.Features.Tickets.GetTicketDetails.GetTicketDetailsQueryHandler")!;
        var detailsHandler = (IQueryHandler<GetTicketDetailsQuery, TicketDetailsResponse>)
            ActivatorUtilities.CreateInstance(scope.ServiceProvider, detailsHandlerType);
        var authorizedDetails = await detailsHandler.Handle(
            new GetTicketDetailsQuery(practice.Id, ticket.Id),
            TestContext.Current.CancellationToken);
        Assert.True(authorizedDetails.IsSuccess);
        Assert.True(authorizedDetails.Value.CanRefund);

        var paymentCorrectionType = typeof(CorrectPaymentCommand).Assembly.GetType(
            "Wasla.Application.Features.Finance.CorrectPaymentCommandHandler")!;
        var paymentCorrectionHandler = (ICommandHandler<CorrectPaymentCommand, FinancialCorrectionResponse>)
            ActivatorUtilities.CreateInstance(scope.ServiceProvider, paymentCorrectionType);
        var paymentVersion = Convert.ToBase64String((await context.Payments.AsNoTracking()
            .SingleAsync(item => item.Id == payment.Id, TestContext.Current.CancellationToken)).RowVersion);
        var paymentCorrection = await paymentCorrectionHandler.Handle(new CorrectPaymentCommand(
            practice.Id, payment.Id, PaymentMethod.Wallet, "WALLET-123", null,
            "Wrong payment method", paymentVersion, "phase12-payment-correction"),
            TestContext.Current.CancellationToken);
        Assert.True(paymentCorrection.IsSuccess, string.Join("; ", paymentCorrection.Errors));
        Assert.Equal(1, await context.PaymentCorrectionHistories.CountAsync(
            TestContext.Current.CancellationToken));
        var staleCorrection = await paymentCorrectionHandler.Handle(new CorrectPaymentCommand(
            practice.Id, payment.Id, PaymentMethod.Card, "CARD-999", null,
            "Stale correction", paymentVersion, "phase12-payment-correction-stale"),
            TestContext.Current.CancellationToken);
        Assert.Contains(staleCorrection.Errors, error => error.Code == "Finance.ConcurrentModification");

        var handlerType = typeof(RefundPaymentCommand).Assembly.GetType(
            "Wasla.Application.Features.Finance.RefundPaymentCommandHandler")!;
        var handler = (ICommandHandler<RefundPaymentCommand, RefundPaymentResponse>)
            ActivatorUtilities.CreateInstance(scope.ServiceProvider, handlerType);
        var command = new RefundPaymentCommand(
            practice.Id, payment.Id, PaymentMethod.Wallet,
            RefundReasonCode.PatientRequestedCancellation, null, null, null,
            "phase12-refund-one");
        var refundedOnce = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(refundedOnce.IsSuccess);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(replay.IsSuccess);
        Assert.Equal(refundedOnce.Value.RefundId, replay.Value.RefundId);
        var duplicate = await handler.Handle(
            command with { IdempotencyKey = "phase12-refund-two" },
            TestContext.Current.CancellationToken);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(1, await context.Refunds.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PaymentStatus.Paid, (await context.Payments.AsNoTracking()
            .SingleAsync(item => item.Id == payment.Id, TestContext.Current.CancellationToken)).Status);

        var correctionAfterRefund = await paymentCorrectionHandler.Handle(new CorrectPaymentCommand(
            practice.Id, payment.Id, PaymentMethod.Cash, null, null,
            "Post-refund correction", Convert.ToBase64String((await context.Payments.AsNoTracking()
                .SingleAsync(item => item.Id == payment.Id, TestContext.Current.CancellationToken)).RowVersion),
            "phase12-payment-correction-after-refund"), TestContext.Current.CancellationToken);
        Assert.True(correctionAfterRefund.IsFailure);
        Assert.Contains(correctionAfterRefund.Errors, error => error.Code == "Payment.AlreadyRefunded");

        var refundCorrectionType = typeof(CorrectRefundCommand).Assembly.GetType(
            "Wasla.Application.Features.Finance.CorrectRefundCommandHandler")!;
        var refundCorrectionHandler = (ICommandHandler<CorrectRefundCommand, FinancialCorrectionResponse>)
            ActivatorUtilities.CreateInstance(scope.ServiceProvider, refundCorrectionType);
        var refundVersion = Convert.ToBase64String((await context.Refunds.AsNoTracking()
            .SingleAsync(item => item.Id == refundedOnce.Value.RefundId,
                TestContext.Current.CancellationToken)).RowVersion);
        var refundCorrection = await refundCorrectionHandler.Handle(new CorrectRefundCommand(
            practice.Id, refundedOnce.Value.RefundId, PaymentMethod.Cash,
            RefundReasonCode.OperationalError, null, "CASH-RETURN", null,
            "Correct return method", refundVersion, "phase12-refund-correction"),
            TestContext.Current.CancellationToken);
        Assert.True(refundCorrection.IsSuccess, string.Join("; ", refundCorrection.Errors));
        Assert.Equal(1, await context.RefundCorrectionHistories.CountAsync(
            TestContext.Current.CancellationToken));

        var refunded = await reader.GetDetailsAsync(ticket.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(refunded);
        Assert.False(refunded.CanRefund);
        Assert.True(refunded.IsRefunded);
        Assert.Equal(0m, refunded.RefundableAmount);
        Assert.Equal(refundedOnce.Value.RefundId, refunded.RefundId);
        Assert.Equal(refundedOnce.Value.TransactionNumber, refunded.RefundTransactionNumber);
        var afterRefundDetails = await detailsHandler.Handle(
            new GetTicketDetailsQuery(practice.Id, ticket.Id),
            TestContext.Current.CancellationToken);
        Assert.True(afterRefundDetails.IsSuccess);
        Assert.False(afterRefundDetails.Value.CanRefund);

        var servedTicket = Ticket.CreateWalkIn(new TicketCreationSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null,
            new DateOnly(2026, 9, 30), 2, TicketSource.WalkIn, Guid.NewGuid(),
            "شريحة", "Segment", 1, Guid.NewGuid(), "NewConsultation",
            "كشف جديد", "New consultation", 500m, "Africa/Cairo",
            now.AddMinutes(3), now.AddMinutes(3), CheckInMode.WalkIn,
            actorId, null)).Value;
        Assert.True(servedTicket.Call(actorId, now.AddMinutes(4)).IsSuccess);
        Assert.True(servedTicket.StartVisit(actorId, now.AddMinutes(5)).IsSuccess);
        Assert.True(servedTicket.Complete(actorId, now.AddMinutes(6)).IsSuccess);
        var servedPayment = Payment.RecordPaid(new PaymentRecordSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null, servedTicket.Id,
            500m, servedTicket.PriceSnapshot, "PAY-TEST-20260930-000002", 2,
            servedTicket.BusinessDate, PaymentMethod.Cash, null, null,
            actorId, now.AddMinutes(3))).Value;
        context.Tickets.Add(servedTicket);
        context.Payments.Add(servedPayment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Tickets SET Status = 6 WHERE Id = {servedTicket.Id}",
            TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var servedRefund = await handler.Handle(new RefundPaymentCommand(
            practice.Id, servedPayment.Id, PaymentMethod.Cash,
            RefundReasonCode.OperationalError, null, null, null,
            "phase12-served-refund"), TestContext.Current.CancellationToken);
        Assert.True(servedRefund.IsFailure);
        Assert.Contains(servedRefund.Errors, error => error.Code == "Finance.ServiceAlreadyStarted");
        Assert.Equal(1, await context.Refunds.CountAsync(TestContext.Current.CancellationToken));

        var noShowTicket = Ticket.CreateWalkIn(new TicketCreationSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null,
            new DateOnly(2026, 9, 30), 3, TicketSource.WalkIn, Guid.NewGuid(),
            "شريحة", "Segment", 1, Guid.NewGuid(), "NewConsultation",
            "كشف جديد", "New consultation", 500m, "Africa/Cairo",
            now.AddMinutes(7), now.AddMinutes(7), CheckInMode.WalkIn,
            actorId, null)).Value;
        Assert.True(noShowTicket.Call(actorId, now.AddMinutes(8)).IsSuccess);
        Assert.True(noShowTicket.ConfirmNoResponse(actorId, 1, now.AddMinutes(9)).IsSuccess);
        Assert.Equal(TicketStatus.NoShow, noShowTicket.Status);
        Assert.True(noShowTicket.Cancel(actorId, "Patient did not attend", now.AddMinutes(10)).IsSuccess);
        var noShowPayment = Payment.RecordPaid(new PaymentRecordSnapshot(
            Guid.NewGuid(), doctor.Id, practice.Id, patient.Id, null, noShowTicket.Id,
            500m, noShowTicket.PriceSnapshot, "PAY-TEST-20260930-000003", 3,
            noShowTicket.BusinessDate, PaymentMethod.Cash, null, null,
            actorId, now.AddMinutes(7))).Value;
        context.Tickets.Add(noShowTicket);
        context.Payments.Add(noShowPayment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var noShowRefund = await handler.Handle(new RefundPaymentCommand(
            practice.Id, noShowPayment.Id, PaymentMethod.Card,
            RefundReasonCode.PatientRequestedCancellation, null, null, null,
            "phase12-noshow-refund"), TestContext.Current.CancellationToken);
        Assert.True(noShowRefund.IsSuccess, string.Join("; ", noShowRefund.Errors));
        var restoreHandlerType = typeof(RestoreNoShowTicketCommand).Assembly.GetType(
            "Wasla.Application.Features.Tickets.RestoreNoShow.RestoreNoShowTicketCommandHandler")!;
        var restoreHandler = (ICommandHandler<RestoreNoShowTicketCommand, TicketDetailsResponse>)
            ActivatorUtilities.CreateInstance(scope.ServiceProvider, restoreHandlerType);
        var restore = await restoreHandler.Handle(new RestoreNoShowTicketCommand(
            practice.Id, noShowTicket.Id,
            Convert.ToBase64String((await context.Tickets.AsNoTracking()
                .SingleAsync(item => item.Id == noShowTicket.Id,
                    TestContext.Current.CancellationToken)).RowVersion),
            "phase12-noshow-restore"), TestContext.Current.CancellationToken);
        Assert.True(restore.IsFailure);
        Assert.Contains(restore.Errors,
            error => error.Code == "Ticket.RefundedTicketCannotBeRestored");
    }

    private sealed class FixedClock(DateTime nowUtc) : IDateTimeProvider
    {
        public DateTime UtcNow => nowUtc;
    }

    private sealed class DoctorCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "phase12-doctor";
        public string? Email => "phase12-doctor@example.test";
        public IReadOnlyCollection<string> Roles => [SystemRoleNames.Doctor];
        public IReadOnlyCollection<string> Permissions =>
            [PermissionNames.DoctorPracticeTicketsViewOwn,
                PermissionNames.DoctorPracticeTicketsRestoreNoShowOwn,
                PermissionNames.DoctorPracticePaymentsRefundOwn,
                PermissionNames.DoctorPracticePaymentsCorrectOwn];
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
    }

    private sealed class UnusedReceptionAuthorization : IReceptionPracticeAuthorizationService
    {
        public Task<Result> AuthorizeAsync(
            Guid doctorPracticeId, string permissionCode, CancellationToken cancellationToken)
            => Task.FromResult(Result.Fail(TicketErrors.AccessDenied));
    }
}
