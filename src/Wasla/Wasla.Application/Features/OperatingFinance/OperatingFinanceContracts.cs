using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Application.Features.OperatingFinance;

public sealed record OperatingPlanRequest(decimal Amount, int DueDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    IReadOnlyCollection<PracticeAllocationPercentage> Allocations, string DescriptionAr, string? DescriptionEn = null,
    Guid? ReceptionId = null, ExpenseCategoryCode Category = ExpenseCategoryCode.Other,
    ExpenseCadence Cadence = ExpenseCadence.Monthly, ExpenseAmountPolicy AmountPolicy = ExpenseAmountPolicy.Fixed,
    string? RowVersion = null, string? Reason = null);
public sealed record OperatingSettlementRequest(decimal Amount, DateOnly PaidBusinessDate, OperatingPaymentMethod PaymentMethod,
    string? RowVersion = null, string? Reference = null, string? BackdateReason = null, string? ReportTimeZoneId = null);
public sealed record OperatingExpenseRequest(decimal Amount, DateOnly BusinessDate, DateOnly DueBusinessDate,
    ExpenseCategoryCode Category, string DescriptionAr, IReadOnlyCollection<PracticeAllocationPercentage> Allocations,
    string Reason, string? DescriptionEn = null, bool PaidNow = false, OperatingSettlementRequest? Payment = null);
public sealed record OperatingCorrectionRequest(decimal Amount, DateOnly DueBusinessDate, ExpenseCategoryCode Category,
    string DescriptionAr, IReadOnlyCollection<PracticeAllocationPercentage> Allocations, string Reason,
    string RowVersion, string? DescriptionEn = null);
public sealed record OperatingActualAmountRequest(decimal Amount, string RowVersion, string? Reason = null);
public sealed record OperatingTransitionRequest(string RowVersion, string Reason);
public sealed record OperatingReversalRequest(DateOnly BusinessDate, string RowVersion, string Reason, string? ReportTimeZoneId = null);
public sealed record OperatingFinanceFilter(DateOnly? FromDate = null, DateOnly? ToDate = null, Guid? PracticeId = null,
    Guid? ReceptionId = null, Guid? PlanId = null, OperatingPlanStatus? PlanStatus = null,
    OperatingObligationStatus? ObligationStatus = null, ExpenseCategoryCode? Category = null,
    int PageNumber = 1, int PageSize = 20, string SortBy = "Date", string SortDirection = "desc");
public enum OperatingFinanceOperation { CreateSalaryPlan = 1, ReviseSalaryPlan = 2, EndSalaryPlan = 3,
    CreateRecurringPlan = 4, ReviseRecurringPlan = 5, PauseRecurringPlan = 6, ResumeRecurringPlan = 7,
    EndRecurringPlan = 8, CreateExpense = 9, CorrectObligation = 10, ConfirmActual = 11,
    CancelObligation = 12, SettleSalary = 13, SettleExpense = 14, ReverseSalary = 15, ReverseExpense = 16 }

public sealed record WriteOperatingFinanceCommand(OperatingFinanceOperation Operation, Guid? ResourceId,
    string IdempotencyKey, OperatingPlanRequest? Plan = null, OperatingExpenseRequest? Expense = null,
    OperatingCorrectionRequest? Correction = null, OperatingActualAmountRequest? Actual = null,
    OperatingTransitionRequest? Transition = null, OperatingSettlementRequest? Settlement = null,
    OperatingReversalRequest? Reversal = null, bool SalaryObligation = false)
    : ICommand<OperatingFinanceWriteResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record OperatingFinanceWriteResponse(Guid Id, string ResourceType, string DetailRoute,
    string? RowVersion = null, Guid? ObligationId = null, Guid? SettlementId = null);
public sealed record OperatingAllocationResponse(Guid PracticeId, decimal Percentage, decimal? Amount);
public sealed record OperatingPlanRevisionResponse(Guid Id, int RevisionNumber, decimal Amount, string CurrencyCode,
    int DueDay, DateOnly EffectiveFrom, DateOnly? EffectiveTo, ExpenseCadence Cadence, ExpenseAmountPolicy AmountPolicy,
    ExpenseCategoryCode Category, string DescriptionAr, string? DescriptionEn, OperatingPlanStatus Status,
    bool IsLifecycleChange, IReadOnlyList<OperatingAllocationResponse> Allocations, DateTime RecordedAtUtc,
    Guid ActorApplicationUserId, string Reason);
