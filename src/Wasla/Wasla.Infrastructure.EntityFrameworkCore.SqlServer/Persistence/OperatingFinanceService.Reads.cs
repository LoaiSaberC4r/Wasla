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
            (filter.PlanId == null || p.Id == filter.PlanId) &&
            (filter.PlanStatus == null || p.Status == filter.PlanStatus) &&
            (filter.Category == null || p.Revisions.Any(r => r.RevisionNumber == p.CurrentRevisionNumber && r.Category == filter.Category)) &&
            (filter.FromDate == null || p.EffectiveFrom >= filter.FromDate) && (filter.ToDate == null || p.EffectiveFrom <= filter.ToDate));
        if (filter.PracticeId is { } practice) query = query.Where(p => p.Revisions.Any(r => r.RevisionNumber == p.CurrentRevisionNumber && r.Allocations.Any(a => a.PracticeId == practice)));
        var count = await query.LongCountAsync(ct);
        var ordered = filter.SortBy == "Amount" ? (filter.SortDirection == "asc"
                ? query.OrderBy(p => p.Revisions.Where(r => r.RevisionNumber == p.CurrentRevisionNumber).Select(r => r.Amount).First())
                : query.OrderByDescending(p => p.Revisions.Where(r => r.RevisionNumber == p.CurrentRevisionNumber).Select(r => r.Amount).First()))
            : filter.SortBy == "Status" ? (filter.SortDirection == "asc" ? query.OrderBy(p => p.Status) : query.OrderByDescending(p => p.Status))
            : filter.SortDirection == "asc" ? query.OrderBy(p => p.EffectiveFrom) : query.OrderByDescending(p => p.EffectiveFrom);
        var rows = await ordered.ThenBy(p => p.Id)
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
            .Select(h => new OperatingHistoryResponse(h.Id, h.ResourceType, h.ResourceId, h.Operation, h.Reason, h.ActorApplicationUserId, h.RecordedAtUtc, h.SnapshotRevisionId, null, null)).ToArrayAsync(ct);
        var revisionIds = rows.Where(h => h.SnapshotRevisionId != null).Select(h => h.SnapshotRevisionId!.Value).ToArray();
        var planSnapshots = await db.OperatingPlanRevisions.AsNoTracking().Where(r => revisionIds.Contains(r.Id) && r.DoctorId == scope.Value.DoctorId)
            .Include(r => r.Allocations).ToArrayAsync(ct);
        var obligationSnapshots = await db.OperatingObligationRevisions.AsNoTracking().Where(r => revisionIds.Contains(r.Id) && r.DoctorId == scope.Value.DoctorId)
            .Include(r => r.Allocations).ToArrayAsync(ct);
        rows = rows.Select(h => h with
        {
            PlanSnapshot = planSnapshots.Where(r => r.Id == h.SnapshotRevisionId).Select(r => new OperatingPlanRevisionResponse(r.Id, r.RevisionNumber,
                r.Amount, r.CurrencyCode, r.DueDay, r.EffectiveFrom, r.EffectiveTo, r.Cadence, r.AmountPolicy, r.Category, r.DescriptionAr, r.DescriptionEn,
                r.Status, r.IsLifecycleChange, r.Allocations.Select(a => new OperatingAllocationResponse(a.PracticeId, a.Percentage, null)).ToArray(),
                DateTime.SpecifyKind(r.RecordedAtUtc, DateTimeKind.Utc), r.ActorApplicationUserId, r.Reason)).SingleOrDefault(),
            ObligationSnapshot = obligationSnapshots.Where(r => r.Id == h.SnapshotRevisionId).Select(r => new OperatingObligationRevisionResponse(r.Id, r.RevisionNumber,
                r.AmountMinorUnits / 100m, r.DueBusinessDate, r.Category, r.DescriptionAr, r.DescriptionEn, r.ActualAmountConfirmed, r.Status,
                r.Reason, r.ActorApplicationUserId, DateTime.SpecifyKind(r.RecordedAtUtc, DateTimeKind.Utc),
                r.Allocations.Select(a => new OperatingAllocationResponse(a.PracticeId, a.Percentage, a.AmountMinorUnits / 100m)).ToArray())).SingleOrDefault()
        }).ToArray();
        return Result<ReportPage<OperatingHistoryResponse>>.Ok(new(rows, count, pageNumber, pageSize));
    }
    public async Task<Result<ReportPage<OperatingMovementResponse>>> MovementsAsync(OperatingFinanceFilter filter, CancellationToken ct)
    {
        var scope = await AccessAsync(Phase18PermissionNames.ExpensesView, filter.PracticeId, ct);
        if (scope.IsFailure) return Result<ReportPage<OperatingMovementResponse>>.Fail(scope.Errors);
        if (!ValidFilter(filter)) return Result<ReportPage<OperatingMovementResponse>>.Fail(ReportingErrors.InvalidFilter);
        var eligible = db.OperatingObligations.Where(o => o.DoctorId == scope.Value.DoctorId && o.Kind != OperatingObligationKind.Salary &&
            (filter.PlanId == null || o.PlanId == filter.PlanId)).Select(o => o.Id);
        var payments = db.OperatingSettlements.Where(s => eligible.Contains(s.ObligationId) &&
            (filter.Category == null || db.OperatingObligationRevisions.Any(r => r.Id == s.ObligationRevisionId && r.Category == filter.Category)) &&
            (filter.FromDate == null || s.BusinessDate >= filter.FromDate) && (filter.ToDate == null || s.BusinessDate <= filter.ToDate) &&
            (filter.PracticeId == null || db.OperatingCostAllocationSnapshots.Any(a => a.RevisionId == s.ObligationRevisionId && a.PracticeId == filter.PracticeId)))
            .Select(s => new { s.Id, s.ObligationId, MovementType = "Settlement", MinorUnits = filter.PracticeId == null ? s.AmountMinorUnits :
                db.OperatingCostAllocationSnapshots.Where(a => a.RevisionId == s.ObligationRevisionId && a.PracticeId == filter.PracticeId).Sum(a => a.AmountMinorUnits),
                s.BusinessDate, s.RecordedAtUtc, OriginalSettlementId = (Guid?)null });
        var reversals = db.OperatingSettlementReversals.Where(r => eligible.Contains(r.ObligationId) &&
            (filter.Category == null || db.OperatingSettlements.Any(s => s.Id == r.OriginalSettlementId && db.OperatingObligationRevisions.Any(revision => revision.Id == s.ObligationRevisionId && revision.Category == filter.Category))) &&
            (filter.FromDate == null || r.BusinessDate >= filter.FromDate) && (filter.ToDate == null || r.BusinessDate <= filter.ToDate) &&
            (filter.PracticeId == null || db.OperatingSettlements.Any(s => s.Id == r.OriginalSettlementId &&
                db.OperatingCostAllocationSnapshots.Any(a => a.RevisionId == s.ObligationRevisionId && a.PracticeId == filter.PracticeId))))
            .Select(r => new { r.Id, r.ObligationId, MovementType = "Reversal", MinorUnits = -(filter.PracticeId == null ? r.AmountMinorUnits :
                db.OperatingCostAllocationSnapshots.Where(a => a.PracticeId == filter.PracticeId && db.OperatingSettlements.Any(s =>
                    s.Id == r.OriginalSettlementId && s.ObligationRevisionId == a.RevisionId)).Sum(a => a.AmountMinorUnits)),
                r.BusinessDate, r.RecordedAtUtc, OriginalSettlementId = (Guid?)r.OriginalSettlementId });
        var query = payments.Concat(reversals);
        var count = await query.LongCountAsync(ct);
        var ordered = filter.SortBy == "Amount" ? (filter.SortDirection == "asc" ? query.OrderBy(r => r.MinorUnits) : query.OrderByDescending(r => r.MinorUnits))
            : filter.SortBy == "Status" ? (filter.SortDirection == "asc" ? query.OrderBy(r => r.MovementType) : query.OrderByDescending(r => r.MovementType))
            : filter.SortDirection == "asc" ? query.OrderBy(r => r.BusinessDate) : query.OrderByDescending(r => r.BusinessDate);
        var rows = await ordered.ThenBy(r => r.Id).Skip(Offset(filter)).Take(filter.PageSize).ToArrayAsync(ct);
        return Result<ReportPage<OperatingMovementResponse>>.Ok(new(rows.Select(r => new OperatingMovementResponse(r.Id, r.ObligationId,
            r.MovementType, r.MinorUnits / 100m, r.BusinessDate, r.RecordedAtUtc, r.OriginalSettlementId)).ToArray(), count, filter.PageNumber, filter.PageSize));
    }
    public async Task<Result<OperatingFinanceOptions>> OptionsAsync(CancellationToken ct, ReportOptionsFilter? filter = null)
    {
        var scope = await AccessAsync(Phase18PermissionNames.ExpensesView, null, ct);
        if (scope.IsFailure) scope = await AccessAsync(Phase18PermissionNames.SalaryPlansView, null, ct);
        if (scope.IsFailure) return Result<OperatingFinanceOptions>.Fail(scope.Errors);
        var f = filter ?? new();
        if (f.PageNumber is < 1 or > 1_000_000 || f.PageSize is < 1 or > 100 || f.Search?.Length > 100) return Result<OperatingFinanceOptions>.Fail(ReportingErrors.InvalidFilter);
        var pq = db.DoctorPractices.AsNoTracking().Where(p => p.DoctorId == scope.Value.DoctorId && (f.PracticeId == null || p.Id == f.PracticeId) &&
            (f.Search == null || p.NameAr.Contains(f.Search) || p.NameEn != null && p.NameEn.Contains(f.Search)));
        var rq = db.Receptions.AsNoTracking().Where(r => r.OwnerDoctorId == scope.Value.DoctorId && (f.Search == null || r.NameAr.Contains(f.Search) || r.NameEn != null && r.NameEn.Contains(f.Search)) &&
            db.ApplicationUsers.Any(u => u.Id == r.ApplicationUserId && u.IsActive) && db.ReceptionPracticeAssignments.Any(a => a.ReceptionId == r.Id && a.IsActive && (f.PracticeId == null || a.DoctorPracticeId == f.PracticeId)));
        var page = new ReportOptionsPagination(f.PageNumber, f.PageSize, await pq.LongCountAsync(ct), 0, 0, await rq.LongCountAsync(ct));
        var practices = await pq.OrderBy(p => p.NameAr).ThenBy(p => p.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(p => new ReportOption(p.Id.ToString(), p.NameAr, p.NameEn, p.Id)).ToArrayAsync(ct);
        var receptions = await rq.OrderBy(r => r.NameAr).ThenBy(r => r.Id).Skip((f.PageNumber - 1) * f.PageSize).Take(f.PageSize).Select(r => new ReportOption(r.Id.ToString(), r.NameAr, r.NameEn, r.Id)).ToArrayAsync(ct);
        return Result<OperatingFinanceOptions>.Ok(new(practices, receptions, Categories(),
            [new("Cash", "نقدي", "Cash"), new("Card", "بطاقة", "Card"), new("Wallet", "محفظة", "Wallet"), new("BankTransfer", "تحويل بنكي مسجل", "Recorded bank transfer")],
            [new("Monthly", "شهري", "Monthly"), new("Quarterly", "ربع سنوي", "Quarterly"), new("Yearly", "سنوي", "Yearly")],
            [new("Fixed", "ثابت", "Fixed"), new("Variable", "متغير", "Variable")], "EGP", true, options.Value.DefaultReportTimeZoneId, page));
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
