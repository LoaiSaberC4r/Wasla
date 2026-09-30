using System.Reflection;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Finance;
using Wasla.Application.Features.Practices;
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

public sealed class Phase12FinanceReadTests
{
    [Fact]
    public async Task Doctor_dashboard_aggregates_transactions_by_their_own_business_dates_and_patient_list_is_scoped()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<WaslaDbContext>().UseSqlite(connection).Options;
        await using var db = new WaslaDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;", TestContext.Current.CancellationToken);

        var paymentDate = new DateOnly(2026, 9, 30);
        var refundDate = new DateOnly(2026, 10, 2);
        var at = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        var doctorUserId = Guid.NewGuid();
        var doctorUser = ApplicationUser.Create(doctorUserId, "doctor", "doctor@example.test", null,
            "hash", UserType.Doctor, false, at).Value;
        var doctor = Doctor.Create(Guid.NewGuid(), doctorUserId, "طبيب", "Doctor",
            new DateOnly(1980, 1, 1), Gender.Male, null, "front", "back", "card", null,
            paymentDate).Value;
        Assert.True(doctor.Approve("123", Guid.NewGuid(), at).IsSuccess);
        var practice = DoctorPractice.Create(Guid.NewGuid(), doctor.Id, "عيادة", "Practice",
            1, 1, 1, "Address", 30, 31, doctorUserId).Value;
        var patient = Patient.Create(Guid.NewGuid(), "مريض", "Patient",
            new DateOnly(1990, 1, 1), Gender.Male, null, null, null, null, null,
            paymentDate).Value;
        var otherPatient = Patient.Create(Guid.NewGuid(), "آخر", "Other",
            new DateOnly(1991, 1, 1), Gender.Male, null, null, null, null, null,
            paymentDate).Value;

        var firstTicket = TicketFor(doctor.Id, practice.Id, patient.Id, paymentDate, 1, 500m, at, doctorUserId);
        Assert.True(firstTicket.Cancel(doctorUserId, "Cancelled", at.AddMinutes(1)).IsSuccess);
        var secondTicket = TicketFor(doctor.Id, practice.Id, patient.Id, paymentDate, 2, 700m,
            at.AddMinutes(2), doctorUserId);
        var firstPayment = PaymentFor(firstTicket, 500m, 1, doctorUserId, at);
        var secondPayment = PaymentFor(secondTicket, 700m, 2, doctorUserId, at.AddMinutes(2));
        var refund = Refund.Record(Guid.NewGuid(), firstPayment,
            new FinancialTransactionNumber(1, "REF-TEST-20261002-000001"),
            refundDate, PaymentMethod.Wallet,
            RefundReasonCode.PatientRequestedCancellation, null, null, null,
            doctorUserId, at.AddDays(2)).Value;

        db.ApplicationUsers.Add(doctorUser);
        db.Doctors.Add(doctor);
        db.DoctorPractices.Add(practice);
        db.Patients.AddRange(patient, otherPatient);
        db.Tickets.AddRange(firstTicket, secondTicket);
        db.Payments.AddRange(firstPayment, secondPayment);
        db.Refunds.Add(refund);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var doctorAccess = new UserAccessSnapshot(doctorUser,
            [SystemRoleNames.Doctor],
            [PermissionNames.DoctorRevenueViewOwn, PermissionNames.DoctorPracticePaymentsViewOwn],
            doctor.Id, DoctorApprovalStatus.Approved, null, null, null, false);
        var finance = CreateService(db, new TestCurrentUser(doctorUserId,
            [SystemRoleNames.Doctor], doctorAccess.Permissions), doctorAccess);
        var dashboard = await finance.GetDoctorDashboardAsync(paymentDate, refundDate,
            practice.Id, TestContext.Current.CancellationToken);

