using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private sealed record FinancialOpenFact(Guid Id, Guid RevisionId, OperatingObligationKind Kind, DateOnly BusinessDate,
        DateOnly DueDate, DateTime RecordedAtUtc, bool Unsettled, bool Confirmed, OperatingObligationStatus Status,
        AllocatedOperatingCost[] Allocations);
    private sealed record FinancialMovementFact(Guid Id, Guid ObligationId, string Type, DateOnly BusinessDate,
        DateTime RecordedAtUtc, decimal Amount, AllocatedOperatingCost[] Allocations, string DetailRoute);
    private sealed record FinancialSources(IReadOnlyList<FinancialOpenFact> Obligations,
        IReadOnlyList<FinancialMovementFact> Movements, IReadOnlyList<CollectionFact> Collections, decimal Gross, decimal Refunds);
    private sealed record CollectionFact(Guid Id, Guid PracticeId, Guid PatientId, string Type, decimal Amount,
        DateOnly BusinessDate, DateTime RecordedAtUtc, string Reference, string DetailRoute);

    private async Task<FinancialSources> FinancialSourcesAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct, ReportFilter? filter = null)
    {
        var allowed = db.OperatingObligations.AsNoTracking().Where(o => o.DoctorId == scope.DoctorId && o.RecordedAtUtc <= w.AsOfUtc &&
            (filter == null || filter.ReceptionId == null || o.ReceptionId == filter.ReceptionId) &&
            (filter == null || filter.PlanId == null || o.PlanId == filter.PlanId) &&
            (code != "salary" || o.Kind == OperatingObligationKind.Salary) &&
            (code != "expenses" || o.Kind != OperatingObligationKind.Salary) &&
            (code != "recurring-expenses" || o.Kind == OperatingObligationKind.RecurringExpense));
        var obligationAllowed = allowed;
        if (filter?.ExpenseCategory is { } category)
            obligationAllowed = obligationAllowed.Where(o => db.OperatingObligationRevisions.Any(r => r.ObligationId == o.Id && r.RecordedAtUtc <= w.AsOfUtc && r.Category == category &&
                !db.OperatingObligationRevisions.Any(later => later.ObligationId == o.Id && later.RecordedAtUtc <= w.AsOfUtc && later.RevisionNumber > r.RevisionNumber)));
        var obligationIds = allowed.Select(o => o.Id);
        // Closed liabilities outside the due window cannot contribute to an
        // outstanding total or detail preview. Cash movements are queried below
        // independently against their immutable settlement revision.
        obligationAllowed = obligationAllowed.Where(o => !db.OperatingSettlements.Any(s => s.ObligationId == o.Id && s.RecordedAtUtc <= w.AsOfUtc &&
                !db.OperatingSettlementReversals.Any(r => r.OriginalSettlementId == s.Id && r.RecordedAtUtc <= w.AsOfUtc)) ||
            db.OperatingObligationRevisions.Any(r => r.ObligationId == o.Id && r.RecordedAtUtc <= w.AsOfUtc &&
                r.DueBusinessDate >= w.FromDate && r.DueBusinessDate <= w.ToDate &&
                !db.OperatingObligationRevisions.Any(later => later.ObligationId == o.Id && later.RecordedAtUtc <= w.AsOfUtc && later.RevisionNumber > r.RevisionNumber)));
        var roots = await obligationAllowed.Select(o => new
        {
            o.Id, o.Kind, o.BusinessDate, o.RecordedAtUtc,
            Unsettled = !db.OperatingSettlements.Any(s => s.ObligationId == o.Id && s.RecordedAtUtc <= w.AsOfUtc &&
                !db.OperatingSettlementReversals.Any(r => r.OriginalSettlementId == s.Id && r.RecordedAtUtc <= w.AsOfUtc))
        }).ToArrayAsync(ct);
        var rootIds = roots.Select(o => o.Id).ToArray();
        var revisions = await db.OperatingObligationRevisions.AsNoTracking().Where(r => rootIds.Contains(r.ObligationId) && r.RecordedAtUtc <= w.AsOfUtc &&
            !db.OperatingObligationRevisions.Any(later => later.ObligationId == r.ObligationId && later.RecordedAtUtc <= w.AsOfUtc && later.RevisionNumber > r.RevisionNumber))
            .Include(r => r.Allocations).AsSplitQuery().ToArrayAsync(ct);
        var revisionByObligation = revisions.ToDictionary(r => r.ObligationId);
        var facts = roots.Where(o => revisionByObligation.ContainsKey(o.Id)).Select(o =>
        {
            var r = revisionByObligation[o.Id];
            return new FinancialOpenFact(o.Id, r.Id, o.Kind, o.BusinessDate, r.DueBusinessDate, o.RecordedAtUtc, o.Unsettled,
                r.ActualAmountConfirmed, r.Status, r.Allocations.Where(a => scope.PracticeIds.Contains(a.PracticeId))
                    .Select(a => new AllocatedOperatingCost(a.PracticeId, a.Percentage, a.AmountMinorUnits)).ToArray());
        }).Where(o => o.Allocations.Length > 0).ToArray();
        var settlements = await db.OperatingSettlements.AsNoTracking().Where(s => obligationIds.Contains(s.ObligationId) &&
            s.RecordedAtUtc <= w.AsOfUtc && s.BusinessDate >= w.FromDate && s.BusinessDate <= w.ToDate &&
            (filter == null || filter.ExpenseCategory == null || db.OperatingObligationRevisions.Any(r => r.Id == s.ObligationRevisionId && r.Category == filter.ExpenseCategory))).ToArrayAsync(ct);
        var reversals = await db.OperatingSettlementReversals.AsNoTracking().Where(r => obligationIds.Contains(r.ObligationId) &&
            r.RecordedAtUtc <= w.AsOfUtc && r.BusinessDate >= w.FromDate && r.BusinessDate <= w.ToDate &&
            (filter == null || filter.ExpenseCategory == null || db.OperatingSettlements.Any(s => s.Id == r.OriginalSettlementId &&
                db.OperatingObligationRevisions.Any(revision => revision.Id == s.ObligationRevisionId && revision.Category == filter.ExpenseCategory)))).ToArrayAsync(ct);
        var reversedIds = reversals.Select(r => r.OriginalSettlementId).ToArray();
        var reversedSettlements = await db.OperatingSettlements.AsNoTracking().Where(s => reversedIds.Contains(s.Id)).ToArrayAsync(ct);
        var revisionIds = settlements.Concat(reversedSettlements).Select(s => s.ObligationRevisionId).Distinct().ToArray();
        var allocationRows = await db.OperatingCostAllocationSnapshots.AsNoTracking().Where(a => revisionIds.Contains(a.RevisionId) && scope.PracticeIds.Contains(a.PracticeId)).ToArrayAsync(ct);
        var allocationByRevision = allocationRows.GroupBy(a => a.RevisionId).ToDictionary(g => g.Key,
            g => g.Select(a => new AllocatedOperatingCost(a.PracticeId, a.Percentage, a.AmountMinorUnits)).ToArray());
        var movementObligationIds = settlements.Select(s => s.ObligationId).Concat(reversals.Select(r => r.ObligationId)).Distinct().ToArray();
        var kinds = await db.OperatingObligations.AsNoTracking().Where(o => movementObligationIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Kind, ct);
        string Route(Guid obligation, Guid settlement) => $"/api/v1/doctors/me/{(kinds[obligation] == OperatingObligationKind.Salary ? "salary-settlements" : "expense-settlements")}/{settlement}";
        var movements = settlements.Where(s => allocationByRevision.ContainsKey(s.ObligationRevisionId)).Select(s =>
            new FinancialMovementFact(s.Id, s.ObligationId, "Settlement", s.BusinessDate, s.RecordedAtUtc,
                allocationByRevision[s.ObligationRevisionId].Sum(a => a.Amount), allocationByRevision[s.ObligationRevisionId], Route(s.ObligationId, s.Id))).ToList();
        var originals = reversedSettlements.ToDictionary(s => s.Id);
        movements.AddRange(reversals.Where(r => allocationByRevision.ContainsKey(originals[r.OriginalSettlementId].ObligationRevisionId)).Select(r =>
        {
            var original = originals[r.OriginalSettlementId]; var allocations = allocationByRevision[original.ObligationRevisionId];
            return new FinancialMovementFact(r.Id, r.ObligationId, "Reversal", r.BusinessDate, r.RecordedAtUtc, -allocations.Sum(a => a.Amount), allocations,
                Route(r.ObligationId, r.OriginalSettlementId));
        }));
        decimal gross = 0, refunds = 0;
        List<CollectionFact> collections = [];
        if (code == "operating-finance")
        {
            var bounds = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
            var payments = await db.Payments.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.DoctorPracticeId) && p.CollectedOnUtc <= w.AsOfUtc &&
                p.CollectedOnUtc >= bounds.FromUtc && p.CollectedOnUtc < bounds.BeforeUtc)
                .Select(p => new { p.Id, p.DoctorPracticeId, p.PatientId, p.Amount, p.CollectedOnUtc, p.TransactionNumber }).ToArrayAsync(ct);
            var refundRows = await db.Refunds.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.RefundedOnUtc <= w.AsOfUtc &&
                r.RefundedOnUtc >= bounds.FromUtc && r.RefundedOnUtc < bounds.BeforeUtc)
                .Select(r => new { r.Id, r.DoctorPracticeId, r.PatientId, r.Amount, r.RefundedOnUtc, r.TransactionNumber }).ToArrayAsync(ct);
            gross = payments.Sum(p => p.Amount); refunds = refundRows.Sum(r => r.Amount);
            collections.AddRange(payments.Select(p => new CollectionFact(p.Id, p.DoctorPracticeId, p.PatientId, "Payment", p.Amount,
                OperationalKpiCalculator.LocalDate(p.CollectedOnUtc, w.ReportTimeZoneId), p.CollectedOnUtc, p.TransactionNumber,
                $"/api/v1/practices/{p.DoctorPracticeId}/payments/{p.Id}")));
            collections.AddRange(refundRows.Select(r => new CollectionFact(r.Id, r.DoctorPracticeId, r.PatientId, "Refund", -r.Amount,
                OperationalKpiCalculator.LocalDate(r.RefundedOnUtc, w.ReportTimeZoneId), r.RefundedOnUtc, r.TransactionNumber,
                $"/api/v1/practices/{r.DoctorPracticeId}/refunds/{r.Id}/receipt")));
        }
        return new(facts, movements, collections, gross, refunds);
    }
    private async Task<IReadOnlyList<ReportMetric>> FinancialMetricsAsync(ReportScope scope, ReportFilter f, ReportingWindow w, string code, CancellationToken ct)
        => FinancialMetrics(await FinancialSourcesAsync(scope, code, w, ct, f), w, code);
    private List<ReportMetric> FinancialMetrics(FinancialSources source, ReportingWindow w, string code)
    {
        var outstanding = source.Obligations.Where(o => o.Unsettled && o.Status != OperatingObligationStatus.Cancelled).ToArray();
        var all = outstanding.Sum(o => o.Allocations.Sum(a => a.Amount));
        var due = outstanding.Where(o => OperationalKpiCalculator.InWindow(o.DueDate, w)).Sum(o => o.Allocations.Sum(a => a.Amount));
        var today = OperationalKpiCalculator.LocalDate(w.AsOfUtc, w.ReportTimeZoneId);
        var overdue = outstanding.Where(o => o.DueDate < today).Sum(o => o.Allocations.Sum(a => a.Amount));
        var paid = source.Movements.Sum(m => m.Amount);
        var status = outstanding.Any(o => !o.Confirmed) ? ReportDataStatus.Partial : ReportDataStatus.Complete;
        var warning = status == ReportDataStatus.Partial ? "Report.UnconfirmedVariableAmountsArePlannedObligations" : null;
        var result = new List<ReportMetric>
        {
            calculator.Count("PaidOperatingExpenses", paid, source.Movements.Count),
            calculator.Count("OutstandingDueInPeriod", due, outstanding.Length, status, warning: warning),
            calculator.Count("AllOutstandingAsOf", all, outstanding.Length, status, warning: warning),
            calculator.Count("OverdueAsOf", overdue, outstanding.Length, status, warning: warning)
        };
        if (code == "operating-finance") result.AddRange([calculator.Count("GrossCollected", source.Gross), calculator.Count("Refunds", source.Refunds),
            calculator.Count("NetCollected", source.Gross - source.Refunds), calculator.Count("NetOperatingCashResult", source.Gross - source.Refunds - paid),
            calculator.Count("ProjectedResultForPeriod", source.Gross - source.Refunds - paid - due, outstanding.Length, status, warning: warning)]);
        return result;
    }
    private static ReportRecord[] FinancialRecords(FinancialSources source, ReportScope scope, ReportingWindow w)
    {
        return source.Obligations.Where(o => OperationalKpiCalculator.InWindow(o.DueDate, w)).Select(o =>
            new ReportRecord(o.Id, "OperatingObligation", o.Allocations.Length == 1 ? o.Allocations[0].PracticeId : null,
                o.DueDate, o.RecordedAtUtc, o.Id.ToString(), o.Status == OperatingObligationStatus.Cancelled ? "Cancelled" : o.Unsettled ? "Unpaid" : "Paid",
                Amount: o.Allocations.Sum(a => a.Amount), DetailRoute: $"/api/v1/doctors/me/{(o.Kind == OperatingObligationKind.Salary ? "salary-obligations" : "expenses/obligations")}/{o.Id}"))
            .Concat(source.Movements.Select(m => new ReportRecord(m.Id, "Operating" + m.Type,
                m.Allocations.Length == 1 ? m.Allocations[0].PracticeId : null, m.BusinessDate, m.RecordedAtUtc, m.Id.ToString(), m.Type,
                Amount: m.Amount, DetailRoute: m.DetailRoute)))
            .Concat(source.Collections.Select(c => new ReportRecord(c.Id, c.Type, c.PracticeId, c.BusinessDate, c.RecordedAtUtc, c.Reference, c.Type,
                scope.Permissions.Contains(Wasla.Domain.Security.PermissionNames.DoctorPracticePaymentsViewOwn) ? c.PatientId : null,
                c.Amount, scope.Permissions.Contains(Wasla.Domain.Security.PermissionNames.DoctorPracticePaymentsViewOwn) ? c.DetailRoute : null))).ToArray();
    }
    private static FinancialSources FinancialPractice(FinancialSources source, Guid practice)
    {
        var obligations = source.Obligations.Where(o => o.Allocations.Any(a => a.PracticeId == practice))
            .Select(o => o with { Allocations = o.Allocations.Where(a => a.PracticeId == practice).ToArray() }).ToArray();
        var movements = source.Movements.Where(m => m.Allocations.Any(a => a.PracticeId == practice)).Select(m =>
        {
            var allocations = m.Allocations.Where(a => a.PracticeId == practice).ToArray();
            return m with { Allocations = allocations, Amount = allocations.Sum(a => a.Amount) * (m.Type == "Reversal" ? -1 : 1) };
        }).ToArray();
        var collections = source.Collections.Where(c => c.PracticeId == practice).ToArray();
        return new(obligations, movements, collections, collections.Where(c => c.Type == "Payment").Sum(c => c.Amount), -collections.Where(c => c.Type == "Refund").Sum(c => c.Amount));
    }
    private ReportSeriesPoint[] FinancialSeries(FinancialSources source, ReportingWindow w, string code, ReportGrouping grouping)
    {
        if (grouping is not (ReportGrouping.Day or ReportGrouping.Week or ReportGrouping.Month)) return [];
        string Key(DateOnly d) => grouping == ReportGrouping.Month ? d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : grouping == ReportGrouping.Week ? d.AddDays(-((int)d.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var dates = Enumerable.Range(w.FromDate.DayNumber, w.ToDate.DayNumber - w.FromDate.DayNumber + 1).Select(DateOnly.FromDayNumber).ToArray();
        return dates.GroupBy(Key).Select(g =>
        {
            var part = w with { FromDate = g.First(), ToDate = g.Last() };
            var movements = source.Movements.Where(m => OperationalKpiCalculator.InWindow(m.BusinessDate, part)).ToArray();
            var collections = source.Collections.Where(c => OperationalKpiCalculator.InWindow(c.BusinessDate, part)).ToArray();
            var snapshot = new FinancialSources(source.Obligations, movements, collections,
                collections.Where(c => c.Type == "Payment").Sum(c => c.Amount), -collections.Where(c => c.Type == "Refund").Sum(c => c.Amount));
            return new ReportSeriesPoint(g.Key, g.Key, g.Key, FinancialMetrics(snapshot, part, code)
                .Where(m => m.MetricCode is "NetCollected" or "PaidOperatingExpenses" or "OutstandingDueInPeriod" or "NetOperatingCashResult" or "ProjectedResultForPeriod").ToArray());
        }).ToArray();
    }
}
