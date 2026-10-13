using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class OperatingFinanceService
{
    public async Task<Result<ReportPage<OperatingPlanResponse>>> ListPlansAsync(OperatingPlanKind kind, OperatingFinanceFilter filter, CancellationToken ct)
    {
        var scope = await AccessAsync(kind == OperatingPlanKind.Salary ? Phase18PermissionNames.SalaryPlansView : Phase18PermissionNames.ExpensesView, filter.PracticeId, ct);
        if (scope.IsFailure) return Result<ReportPage<OperatingPlanResponse>>.Fail(scope.Errors);
        if (!ValidFilter(filter)) return Result<ReportPage<OperatingPlanResponse>>.Fail(ReportingErrors.InvalidFilter);
        var query = db.OperatingFinancialPlans.AsNoTracking().Where(p => p.DoctorId == scope.Value.DoctorId && p.Kind == kind &&
            (filter.ReceptionId == null || p.ReceptionId == filter.ReceptionId) &&
            (filter.PlanStatus == null || p.Status == filter.PlanStatus) &&
            (filter.FromDate == null || p.EffectiveFrom >= filter.FromDate) && (filter.ToDate == null || p.EffectiveFrom <= filter.ToDate));
        if (filter.PracticeId is { } practice) query = query.Where(p => p.Revisions.Any(r => r.Allocations.Any(a => a.PracticeId == practice)));
        var count = await query.LongCountAsync(ct);
        var rows = await (filter.SortDirection == "asc" ? query.OrderBy(p => p.CreatedOnUtc).ThenBy(p => p.Id)
                : query.OrderByDescending(p => p.CreatedOnUtc).ThenBy(p => p.Id))
            .Skip(Offset(filter)).Take(filter.PageSize).Include(p => p.Revisions.OrderByDescending(r => r.RevisionNumber).Take(20))
            .ThenInclude(r => r.Allocations).AsSplitQuery().ToArrayAsync(ct);
        return Result<ReportPage<OperatingPlanResponse>>.Ok(new(rows.Select(p => PlanResponse(p, scope.Value)).ToArray(), count, filter.PageNumber, filter.PageSize));
    }
    public async Task<Result<OperatingPlanResponse>> PlanAsync(Guid id, OperatingPlanKind kind, CancellationToken ct)
    {
        var scope = await AccessAsync(kind == OperatingPlanKind.Salary ? Phase18PermissionNames.SalaryPlansView : Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) return Result<OperatingPlanResponse>.Fail(scope.Errors);
        var p = await db.OperatingFinancialPlans.AsNoTracking().Include(p => p.Revisions.OrderByDescending(r => r.RevisionNumber).Take(20))
            .ThenInclude(r => r.Allocations).AsSplitQuery().SingleOrDefaultAsync(p => p.Id == id && p.DoctorId == scope.Value.DoctorId && p.Kind == kind, ct);
        return p is null ? Result<OperatingPlanResponse>.Fail(ReportingErrors.NotFound) : Result<OperatingPlanResponse>.Ok(PlanResponse(p, scope.Value));
    }
    public async Task<Result<ReportPage<OperatingObligationResponse>>> ListObligationsAsync(bool salary, bool oneTimeOnly,
        OperatingFinanceFilter filter, CancellationToken ct)
    {
        var scope = await AccessAsync(salary ? Phase18PermissionNames.SalaryObligationsView : Phase18PermissionNames.ExpensesView, filter.PracticeId, ct);
        if (scope.IsFailure) return Result<ReportPage<OperatingObligationResponse>>.Fail(scope.Errors);
        if (!ValidFilter(filter)) return Result<ReportPage<OperatingObligationResponse>>.Fail(ReportingErrors.InvalidFilter);
        if (filter.PlanId is { } planId && !await db.OperatingFinancialPlans.AnyAsync(p => p.Id == planId && p.DoctorId == scope.Value.DoctorId &&
                (p.Kind == OperatingPlanKind.Salary) == salary, ct)) return Result<ReportPage<OperatingObligationResponse>>.Fail(ReportingErrors.NotFound);
        var query = db.OperatingObligations.AsNoTracking().Where(o => o.DoctorId == scope.Value.DoctorId &&
            (o.Kind == OperatingObligationKind.Salary) == salary && (!oneTimeOnly || o.Kind == OperatingObligationKind.OneTimeExpense) &&
            (filter.PlanId == null || o.PlanId == filter.PlanId) && (filter.ReceptionId == null || o.ReceptionId == filter.ReceptionId) &&
            (filter.Category == null || o.Category == filter.Category) && (filter.ObligationStatus == null || o.Status == filter.ObligationStatus) &&
            (filter.FromDate == null || o.DueBusinessDate >= filter.FromDate) && (filter.ToDate == null || o.DueBusinessDate <= filter.ToDate));
        if (filter.PracticeId is { } practice) query = query.Where(o => o.Revisions.Any(r => r.RevisionNumber == o.CurrentRevisionNumber && r.Allocations.Any(a => a.PracticeId == practice)));
        var count = await query.LongCountAsync(ct);
        var ordered = filter.SortBy == "Amount" ? (filter.SortDirection == "asc" ? query.OrderBy(o => o.AmountMinorUnits) : query.OrderByDescending(o => o.AmountMinorUnits))
            : filter.SortBy == "Status" ? (filter.SortDirection == "asc" ? query.OrderBy(o => o.Status) : query.OrderByDescending(o => o.Status))
            : filter.SortDirection == "asc" ? query.OrderBy(o => o.DueBusinessDate) : query.OrderByDescending(o => o.DueBusinessDate);
        var rows = await ordered.ThenBy(o => o.Id).Skip(Offset(filter)).Take(filter.PageSize)
            .Include(o => o.Revisions.OrderByDescending(r => r.RevisionNumber).Take(1)).ThenInclude(r => r.Allocations).AsSplitQuery().ToArrayAsync(ct);
        return Result<ReportPage<OperatingObligationResponse>>.Ok(new(rows.Select(o => ObligationResponse(o, scope.Value)).ToArray(), count, filter.PageNumber, filter.PageSize));
    }
    public async Task<Result<OperatingObligationResponse>> ObligationAsync(Guid id, bool salary, CancellationToken ct)
    {
        var scope = await AccessAsync(salary ? Phase18PermissionNames.SalaryObligationsView : Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) return Result<OperatingObligationResponse>.Fail(scope.Errors);
        var o = await db.OperatingObligations.AsNoTracking().Include(o => o.Revisions.OrderByDescending(r => r.RevisionNumber).Take(1))
            .ThenInclude(r => r.Allocations).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id && o.DoctorId == scope.Value.DoctorId &&
                (o.Kind == OperatingObligationKind.Salary) == salary, ct);
        return o is null ? Result<OperatingObligationResponse>.Fail(ReportingErrors.NotFound) : Result<OperatingObligationResponse>.Ok(ObligationResponse(o, scope.Value));
    }
    public async Task<Result<OperatingSettlementResponse>> SettlementAsync(Guid id, bool salary, CancellationToken ct)
    {
        var scope = await AccessAsync(salary ? Phase18PermissionNames.SalaryObligationsView : Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) return Result<OperatingSettlementResponse>.Fail(scope.Errors);
        var s = await db.OperatingSettlements.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id && s.DoctorId == scope.Value.DoctorId, ct);
        if (s is null) return Result<OperatingSettlementResponse>.Fail(ReportingErrors.NotFound);
        var o = await db.OperatingObligations.AsNoTracking().SingleAsync(o => o.Id == s.ObligationId, ct);
        if ((o.Kind == OperatingObligationKind.Salary) != salary) return Result<OperatingSettlementResponse>.Fail(ReportingErrors.NotFound);
        var r = await db.OperatingObligationRevisions.AsNoTracking().Include(r => r.Allocations).SingleAsync(r => r.Id == s.ObligationRevisionId, ct);
        var reversal = await db.OperatingSettlementReversals.AsNoTracking().SingleOrDefaultAsync(r => r.OriginalSettlementId == id, ct);
        return Result<OperatingSettlementResponse>.Ok(new(s.Id, o.Id, r.Id, s.ReceiptNumber, s.AmountMinorUnits / 100m,
            s.CurrencyCode, s.PaymentMethod, s.Reference, s.BusinessDate, s.ReportTimeZoneId, s.BackdateReason,
            s.ActorApplicationUserId, s.RecordedAtUtc, reversal != null, reversal?.Id, Convert.ToBase64String(o.RowVersion),
            r.DescriptionAr, r.DescriptionEn, r.Allocations.Select(a => new OperatingAllocationResponse(a.PracticeId, a.Percentage, a.AmountMinorUnits / 100m)).ToArray(),
            [new("Reverse", reversal == null && o.ActiveSettlementId == id && scope.Value.Permissions.Contains(salary ? Phase18PermissionNames.SalaryReverse : Phase18PermissionNames.ExpensesReverse),
                SettlementRoute(o.Kind, id) + "/reverse"), new("Receipt", true, SettlementRoute(o.Kind, id) + "/receipt")]));
    }
    public async Task<Result<ReportPage<OperatingHistoryResponse>>> HistoryAsync(Guid id, string resourceType, bool salary,
        int pageNumber, int pageSize, CancellationToken ct)
    {
        var scope = await AccessAsync(salary ? resourceType == "Plan" ? Phase18PermissionNames.SalaryPlansView : Phase18PermissionNames.SalaryObligationsView
            : Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) return Result<ReportPage<OperatingHistoryResponse>>.Fail(scope.Errors);
        if (pageNumber < 1 || pageNumber > 1_000_000 || pageSize is < 1 or > 100 || resourceType is not ("Plan" or "Obligation" or "Settlement"))
            return Result<ReportPage<OperatingHistoryResponse>>.Fail(ReportingErrors.InvalidFilter);
        var exists = resourceType switch
        {
            "Plan" => await db.OperatingFinancialPlans.AnyAsync(p => p.Id == id && p.DoctorId == scope.Value.DoctorId && (p.Kind == OperatingPlanKind.Salary) == salary, ct),
            "Obligation" => await db.OperatingObligations.AnyAsync(o => o.Id == id && o.DoctorId == scope.Value.DoctorId && (o.Kind == OperatingObligationKind.Salary) == salary, ct),
            _ => await db.OperatingSettlements.AnyAsync(s => s.Id == id && s.DoctorId == scope.Value.DoctorId && db.OperatingObligations.Any(o => o.Id == s.ObligationId && (o.Kind == OperatingObligationKind.Salary) == salary), ct)
        };
        if (!exists) return Result<ReportPage<OperatingHistoryResponse>>.Fail(ReportingErrors.NotFound);
        var query = db.OperatingFinanceHistories.AsNoTracking().Where(h => h.DoctorId == scope.Value.DoctorId && h.ResourceId == id && h.ResourceType == resourceType);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(h => h.RecordedAtUtc).ThenBy(h => h.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(h => new OperatingHistoryResponse(h.Id, h.ResourceType, h.ResourceId, h.Operation, h.Reason, h.ActorApplicationUserId, h.RecordedAtUtc)).ToArrayAsync(ct);
        return Result<ReportPage<OperatingHistoryResponse>>.Ok(new(rows, count, pageNumber, pageSize));
    }
    public async Task<Result<ReportPage<OperatingMovementResponse>>> MovementsAsync(OperatingFinanceFilter filter, CancellationToken ct)
    {
        var scope = await AccessAsync(Phase18PermissionNames.ExpensesView, filter.PracticeId, ct);
        if (scope.IsFailure) return Result<ReportPage<OperatingMovementResponse>>.Fail(scope.Errors);
        if (!ValidFilter(filter)) return Result<ReportPage<OperatingMovementResponse>>.Fail(ReportingErrors.InvalidFilter);
        var eligible = db.OperatingObligations.Where(o => o.DoctorId == scope.Value.DoctorId && o.Kind != OperatingObligationKind.Salary &&
            (filter.PlanId == null || o.PlanId == filter.PlanId) && (filter.Category == null || o.Category == filter.Category)).Select(o => o.Id);
        var payments = db.OperatingSettlements.Where(s => eligible.Contains(s.ObligationId) &&
            (filter.FromDate == null || s.BusinessDate >= filter.FromDate) && (filter.ToDate == null || s.BusinessDate <= filter.ToDate) &&
            (filter.PracticeId == null || db.OperatingCostAllocationSnapshots.Any(a => a.RevisionId == s.ObligationRevisionId && a.PracticeId == filter.PracticeId)))
            .Select(s => new OperatingMovementResponse(s.Id, s.ObligationId, "Settlement", s.AmountMinorUnits / 100m, s.BusinessDate, s.RecordedAtUtc, null));
        var reversals = db.OperatingSettlementReversals.Where(r => eligible.Contains(r.ObligationId) &&
            (filter.FromDate == null || r.BusinessDate >= filter.FromDate) && (filter.ToDate == null || r.BusinessDate <= filter.ToDate) &&
            (filter.PracticeId == null || db.OperatingSettlements.Any(s => s.Id == r.OriginalSettlementId &&
                db.OperatingCostAllocationSnapshots.Any(a => a.RevisionId == s.ObligationRevisionId && a.PracticeId == filter.PracticeId))))
            .Select(r => new OperatingMovementResponse(r.Id, r.ObligationId, "Reversal", -r.AmountMinorUnits / 100m, r.BusinessDate, r.RecordedAtUtc, r.OriginalSettlementId));
        var query = payments.Concat(reversals);
        var count = await query.LongCountAsync(ct);
        var rows = await query.OrderByDescending(r => r.BusinessDate).ThenBy(r => r.Id).Skip(Offset(filter)).Take(filter.PageSize).ToArrayAsync(ct);
        return Result<ReportPage<OperatingMovementResponse>>.Ok(new(rows, count, filter.PageNumber, filter.PageSize));
    }
    public async Task<Result<OperatingFinanceOptions>> OptionsAsync(CancellationToken ct)
    {
        var scope = await AccessAsync(Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) scope = await AccessAsync(Phase18PermissionNames.SalaryPlansView, null, ct);
        if (scope.IsFailure) return Result<OperatingFinanceOptions>.Fail(scope.Errors);
        var practices = await db.DoctorPractices.AsNoTracking().Where(p => p.DoctorId == scope.Value.DoctorId)
            .OrderBy(p => p.NameAr).Select(p => new ReportOption(p.Id.ToString(), p.NameAr, p.NameEn, p.Id)).ToArrayAsync(ct);
        var receptions = await db.Receptions.AsNoTracking().Where(r => r.OwnerDoctorId == scope.Value.DoctorId &&
            db.ApplicationUsers.Any(u => u.Id == r.ApplicationUserId && u.IsActive) && db.ReceptionPracticeAssignments.Any(a => a.ReceptionId == r.Id && a.IsActive))
            .OrderBy(r => r.NameAr).Select(r => new ReportOption(r.Id.ToString(), r.NameAr, r.NameEn, r.Id)).ToArrayAsync(ct);
        return Result<OperatingFinanceOptions>.Ok(new(practices, receptions, Categories(),
            [new("Cash", "نقدي", "Cash"), new("Card", "بطاقة", "Card"), new("Wallet", "محفظة", "Wallet"), new("BankTransfer", "تحويل بنكي مسجل", "Recorded bank transfer")],
            [new("Monthly", "شهري", "Monthly"), new("Quarterly", "ربع سنوي", "Quarterly"), new("Yearly", "سنوي", "Yearly")],
            [new("Fixed", "ثابت", "Fixed"), new("Variable", "متغير", "Variable")], "EGP", true, options.Value.DefaultReportTimeZoneId));
    }
    internal static ReportOption[] Categories() => [new("Rent", "إيجار", "Rent"), new("Utilities", "مرافق", "Utilities"),
        new("Supplies", "مستلزمات", "Supplies"), new("Maintenance", "صيانة", "Maintenance"), new("Transport", "انتقالات", "Transport"), new("Other", "أخرى", "Other")];
    private Task<Result<ReportScope>> AccessAsync(string permission, Guid? practiceId, CancellationToken ct)
        => guard.AuthorizeAsync(ReportActor.Doctor, permission, practiceId, null, ct);
    private static bool ValidFilter(OperatingFinanceFilter f) => f.PageNumber is >= 1 and <= 1_000_000 && f.PageSize is >= 1 and <= 100 &&
        f.SortBy is "Date" or "Status" or "Amount" && f.SortDirection is "asc" or "desc" &&
        (f.FromDate == null || f.ToDate == null || f.FromDate <= f.ToDate && f.ToDate.Value.DayNumber - f.FromDate.Value.DayNumber < 366) &&
        (f.Category == null || Enum.IsDefined(f.Category.Value)) && (f.PlanStatus == null || Enum.IsDefined(f.PlanStatus.Value)) &&
        (f.ObligationStatus == null || Enum.IsDefined(f.ObligationStatus.Value));
    private static int Offset(OperatingFinanceFilter f) => (f.PageNumber - 1) * f.PageSize;
    private static OperatingPlanResponse PlanResponse(OperatingFinancialPlan p, ReportScope scope)
    {
        var manage = scope.Permissions.Contains(p.Kind == OperatingPlanKind.Salary ? Phase18PermissionNames.SalaryPlansManage : Phase18PermissionNames.ExpensesManage);
        return new(p.Id, p.DoctorId, p.ReceptionId, p.Kind, p.Status, p.FirstCoveredMonth, true, p.EffectiveFrom, p.EffectiveTo,
            p.Revisions.OrderByDescending(r => r.RevisionNumber).Select(r => new OperatingPlanRevisionResponse(r.Id, r.RevisionNumber,
                r.Amount, r.CurrencyCode, r.DueDay, r.EffectiveFrom, r.EffectiveTo, r.Cadence, r.AmountPolicy, r.Category,
                r.DescriptionAr, r.DescriptionEn, r.Status, r.IsLifecycleChange, r.Allocations.Select(a => new OperatingAllocationResponse(a.PracticeId, a.Percentage, null)).ToArray(),
                r.RecordedAtUtc, r.ActorApplicationUserId, r.Reason)).ToArray(), Convert.ToBase64String(p.RowVersion),
            [new("Revise", manage && p.Status != OperatingPlanStatus.Ended), new("End", manage && p.Status != OperatingPlanStatus.Ended),
             new("Pause", manage && p.Kind == OperatingPlanKind.RecurringExpense && p.Status == OperatingPlanStatus.Active),
             new("Resume", manage && p.Kind == OperatingPlanKind.RecurringExpense && p.Status == OperatingPlanStatus.Paused)]);
    }
    private OperatingObligationResponse ObligationResponse(OperatingObligation o, ReportScope scope)
    {
        var r = o.Revisions.OrderByDescending(r => r.RevisionNumber).First();
        var today = Today(clock.UtcNow, options.Value.DefaultReportTimeZoneId);
        var salary = o.Kind == OperatingObligationKind.Salary;
        var unpaid = o.Status == OperatingObligationStatus.Unpaid;
        var manage = scope.Permissions.Contains(salary ? Phase18PermissionNames.SalaryPlansManage : Phase18PermissionNames.ExpensesManage);
        return new(o.Id, o.DoctorId, o.PlanId, o.ReceptionId, o.Kind, o.OccurrenceKey, o.PeriodYear, o.PeriodMonth,
            o.BusinessDate, o.DueBusinessDate, o.PlannedAmountMinorUnits / 100m, o.AmountMinorUnits / 100m, o.CurrencyCode,
            o.AmountPolicy, o.ActualAmountConfirmed, o.Category, o.DescriptionAr, o.DescriptionEn, o.Status,
            !unpaid ? o.Status.ToString() : o.DueBusinessDate < today ? "Overdue" : o.DueBusinessDate == today ? "Due" : "Upcoming",
            o.ActiveSettlementId, r.Allocations.Select(a => new OperatingAllocationResponse(a.PracticeId, a.Percentage, a.AmountMinorUnits / 100m)).ToArray(),
            o.RecordedAtUtc, Convert.ToBase64String(o.RowVersion), [new("Correct", unpaid && manage), new("Cancel", unpaid && manage),
                new("ConfirmActual", unpaid && manage && o.AmountPolicy == ExpenseAmountPolicy.Variable),
                new("Settle", unpaid && o.ActualAmountConfirmed && scope.Permissions.Contains(salary ? Phase18PermissionNames.SalarySettle : Phase18PermissionNames.ExpensesSettle))]);
    }
}