        Assert.True(dashboard.IsSuccess);
        Assert.Equal(1200m, dashboard.Value.Summary.GrossRevenue);
        Assert.Equal(500m, dashboard.Value.Summary.TotalRefunds);
        Assert.Equal(700m, dashboard.Value.Summary.NetRevenue);
        Assert.Equal(2, dashboard.Value.Summary.PaymentCount);
        Assert.Equal(1, dashboard.Value.Summary.RefundCount);
        Assert.Equal(1200m, Assert.Single(dashboard.Value.ByPractice).GrossRevenue);
        Assert.Equal(500m, Assert.Single(dashboard.Value.DailyTrend,
            item => item.Date == refundDate).Refunds);
        Assert.Equal(1200m, Assert.Single(dashboard.Value.BySegment).GrossRevenue);

        var adminUserId = Guid.NewGuid();
        var adminUser = ApplicationUser.Create(adminUserId, "admin", "admin@example.test",
            null, "hash", UserType.SuperAdmin, false, at).Value;
        var adminAccess = new UserAccessSnapshot(adminUser, [SystemRoleNames.SuperAdmin],
            [PermissionNames.PlatformRevenueViewAggregates], null, null, null, null,
            Guid.NewGuid(), false);
        var adminFinance = CreateService(db, new TestCurrentUser(adminUserId,
            [SystemRoleNames.SuperAdmin], adminAccess.Permissions), adminAccess);
        var aggregates = await adminFinance.GetPlatformDashboardAsync(paymentDate, refundDate,
            doctor.Id, practice.Id, TestContext.Current.CancellationToken);
        Assert.True(aggregates.IsSuccess);
        Assert.Equal(700m, aggregates.Value.Summary.NetRevenue);
        Assert.Equal(500m, Assert.Single(aggregates.Value.ByDoctorPractice).Refunds);

        var detail = await finance.GetPracticePaymentDetailAsync(practice.Id, firstPayment.Id,
            TestContext.Current.CancellationToken);
        Assert.True(detail.IsSuccess, string.Join("; ", detail.Errors));
        Assert.True(detail.Value.IsRefunded);
        Assert.Equal(refund.TransactionNumber, detail.Value.Refund?.TransactionNumber);
        var internalReceipt = await finance.GetPracticeRefundReceiptAsync(practice.Id,
            refund.Id, TestContext.Current.CancellationToken);
        Assert.True(internalReceipt.IsSuccess);
        Assert.Equal(doctorUser.UserName, internalReceipt.Value.RecordedByDisplayName);

        var otherTicket = TicketFor(doctor.Id, practice.Id, otherPatient.Id, paymentDate, 3,
            200m, at.AddMinutes(3), doctorUserId);
        var otherPayment = PaymentFor(otherTicket, 200m, 3, doctorUserId, at.AddMinutes(3));
        db.Tickets.Add(otherTicket);
        db.Payments.Add(otherPayment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var patientUserId = Guid.NewGuid();
        var patientUser = ApplicationUser.Create(patientUserId, "patient", "patient@example.test",
            null, "hash", UserType.Patient, false, at).Value;
        var patientAccess = new UserAccessSnapshot(patientUser, [SystemRoleNames.Patient],
            [PermissionNames.PaymentsViewOwn], null, null, patient.Id, null, null, false);
        var patientFinance = CreateService(db, new TestCurrentUser(patientUserId,
            [SystemRoleNames.Patient], patientAccess.Permissions), patientAccess);
        var own = await patientFinance.ListOwnTransactionsAsync(new FinancialTransactionFilter(
            PageSize: 10), TestContext.Current.CancellationToken);
        Assert.True(own.IsSuccess, string.Join("; ", own.Errors));
        Assert.Equal(3, own.Value.TotalCount);
        Assert.DoesNotContain(own.Value.Items, item => item.TransactionId == otherPayment.Id);
        var ownReceipt = await patientFinance.GetOwnPaymentReceiptAsync(firstPayment.Id,
            TestContext.Current.CancellationToken);
        Assert.True(ownReceipt.IsSuccess);
        Assert.DoesNotContain("recordedByDisplayName", JsonSerializer.Serialize(ownReceipt.Value,
            WebJsonOptions), StringComparison.Ordinal);

        var receptionUserId = Guid.NewGuid();
        var receptionUser = ApplicationUser.Create(receptionUserId, "reception",
            "reception@example.test", null, "hash", UserType.Reception, false, at).Value;
        var receptionAccess = new UserAccessSnapshot(receptionUser,
            [SystemRoleNames.Reception], [PermissionNames.PracticePaymentsView],
            null, null, null, Guid.NewGuid(), null, false);
        var receptionFinance = CreateService(db, new TestCurrentUser(receptionUserId,
            [SystemRoleNames.Reception], receptionAccess.Permissions), receptionAccess,
            new AssignedPracticeAuthorization(Guid.NewGuid()));
        var crossPractice = await receptionFinance.ListPracticeTransactionsAsync(practice.Id,
            new FinancialTransactionFilter(), TestContext.Current.CancellationToken);
        Assert.True(crossPractice.IsFailure);
    }

