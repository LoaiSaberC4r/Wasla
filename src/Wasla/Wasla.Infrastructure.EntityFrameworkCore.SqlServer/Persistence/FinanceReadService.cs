using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Finance;
using Wasla.Application.Features.Practices;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Payments;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class FinanceReadService(
    WaslaDbContext db,
    ICurrentUser currentUser,
    IWaslaDataStore dataStore,
    IReceptionPracticeAuthorizationService receptionAuthorization) : IFinanceReadService
{
    private static Error AccessDenied => Error.Security(
        "Finance.AccessDenied", ErrorMessage.GetString("FinanceAccessDenied"));
    private static Error NotFound => Error.NotFound(
        "Finance.NotFound", ErrorMessage.GetString("FinanceNotFound"));
    private static Error InvalidFilter => Error.Validation(
        "Finance.InvalidFilter", ErrorMessage.GetString("FinanceInvalidFilter"));

    public async Task<Result<FinancialPage<FinancialTransactionItem>>> ListPracticeTransactionsAsync(
        Guid practiceId, FinancialTransactionFilter filter, CancellationToken cancellationToken)
    {
        var access = await AuthorizePracticeAsync(practiceId, PermissionNames.DoctorPracticePaymentsViewOwn,
            PermissionNames.PracticePaymentsView, cancellationToken);
        if (access.IsFailure) return Result<FinancialPage<FinancialTransactionItem>>.Fail(access.Errors);
        if (!ValidFilter(filter)) return Result<FinancialPage<FinancialTransactionItem>>.Fail(InvalidFilter);
        var rows = await PageAsync(Transactions(filter with { PracticeId = practiceId }, null, null),
            filter.PageNumber, filter.PageSize, cancellationToken);
        return Result<FinancialPage<FinancialTransactionItem>>.Ok(new(
            rows.Items.Select(ToInternalItem).ToArray(), rows.TotalCount, rows.PageNumber, rows.PageSize));
    }

    public async Task<Result<FinancialPage<FinancialTransactionItem>>> ListDoctorTransactionsAsync(
        FinancialTransactionFilter filter, CancellationToken cancellationToken)
    {
        var doctor = await ResolveDoctorAsync(PermissionNames.DoctorPracticePaymentsViewOwn, cancellationToken);
        if (doctor.IsFailure) return Result<FinancialPage<FinancialTransactionItem>>.Fail(doctor.Errors);
        if (!ValidFilter(filter)) return Result<FinancialPage<FinancialTransactionItem>>.Fail(InvalidFilter);
        if (filter.PracticeId is { } practiceId && !await OwnsPracticeAsync(doctor.Value, practiceId, cancellationToken))
            return Result<FinancialPage<FinancialTransactionItem>>.Fail(AccessDenied);
        var rows = await PageAsync(Transactions(filter, doctor.Value, null),
            filter.PageNumber, filter.PageSize, cancellationToken);
        return Result<FinancialPage<FinancialTransactionItem>>.Ok(new(
            rows.Items.Select(ToInternalItem).ToArray(), rows.TotalCount, rows.PageNumber, rows.PageSize));
    }

    public async Task<Result<FinancialPage<PatientFinancialTransactionItem>>> ListOwnTransactionsAsync(
        FinancialTransactionFilter filter, CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(cancellationToken);
        if (patient.IsFailure) return Result<FinancialPage<PatientFinancialTransactionItem>>.Fail(patient.Errors);
        if (!ValidFilter(filter)) return Result<FinancialPage<PatientFinancialTransactionItem>>.Fail(InvalidFilter);
        var rows = await PageAsync(Transactions(filter with { PatientId = patient.Value }, null, patient.Value),
            filter.PageNumber, filter.PageSize, cancellationToken);
        return Result<FinancialPage<PatientFinancialTransactionItem>>.Ok(new(
            rows.Items.Select(ToPatientItem).ToArray(), rows.TotalCount, rows.PageNumber, rows.PageSize));
    }

    public async Task<Result<FinancialPaymentDetail>> GetPracticePaymentDetailAsync(
        Guid practiceId, Guid paymentId, CancellationToken cancellationToken)
    {
        var access = await AuthorizePracticeAsync(practiceId, PermissionNames.DoctorPracticePaymentsViewOwn,
            PermissionNames.PracticePaymentsView, cancellationToken);
        if (access.IsFailure) return Result<FinancialPaymentDetail>.Fail(access.Errors);

        var row = await (from payment in db.Payments.AsNoTracking()
            join ticket in db.Tickets.AsNoTracking() on payment.TicketId equals ticket.Id
            join patient in db.Patients.AsNoTracking() on payment.PatientId equals patient.Id
            join doctor in db.Doctors.AsNoTracking() on payment.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on payment.DoctorPracticeId equals practice.Id
            join user in db.ApplicationUsers.AsNoTracking() on payment.CollectedByApplicationUserId equals user.Id
            where payment.Id == paymentId && payment.DoctorPracticeId == practiceId
            select new { payment, ticket, patient, doctor, practice, user })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return Result<FinancialPaymentDetail>.Fail(NotFound);

        var refund = await (from item in db.Refunds.AsNoTracking()
            join user in db.ApplicationUsers.AsNoTracking() on item.RefundedByApplicationUserId equals user.Id
            where item.PaymentId == paymentId
            select new { item, user }).SingleOrDefaultAsync(cancellationToken);
        var paymentCorrections = await (from correction in db.PaymentCorrectionHistories.AsNoTracking()
            join user in db.ApplicationUsers.AsNoTracking() on correction.CorrectedByApplicationUserId equals user.Id
            where correction.PaymentId == paymentId
            orderby correction.CorrectedOnUtc
            select new FinancialCorrectionItem(correction.Id, correction.CorrectedOnUtc,
                correction.CorrectedByApplicationUserId, user.UserName, correction.CorrectionReason,
                correction.OldPaymentMethod, correction.NewPaymentMethod,
                correction.OldReferenceNumber, correction.NewReferenceNumber,
                correction.OldNotes, correction.NewNotes)).ToArrayAsync(cancellationToken);
        var refundCorrections = refund is null
            ? []
            : await (from correction in db.RefundCorrectionHistories.AsNoTracking()
                join user in db.ApplicationUsers.AsNoTracking() on correction.CorrectedByApplicationUserId equals user.Id
                where correction.RefundId == refund.item.Id
                orderby correction.CorrectedOnUtc
                select new FinancialCorrectionItem(correction.Id, correction.CorrectedOnUtc,
                    correction.CorrectedByApplicationUserId, user.UserName, correction.CorrectionReason,
                    correction.OldRefundMethod, correction.NewRefundMethod,
                    correction.OldReferenceNumber, correction.NewReferenceNumber,
                    correction.OldNotes, correction.NewNotes,
                    correction.OldRefundReasonCode, correction.NewRefundReasonCode,
                    correction.OldReason, correction.NewReason)).ToArrayAsync(cancellationToken);

        var canRefund = refund is null && row.ticket.Status == TicketStatus.Cancelled &&
                        row.ticket.InProgressOnUtc is null &&
                        !await db.TicketHistories.AsNoTracking().AnyAsync(
                            item => item.TicketId == row.ticket.Id &&
                                    item.EventType == TicketHistoryEventType.InProgress,
                            cancellationToken) &&
                        row.payment.Status == PaymentStatus.Paid &&
                        row.payment.Amount > 0 &&
                        row.payment.Amount == row.ticket.PriceSnapshot &&
                        row.payment.CurrencyCode == FinancialPolicy.CurrencyCode &&
                        await CanRefundAsync(practiceId, cancellationToken);
        return Result<FinancialPaymentDetail>.Ok(new(
            new(row.payment.Id, row.payment.TransactionNumber, row.payment.TicketId,
                row.payment.ReservationId, row.payment.Amount,
                row.payment.CurrencyCode, row.payment.Status, row.payment.PaymentMethod, row.payment.ReferenceNumber,
                row.payment.Notes, row.payment.BusinessDate, row.payment.CollectedOnUtc,
                row.payment.CollectedByApplicationUserId, row.user.UserName,
                Convert.ToBase64String(row.payment.RowVersion)),
            refund is null ? null : new RefundFinancialData(refund.item.Id, refund.item.TransactionNumber,
                refund.item.Amount, refund.item.CurrencyCode, refund.item.RefundMethod,
                refund.item.RefundReasonCode, refund.item.Reason, refund.item.ReferenceNumber,
                refund.item.Notes, refund.item.BusinessDate, refund.item.RefundedOnUtc,
                refund.item.RefundedByApplicationUserId, refund.user.UserName,
                Convert.ToBase64String(refund.item.RowVersion)),
            row.patient.Id, row.patient.NameAr, row.patient.NameEn,
            row.doctor.Id, row.doctor.NameAr, row.doctor.NameEn,
            row.practice.Id, row.practice.NameAr, row.practice.NameEn,
            row.ticket.Id, row.ticket.TicketNumber,
            row.ticket.SegmentId, row.ticket.SegmentNameArSnapshot, row.ticket.SegmentNameEnSnapshot,
            row.ticket.VisitTypeId, row.ticket.VisitTypeCodeSnapshot,
            row.ticket.VisitTypeNameArSnapshot, row.ticket.VisitTypeNameEnSnapshot,
            refund is not null, canRefund, canRefund ? row.payment.Amount : 0m,
            paymentCorrections, refundCorrections));
    }

    public async Task<Result<PaymentReceiptData>> GetPracticePaymentReceiptAsync(
        Guid practiceId, Guid paymentId, CancellationToken cancellationToken)
    {
        var access = await AuthorizePracticeAsync(practiceId, PermissionNames.DoctorPracticePaymentsViewOwn,
            PermissionNames.PracticePaymentsView, cancellationToken);
        if (access.IsFailure) return Result<PaymentReceiptData>.Fail(access.Errors);
        var receipt = await PaymentReceiptQuery(paymentId, practiceId, null, includeStaff: true)
            .SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? Result<PaymentReceiptData>.Fail(NotFound) : Result<PaymentReceiptData>.Ok(receipt);
    }

    public async Task<Result<RefundReceiptData>> GetPracticeRefundReceiptAsync(
        Guid practiceId, Guid refundId, CancellationToken cancellationToken)
    {
        var access = await AuthorizePracticeAsync(practiceId, PermissionNames.DoctorPracticePaymentsViewOwn,
            PermissionNames.PracticePaymentsView, cancellationToken);
        if (access.IsFailure) return Result<RefundReceiptData>.Fail(access.Errors);
        var receipt = await RefundReceiptQuery(refundId, practiceId, null, includeStaff: true)
            .SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? Result<RefundReceiptData>.Fail(NotFound) : Result<RefundReceiptData>.Ok(receipt);
    }

    public async Task<Result<PaymentReceiptData>> GetOwnPaymentReceiptAsync(
        Guid paymentId, CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(cancellationToken);
        if (patient.IsFailure) return Result<PaymentReceiptData>.Fail(patient.Errors);
        var receipt = await PaymentReceiptQuery(paymentId, null, patient.Value, includeStaff: false)
            .SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? Result<PaymentReceiptData>.Fail(NotFound) : Result<PaymentReceiptData>.Ok(receipt);
    }

    public async Task<Result<RefundReceiptData>> GetOwnRefundReceiptAsync(
        Guid refundId, CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(cancellationToken);
        if (patient.IsFailure) return Result<RefundReceiptData>.Fail(patient.Errors);
        var receipt = await RefundReceiptQuery(refundId, null, patient.Value, includeStaff: false)
            .SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? Result<RefundReceiptData>.Fail(NotFound) : Result<RefundReceiptData>.Ok(receipt);
    }

    private IQueryable<PaymentReceiptData> PaymentReceiptQuery(
        Guid paymentId, Guid? practiceId, Guid? patientId, bool includeStaff)
        => from payment in db.Payments.AsNoTracking()
           join ticket in db.Tickets.AsNoTracking() on payment.TicketId equals ticket.Id
           join patient in db.Patients.AsNoTracking() on payment.PatientId equals patient.Id
           join doctor in db.Doctors.AsNoTracking() on payment.DoctorId equals doctor.Id
           join practice in db.DoctorPractices.AsNoTracking() on payment.DoctorPracticeId equals practice.Id
           join user in db.ApplicationUsers.AsNoTracking() on payment.CollectedByApplicationUserId equals user.Id
           where payment.Id == paymentId && (!practiceId.HasValue || payment.DoctorPracticeId == practiceId.Value) &&
                 (!patientId.HasValue || payment.PatientId == patientId.Value)
           select new PaymentReceiptData(payment.TransactionNumber,
               patient.Id, patient.NameAr, patient.NameEn, doctor.Id, doctor.NameAr, doctor.NameEn,
               practice.Id, practice.NameAr, practice.NameEn, ticket.TicketNumber,
               payment.Amount, payment.CurrencyCode, payment.PaymentMethod, payment.ReferenceNumber,
               payment.CollectedOnUtc, payment.BusinessDate, includeStaff ? user.UserName : null);

    private IQueryable<RefundReceiptData> RefundReceiptQuery(
        Guid refundId, Guid? practiceId, Guid? patientId, bool includeStaff)
        => from refund in db.Refunds.AsNoTracking()
           join payment in db.Payments.AsNoTracking() on refund.PaymentId equals payment.Id
           join ticket in db.Tickets.AsNoTracking() on refund.TicketId equals ticket.Id
           join patient in db.Patients.AsNoTracking() on refund.PatientId equals patient.Id
           join doctor in db.Doctors.AsNoTracking() on refund.DoctorId equals doctor.Id
           join practice in db.DoctorPractices.AsNoTracking() on refund.DoctorPracticeId equals practice.Id
           join user in db.ApplicationUsers.AsNoTracking() on refund.RefundedByApplicationUserId equals user.Id
           where refund.Id == refundId && (!practiceId.HasValue || refund.DoctorPracticeId == practiceId.Value) &&
                 (!patientId.HasValue || refund.PatientId == patientId.Value)
           select new RefundReceiptData(refund.TransactionNumber, payment.TransactionNumber,
               patient.Id, patient.NameAr, patient.NameEn, doctor.Id, doctor.NameAr, doctor.NameEn,
               practice.Id, practice.NameAr, practice.NameEn, ticket.TicketNumber,
               refund.Amount, refund.CurrencyCode, refund.RefundMethod, refund.RefundReasonCode,
               refund.Reason, refund.ReferenceNumber, refund.RefundedOnUtc, refund.BusinessDate,
               includeStaff ? user.UserName : null);

    private IQueryable<FinancialListRow> Transactions(FinancialTransactionFilter filter,
        Guid? doctorId, Guid? patientId)
    {
        var payments = db.Payments.AsNoTracking().Where(item =>
            (!doctorId.HasValue || item.DoctorId == doctorId.Value) &&
            (!filter.PracticeId.HasValue || item.DoctorPracticeId == filter.PracticeId.Value) &&
            (!patientId.HasValue || item.PatientId == patientId.Value) &&
            (!filter.PatientId.HasValue || item.PatientId == filter.PatientId.Value) &&
            (!filter.FromDate.HasValue || item.BusinessDate >= filter.FromDate.Value) &&
            (!filter.ToDate.HasValue || item.BusinessDate <= filter.ToDate.Value) &&
            (!filter.Method.HasValue || item.PaymentMethod == filter.Method.Value) &&
            (filter.TransactionNumber == null || item.TransactionNumber.Contains(filter.TransactionNumber)));
        var refunds = db.Refunds.AsNoTracking().Where(item =>
            (!doctorId.HasValue || item.DoctorId == doctorId.Value) &&
            (!filter.PracticeId.HasValue || item.DoctorPracticeId == filter.PracticeId.Value) &&
            (!patientId.HasValue || item.PatientId == patientId.Value) &&
            (!filter.PatientId.HasValue || item.PatientId == filter.PatientId.Value) &&
            (!filter.FromDate.HasValue || item.BusinessDate >= filter.FromDate.Value) &&
            (!filter.ToDate.HasValue || item.BusinessDate <= filter.ToDate.Value) &&
            (!filter.Method.HasValue || item.RefundMethod == filter.Method.Value) &&
            (filter.TransactionNumber == null || item.TransactionNumber.Contains(filter.TransactionNumber)));
        var paymentRows = from payment in payments
            join ticket in db.Tickets.AsNoTracking() on payment.TicketId equals ticket.Id
            join patient in db.Patients.AsNoTracking() on payment.PatientId equals patient.Id
            join doctor in db.Doctors.AsNoTracking() on payment.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on payment.DoctorPracticeId equals practice.Id
            join user in db.ApplicationUsers.AsNoTracking() on payment.CollectedByApplicationUserId equals user.Id
            where !filter.TicketNumber.HasValue || ticket.TicketNumber == filter.TicketNumber.Value
            select new FinancialListRow
            {
                TransactionType = FinancialTransactionType.Payment,
                TransactionId = payment.Id,
                TransactionNumber = payment.TransactionNumber,
                OriginalPaymentTransactionNumber = null,
                PatientId = patient.Id,
                PatientNameAr = patient.NameAr,
                PatientNameEn = patient.NameEn,
                DoctorId = doctor.Id,
                DoctorNameAr = doctor.NameAr,
                DoctorNameEn = doctor.NameEn,
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                DoctorPracticeId = practice.Id,
                PracticeNameAr = practice.NameAr,
                PracticeNameEn = practice.NameEn,
                Amount = payment.Amount,
                CurrencyCode = payment.CurrencyCode,
                Method = payment.PaymentMethod,
                ReferenceNumber = payment.ReferenceNumber,
                OccurredOnUtc = payment.CollectedOnUtc,
                BusinessDate = payment.BusinessDate,
                PerformedByApplicationUserId = user.Id,
                PerformedByDisplayName = user.UserName,
                IsCorrected = db.PaymentCorrectionHistories.Any(item => item.PaymentId == payment.Id),
                IsRefunded = db.Refunds.Any(item => item.PaymentId == payment.Id),
                RefundReasonCode = null,
                RefundReason = null
            };
        var refundRows = from refund in refunds
            join payment in db.Payments.AsNoTracking() on refund.PaymentId equals payment.Id
            join ticket in db.Tickets.AsNoTracking() on refund.TicketId equals ticket.Id
            join patient in db.Patients.AsNoTracking() on refund.PatientId equals patient.Id
            join doctor in db.Doctors.AsNoTracking() on refund.DoctorId equals doctor.Id
            join practice in db.DoctorPractices.AsNoTracking() on refund.DoctorPracticeId equals practice.Id
            join user in db.ApplicationUsers.AsNoTracking() on refund.RefundedByApplicationUserId equals user.Id
            where !filter.TicketNumber.HasValue || ticket.TicketNumber == filter.TicketNumber.Value
            select new FinancialListRow
            {
                TransactionType = FinancialTransactionType.Refund,
                TransactionId = refund.Id,
                TransactionNumber = refund.TransactionNumber,
                OriginalPaymentTransactionNumber = payment.TransactionNumber,
                PatientId = patient.Id,
                PatientNameAr = patient.NameAr,
                PatientNameEn = patient.NameEn,
                DoctorId = doctor.Id,
                DoctorNameAr = doctor.NameAr,
                DoctorNameEn = doctor.NameEn,
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                DoctorPracticeId = practice.Id,
                PracticeNameAr = practice.NameAr,
                PracticeNameEn = practice.NameEn,
                Amount = refund.Amount,
                CurrencyCode = refund.CurrencyCode,
                Method = refund.RefundMethod,
                ReferenceNumber = refund.ReferenceNumber,
                OccurredOnUtc = refund.RefundedOnUtc,
                BusinessDate = refund.BusinessDate,
                PerformedByApplicationUserId = user.Id,
                PerformedByDisplayName = user.UserName,
                IsCorrected = db.RefundCorrectionHistories.Any(item => item.RefundId == refund.Id),
                IsRefunded = true,
                RefundReasonCode = refund.RefundReasonCode,
                RefundReason = refund.Reason
            };
        return filter.TransactionType switch
        {
            FinancialTransactionType.Payment => paymentRows,
            FinancialTransactionType.Refund => refundRows,
            _ => paymentRows.Concat(refundRows)
        };
    }

    private static async Task<FinancialPage<FinancialListRow>> PageAsync(
        IQueryable<FinancialListRow> query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.OccurredOnUtc)
            .ThenByDescending(item => item.TransactionNumber)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(items, total, pageNumber, pageSize);
    }

    private static FinancialTransactionItem ToInternalItem(FinancialListRow item)
        => new(item.TransactionType, item.TransactionId, item.TransactionNumber,
            item.OriginalPaymentTransactionNumber, item.PatientId, item.PatientNameAr,
            item.PatientNameEn, item.TicketId, item.TicketNumber, item.DoctorPracticeId,
            item.PracticeNameAr, item.PracticeNameEn, item.Amount, item.CurrencyCode,
            item.Method, item.ReferenceNumber, item.OccurredOnUtc, item.BusinessDate,
            item.PerformedByApplicationUserId, item.PerformedByDisplayName, item.IsCorrected);

    private static PatientFinancialTransactionItem ToPatientItem(FinancialListRow item)
        => new(item.TransactionType, item.TransactionId, item.TransactionNumber,
            item.OriginalPaymentTransactionNumber, item.DoctorId, item.DoctorNameAr,
            item.DoctorNameEn, item.DoctorPracticeId, item.PracticeNameAr, item.PracticeNameEn,
            item.TicketNumber, item.Amount, item.CurrencyCode, item.Method, item.ReferenceNumber,
            item.OccurredOnUtc, item.BusinessDate, item.IsRefunded,
            item.RefundReasonCode, item.RefundReason);

    private static bool ValidFilter(FinancialTransactionFilter filter)
        => filter.PageNumber is >= 1 and <= 100000 && filter.PageSize is >= 1 and <= 100 &&
           (!filter.FromDate.HasValue || !filter.ToDate.HasValue || filter.FromDate <= filter.ToDate) &&
           (!filter.TransactionType.HasValue || Enum.IsDefined(filter.TransactionType.Value)) &&
           (!filter.Method.HasValue || Enum.IsDefined(filter.Method.Value)) &&
           filter.TicketNumber is null or > 0 &&
           (filter.TransactionNumber is null || filter.TransactionNumber.Length <= 100);

    private async Task<Result<UserAccessSnapshot>> AccessSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
            return Result<UserAccessSnapshot>.Fail(AccessDenied);
        var snapshot = await dataStore.GetAccessSnapshotAsync(userId, cancellationToken);
        return snapshot is null || !snapshot.User.IsActive || snapshot.User.IsFirstLogin
            ? Result<UserAccessSnapshot>.Fail(AccessDenied)
            : Result<UserAccessSnapshot>.Ok(snapshot);
    }

    private async Task<Result<Guid>> ResolveDoctorAsync(string permission, CancellationToken cancellationToken)
    {
        var access = await AccessSnapshotAsync(cancellationToken);
        return access.IsSuccess && access.Value.Roles.Contains(SystemRoleNames.Doctor) &&
               access.Value.Permissions.Contains(permission) &&
               access.Value.DoctorStatus == DoctorApprovalStatus.Approved &&
               access.Value.DoctorId is { } doctorId
            ? Result<Guid>.Ok(doctorId)
            : Result<Guid>.Fail(AccessDenied);
    }

    private async Task<Result<Guid>> ResolvePatientAsync(CancellationToken cancellationToken)
    {
        var access = await AccessSnapshotAsync(cancellationToken);
        return access.IsSuccess && access.Value.Roles.Contains(SystemRoleNames.Patient) &&
               access.Value.Permissions.Contains(PermissionNames.PaymentsViewOwn) &&
               access.Value.PatientId is { } patientId
            ? Result<Guid>.Ok(patientId)
            : Result<Guid>.Fail(AccessDenied);
    }

    private async Task<Result> AuthorizePracticeAsync(Guid practiceId, string doctorPermission,
        string receptionPermission, CancellationToken cancellationToken)
    {
        if (practiceId == Guid.Empty) return Result.Fail(AccessDenied);
        var access = await AccessSnapshotAsync(cancellationToken);
        if (access.IsFailure) return Result.Fail(access.Errors);
        if (access.Value.Roles.Contains(SystemRoleNames.Doctor))
        {
            return access.Value.DoctorStatus == DoctorApprovalStatus.Approved &&
                   access.Value.DoctorId is { } doctorId &&
                   access.Value.Permissions.Contains(doctorPermission) &&
                   await OwnsPracticeAsync(doctorId, practiceId, cancellationToken)
                ? Result.Ok() : Result.Fail(AccessDenied);
        }
        if (access.Value.Roles.Contains(SystemRoleNames.Reception) &&
            access.Value.Permissions.Contains(receptionPermission))
        {
            var delegated = await receptionAuthorization.AuthorizeAsync(
                practiceId, receptionPermission, cancellationToken);
            return delegated.IsSuccess ? Result.Ok() : Result.Fail(AccessDenied);
        }
        return Result.Fail(AccessDenied);
    }

    private Task<bool> OwnsPracticeAsync(Guid doctorId, Guid practiceId, CancellationToken cancellationToken)
        => db.DoctorPractices.AsNoTracking().AnyAsync(item => item.Id == practiceId &&
            item.DoctorId == doctorId, cancellationToken);

    private async Task<bool> CanRefundAsync(Guid practiceId, CancellationToken cancellationToken)
        => (await AuthorizePracticeAsync(practiceId, PermissionNames.DoctorPracticePaymentsRefundOwn,
            PermissionNames.PracticePaymentsRefund, cancellationToken)).IsSuccess;

    private sealed class FinancialListRow
    {
        public FinancialTransactionType TransactionType { get; init; }
        public Guid TransactionId { get; init; }
        public string TransactionNumber { get; init; } = string.Empty;
        public string? OriginalPaymentTransactionNumber { get; init; }
        public Guid PatientId { get; init; }
        public string PatientNameAr { get; init; } = string.Empty;
        public string? PatientNameEn { get; init; }
        public Guid DoctorId { get; init; }
        public string DoctorNameAr { get; init; } = string.Empty;
        public string? DoctorNameEn { get; init; }
        public Guid TicketId { get; init; }
        public int TicketNumber { get; init; }
        public Guid DoctorPracticeId { get; init; }
        public string PracticeNameAr { get; init; } = string.Empty;
        public string? PracticeNameEn { get; init; }
        public decimal Amount { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public PaymentMethod Method { get; init; }
        public string? ReferenceNumber { get; init; }
        public DateTime OccurredOnUtc { get; init; }
        public DateOnly BusinessDate { get; init; }
        public Guid PerformedByApplicationUserId { get; init; }
        public string PerformedByDisplayName { get; init; } = string.Empty;
        public bool IsCorrected { get; init; }
        public bool IsRefunded { get; init; }
        public RefundReasonCode? RefundReasonCode { get; init; }
        public string? RefundReason { get; init; }
    }
}