public sealed record OperatingPlanResponse(Guid Id, Guid DoctorId, Guid? ReceptionId, OperatingPlanKind Kind,
    OperatingPlanStatus Status, DateOnly FirstCoveredMonth, bool NoProration, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, IReadOnlyList<OperatingPlanRevisionResponse> Revisions, string RowVersion,
    IReadOnlyList<ReportAction> AvailableActions);
public sealed record OperatingObligationResponse(Guid Id, Guid DoctorId, Guid? PlanId, Guid? ReceptionId,
    OperatingObligationKind Kind, string? OccurrenceKey, int? PeriodYear, int? PeriodMonth,
    DateOnly BusinessDate, DateOnly DueBusinessDate, decimal PlannedAmount, decimal Amount, string CurrencyCode,
    ExpenseAmountPolicy AmountPolicy, bool ActualAmountConfirmed, ExpenseCategoryCode Category,
    string DescriptionAr, string? DescriptionEn, OperatingObligationStatus Status, string DueState,
    Guid? ActiveSettlementId, IReadOnlyList<OperatingAllocationResponse> Allocations, DateTime RecordedAtUtc,
    string RowVersion, IReadOnlyList<ReportAction> AvailableActions);
public sealed record OperatingSettlementResponse(Guid Id, Guid ObligationId, Guid ObligationRevisionId,
    string ReceiptNumber, decimal Amount, string CurrencyCode, OperatingPaymentMethod PaymentMethod,
    string? Reference, DateOnly BusinessDate, string ReportTimeZoneId, string? BackdateReason,
    Guid ActorApplicationUserId, DateTime RecordedAtUtc, bool IsReversed, Guid? ReversalId,
    string ObligationRowVersion, string DescriptionAr, string? DescriptionEn,
    IReadOnlyList<OperatingAllocationResponse> Allocations, IReadOnlyList<ReportAction> AvailableActions);
public sealed record OperatingMovementResponse(Guid Id, Guid ObligationId, string MovementType, decimal Amount,
    DateOnly BusinessDate, DateTime RecordedAtUtc, Guid? OriginalSettlementId);
public sealed record OperatingHistoryResponse(Guid Id, string ResourceType, Guid ResourceId, string Operation,
    string? Reason, Guid ActorApplicationUserId, DateTime RecordedAtUtc);
public sealed record OperatingFinanceOptions(IReadOnlyList<ReportOption> Practices, IReadOnlyList<ReportOption> Receptions,
    IReadOnlyList<ReportOption> Categories, IReadOnlyList<ReportOption> PaymentMethods,
    IReadOnlyList<ReportOption> Cadences, IReadOnlyList<ReportOption> AmountPolicies,
    string CurrencyCode, bool NoProration, string DefaultReportTimeZoneId);

public interface IOperatingFinanceService
{
    Task<Result<OperatingFinanceWriteResponse>> ExecuteAsync(WriteOperatingFinanceCommand command, CancellationToken cancellationToken);
    Task<Result<ReportPage<OperatingPlanResponse>>> ListPlansAsync(OperatingPlanKind kind, OperatingFinanceFilter filter, CancellationToken cancellationToken);
    Task<Result<OperatingPlanResponse>> PlanAsync(Guid id, OperatingPlanKind kind, CancellationToken cancellationToken);
    Task<Result<ReportPage<OperatingObligationResponse>>> ListObligationsAsync(bool salary, bool oneTimeOnly,
        OperatingFinanceFilter filter, CancellationToken cancellationToken);
    Task<Result<OperatingObligationResponse>> ObligationAsync(Guid id, bool salary, CancellationToken cancellationToken);
    Task<Result<OperatingSettlementResponse>> SettlementAsync(Guid id, bool salary, CancellationToken cancellationToken);
    Task<Result<ReportPage<OperatingHistoryResponse>>> HistoryAsync(Guid id, string resourceType, bool salary,
        int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<ReportPage<OperatingMovementResponse>>> MovementsAsync(OperatingFinanceFilter filter, CancellationToken cancellationToken);
    Task<Result<OperatingFinanceOptions>> OptionsAsync(CancellationToken cancellationToken);
}
public interface IOperatingObligationGenerator { Task<int> GenerateAsync(Guid? doctorId, CancellationToken cancellationToken); }

internal sealed class WriteOperatingFinanceHandler(IOperatingFinanceService service)
    : ICommandHandler<WriteOperatingFinanceCommand, OperatingFinanceWriteResponse>
{
    public Task<Result<OperatingFinanceWriteResponse>> Handle(WriteOperatingFinanceCommand request, CancellationToken cancellationToken)
        => service.ExecuteAsync(request, cancellationToken);
}