    private static Ticket TicketFor(Guid doctorId, Guid practiceId, Guid patientId,
        DateOnly date, int number, decimal price, DateTime at, Guid actorId)
        => Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), doctorId, practiceId,
            patientId, null, date, number, TicketSource.WalkIn, SharedSegmentId,
            "شريحة", "Segment", 1, SharedVisitTypeId, "NewConsultation",
            "كشف جديد", "New consultation", price, "Africa/Cairo",
            at, at, CheckInMode.WalkIn, actorId, null)).Value;

    private static Payment PaymentFor(Ticket ticket, decimal amount, int sequence,
        Guid actorId, DateTime at)
        => Payment.RecordPaid(new PaymentRecordSnapshot(Guid.NewGuid(), ticket.DoctorId,
            ticket.DoctorPracticeId, ticket.PatientId, null, ticket.Id, amount,
            ticket.PriceSnapshot, $"PAY-TEST-20260930-{sequence:D6}", sequence,
            ticket.BusinessDate, PaymentMethod.Card, null, null, actorId, at)).Value;

    private static IFinanceReadService CreateService(WaslaDbContext db, ICurrentUser user,
        UserAccessSnapshot access, IReceptionPracticeAuthorizationService? receptionAuthorization = null)
    {
        var dataStore = DispatchProxy.Create<IWaslaDataStore, SnapshotDataStoreProxy>();
        ((SnapshotDataStoreProxy)dataStore).Snapshot = access;
        var type = typeof(WaslaDbContext).Assembly.GetType(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.FinanceReadService")!;
        return (IFinanceReadService)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [db, user, dataStore, receptionAuthorization!], null)!;
    }

    private static readonly Guid SharedSegmentId = Guid.NewGuid();
    private static readonly Guid SharedVisitTypeId = Guid.NewGuid();
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public class SnapshotDataStoreProxy : DispatchProxy
    {
        public UserAccessSnapshot Snapshot { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name == nameof(IWaslaDataStore.GetAccessSnapshotAsync)
                ? Task.FromResult<UserAccessSnapshot?>(Snapshot)
                : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class AssignedPracticeAuthorization(Guid assignedPracticeId)
        : IReceptionPracticeAuthorizationService
    {
        public Task<Result> AuthorizeAsync(Guid doctorPracticeId, string permissionCode,
            CancellationToken cancellationToken)
            => Task.FromResult(doctorPracticeId == assignedPracticeId &&
                               permissionCode == PermissionNames.PracticePaymentsView
                ? Result.Ok()
                : Result.Fail(Error.Security("Finance.AccessDenied", "Access denied.")));
    }

    private sealed class TestCurrentUser(Guid id, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => roles;
        public IReadOnlyCollection<string> Permissions => permissions;
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
    }
}
