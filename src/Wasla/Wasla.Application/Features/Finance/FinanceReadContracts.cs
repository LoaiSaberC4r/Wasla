using BuildingBlock.Domain.Results;
using System.Text.Json.Serialization;
using Wasla.Domain.Payments;

namespace Wasla.Application.Features.Finance;

public sealed record FinancialTransactionFilter(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? PracticeId = null,
    FinancialTransactionType? TransactionType = null,
    string? TransactionNumber = null,
    int? TicketNumber = null,
    Guid? PatientId = null,
    PaymentMethod? Method = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record FinancialPage<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record FinancialTransactionItem(
    FinancialTransactionType TransactionType,
    Guid TransactionId,
    string TransactionNumber,
    string? OriginalPaymentTransactionNumber,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid TicketId,
    int TicketNumber,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod Method,
    string? ReferenceNumber,
    DateTime OccurredOnUtc,
    DateOnly BusinessDate,
    Guid PerformedByApplicationUserId,
    string PerformedByDisplayName,
    bool IsCorrected);

public sealed record PatientFinancialTransactionItem(
    FinancialTransactionType TransactionType,
    Guid TransactionId,
    string TransactionNumber,
    string? OriginalPaymentTransactionNumber,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    int TicketNumber,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod Method,
    string? ReferenceNumber,
    DateTime OccurredOnUtc,
    DateOnly BusinessDate,
    bool IsRefunded,
    RefundReasonCode? RefundReasonCode,
    string? RefundReason);

public sealed record PaymentFinancialData(
    Guid Id,
    string TransactionNumber,
    Guid TicketId,
    Guid? ReservationId,
    decimal Amount,
    string CurrencyCode,
    PaymentStatus Status,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    DateOnly BusinessDate,
    DateTime CollectedOnUtc,
    Guid CollectedByApplicationUserId,
    string CollectedByDisplayName,
    string RowVersion);

public sealed record RefundFinancialData(
    Guid Id,
    string TransactionNumber,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    DateOnly BusinessDate,
    DateTime RefundedOnUtc,
    Guid RefundedByApplicationUserId,
    string RefundedByDisplayName,
    string RowVersion);

public sealed record FinancialCorrectionItem(
    Guid Id,
    DateTime CorrectedOnUtc,
    Guid CorrectedByApplicationUserId,
    string CorrectedByDisplayName,
    string CorrectionReason,
    PaymentMethod OldMethod,
    PaymentMethod NewMethod,
    string? OldReferenceNumber,
    string? NewReferenceNumber,
    string? OldNotes,
    string? NewNotes,
    RefundReasonCode? OldRefundReasonCode = null,
    RefundReasonCode? NewRefundReasonCode = null,
    string? OldReason = null,
    string? NewReason = null);

public sealed record FinancialPaymentDetail(
    PaymentFinancialData Payment,
    RefundFinancialData? Refund,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    Guid TicketId,
    int TicketNumber,
    Guid SegmentId,
    string SegmentNameAr,
    string? SegmentNameEn,
    Guid VisitTypeId,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    bool IsRefunded,
    bool CanRefund,
    decimal RefundableAmount,
    IReadOnlyList<FinancialCorrectionItem> PaymentCorrections,
    IReadOnlyList<FinancialCorrectionItem> RefundCorrections);

public sealed record PaymentReceiptData(
    string PaymentTransactionNumber,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    int TicketNumber,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    DateTime PaidOnUtc,
    DateOnly BusinessDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecordedByDisplayName);

public sealed record RefundReceiptData(
    string RefundTransactionNumber,
    string OriginalPaymentTransactionNumber,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    int TicketNumber,
    decimal Amount,
    string CurrencyCode,
    PaymentMethod RefundMethod,
    RefundReasonCode RefundReasonCode,
    string? RefundReason,
    string? ReferenceNumber,
    DateTime RefundedOnUtc,
    DateOnly BusinessDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RecordedByDisplayName);

public sealed record RevenueSummary(decimal GrossRevenue, decimal TotalRefunds, decimal NetRevenue,
    int PaymentCount, int RefundCount, string CurrencyCode);
public sealed record DailyRevenuePoint(DateOnly Date, decimal GrossRevenue, decimal Refunds, decimal NetRevenue);
public sealed record PracticeRevenuePoint(Guid PracticeId, string PracticeNameAr, string? PracticeNameEn,
    decimal GrossRevenue, decimal Refunds, decimal NetRevenue, int PaymentCount, int RefundCount);
public sealed record MethodDistributionPoint(PaymentMethod Method, decimal Amount, int TransactionCount);
public sealed record SegmentRevenuePoint(Guid SegmentId, string SegmentNameAr, string? SegmentNameEn,
    decimal GrossRevenue, decimal Refunds, decimal NetRevenue, int PaymentCount, int RefundCount);
public sealed record VisitTypeRevenuePoint(Guid VisitTypeId, string VisitTypeCode, string VisitTypeNameAr,
    string? VisitTypeNameEn, decimal GrossRevenue, decimal Refunds, decimal NetRevenue, int PaymentCount, int RefundCount);
public sealed record DoctorRevenueDashboard(
    RevenueSummary Summary,
    IReadOnlyList<DailyRevenuePoint> DailyTrend,
    IReadOnlyList<PracticeRevenuePoint> ByPractice,
    IReadOnlyList<MethodDistributionPoint> PaymentMethods,
    IReadOnlyList<MethodDistributionPoint> RefundMethods,
    IReadOnlyList<SegmentRevenuePoint> BySegment,
    IReadOnlyList<VisitTypeRevenuePoint> ByVisitType);
public sealed record PlatformRevenuePoint(Guid DoctorId, string DoctorNameAr, string? DoctorNameEn,
    Guid PracticeId, string PracticeNameAr, string? PracticeNameEn,
    decimal GrossRevenue, decimal Refunds, decimal NetRevenue, int PaymentCount, int RefundCount);
public sealed record PlatformRevenueDashboard(RevenueSummary Summary, IReadOnlyList<PlatformRevenuePoint> ByDoctorPractice);

public interface IFinanceReadService
{
    Task<Result<FinancialPage<FinancialTransactionItem>>> ListPracticeTransactionsAsync(
        Guid practiceId, FinancialTransactionFilter filter, CancellationToken cancellationToken);
    Task<Result<FinancialPage<FinancialTransactionItem>>> ListDoctorTransactionsAsync(
        FinancialTransactionFilter filter, CancellationToken cancellationToken);
    Task<Result<FinancialPage<PatientFinancialTransactionItem>>> ListOwnTransactionsAsync(
        FinancialTransactionFilter filter, CancellationToken cancellationToken);
    Task<Result<FinancialPaymentDetail>> GetPracticePaymentDetailAsync(
        Guid practiceId, Guid paymentId, CancellationToken cancellationToken);
    Task<Result<PaymentReceiptData>> GetPracticePaymentReceiptAsync(
        Guid practiceId, Guid paymentId, CancellationToken cancellationToken);
    Task<Result<RefundReceiptData>> GetPracticeRefundReceiptAsync(
        Guid practiceId, Guid refundId, CancellationToken cancellationToken);
    Task<Result<PaymentReceiptData>> GetOwnPaymentReceiptAsync(Guid paymentId, CancellationToken cancellationToken);
    Task<Result<RefundReceiptData>> GetOwnRefundReceiptAsync(Guid refundId, CancellationToken cancellationToken);
    Task<Result<DoctorRevenueDashboard>> GetDoctorDashboardAsync(
        DateOnly fromDate, DateOnly toDate, Guid? practiceId, CancellationToken cancellationToken);
    Task<Result<PlatformRevenueDashboard>> GetPlatformDashboardAsync(
        DateOnly fromDate, DateOnly toDate, Guid? doctorId, Guid? practiceId, CancellationToken cancellationToken);
}
