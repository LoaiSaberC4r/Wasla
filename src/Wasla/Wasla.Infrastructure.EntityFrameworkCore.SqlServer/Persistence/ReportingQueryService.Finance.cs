using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private sealed record FinancialOpenFact(Guid Id, Guid RevisionId, OperatingObligationKind Kind, DateOnly BusinessDate,
        DateOnly DueDate, DateTime RecordedAtUtc, bool Unsettled, bool Confirmed, OperatingObligationStatus Status,
        IReadOnlyList<AllocatedOperatingCost> Allocations);
    private sealed record FinancialMovementFact(Guid Id, Guid ObligationId, string Type, DateOnly BusinessDate,
        DateTime RecordedAtUtc, decimal Amount, IReadOnlyList<AllocatedOperatingCost> Allocations, string DetailRoute);
    private sealed record FinancialSources(IReadOnlyList<FinancialOpenFact> Obligations,
        IReadOnlyList<FinancialMovementFact> Movements, decimal Gross, decimal Refunds);

    private async Task<FinancialSources> FinancialSourcesAsync(ReportScope scope, string code, ReportingWindow w, CancellationToken ct)
    {
        var allowed = db.OperatingObligations.AsNoTracking().Where(o => o.DoctorId == scope.DoctorId && o.RecordedAtUtc <= w.AsOfUtc &&
            (code != "salary" || o.Kind == OperatingObligationKind.Salary) &&
            (code != "expenses" || o.Kind != OperatingObligationKind.Salary) &&
            (code != "recurring-expenses" || o.Kind == OperatingObligationKind.RecurringExpense));
        var obligationIds = allowed.Select(o => o.Id);
        var roots = await allowed.Select(o => new
        {
            o.Id, o.Kind, o.BusinessDate, o.RecordedAtUtc,
            Unsettled = !db.OperatingSettlements.Any(s => s.ObligationId == o.Id && s.RecordedAtUtc <= w.AsOfUtc &&
                !db.OperatingSettlementReversals.Any(r => r.OriginalSettlementId == s.Id && r.RecordedAtUtc <= w.AsOfUtc))
        }).ToArrayAsync(ct);
        var revisions = await db.OperatingObligationRevisions.AsNoTracking().Where(r => obligationIds.Contains(r.ObligationId) && r.RecordedAtUtc <= w.AsOfUtc &&
            !db.OperatingObligationRevisions.Any(later => later.ObligationId == r.ObligationId && later.RecordedAtUtc <= w.AsOfUtc && later.RevisionNumber > r.RevisionNumber))
            .Include(r => r.Allocations).AsSplitQuery().ToArrayAsync(ct);
        var revisionByObligation = revisions.ToDictionary(r => r.ObligationId);
        var facts = roots.Where(o => revisionByObligation.ContainsKey(o.Id)).Select(o =>
        {
            var r = revisionByObligation[o.Id];
            return new FinancialOpenFact(o.Id, r.Id, o.Kind, o.BusinessDate, r.DueBusinessDate, o.RecordedAtUtc, o.Unsettled,
                r.ActualAmountConfirmed, r.Status, r.Allocations.Where(a => scope.PracticeIds.Contains(a.PracticeId))
                    .Select(a => new AllocatedOperatingCost(a.PracticeId, a.Percentage, a.AmountMinorUnits)).ToArray());
        }).Where(o => o.Allocations.Count > 0).ToArray();
        var settlements = await db.OperatingSettlements.AsNoTracking().Where(s => obligationIds.Contains(s.ObligationId) &&
            s.RecordedAtUtc <= w.AsOfUtc && s.BusinessDate >= w.FromDate && s.BusinessDate <= w.ToDate).ToArrayAsync(ct);
        var reversals = await db.OperatingSettlementReversals.AsNoTracking().Where(r => obligationIds.Contains(r.ObligationId) &&
            r.RecordedAtUtc <= w.AsOfUtc && r.BusinessDate >= w.FromDate && r.BusinessDate <= w.ToDate).ToArrayAsync(ct);
        var reversedIds = reversals.Select(r => r.OriginalSettlementId).ToArray();
        var reversedSettlements = await db.OperatingSettlements.AsNoTracking().Where(s => reversedIds.Contains(s.Id)).ToArrayAsync(ct);
        var revisionIds = settlements.Concat(reversedSettlements).Select(s => s.ObligationRevisionId).Distinct().ToArray();
        var allocationRows = await db.OperatingCostAllocationSnapshots.AsNoTracking().Where(a => revisionIds.Contains(a.RevisionId) && scope.PracticeIds.Contains(a.PracticeId)).ToArrayAsync(ct);
        var allocationByRevision = allocationRows.GroupBy(a => a.RevisionId).ToDictionary(g => g.Key,
            g => g.Select(a => new AllocatedOperatingCost(a.PracticeId, a.Percentage, a.AmountMinorUnits)).ToArray());
        var kinds = roots.ToDictionary(o => o.Id, o => o.Kind);
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
        if (code == "operating-finance")
        {
            var bounds = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
            gross = (await db.Payments.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.DoctorPracticeId) && p.CollectedOnUtc <= w.AsOfUtc &&
                p.CollectedOnUtc >= bounds.FromUtc && p.CollectedOnUtc < bounds.BeforeUtc).Select(p => p.Amount).ToArrayAsync(ct)).Sum();
            refunds = (await db.Refunds.AsNoTracking().Where(r => scope.PracticeIds.Contains(r.DoctorPracticeId) && r.RefundedOnUtc <= w.AsOfUtc &&
                r.RefundedOnUtc >= bounds.FromUtc && r.RefundedOnUtc < bounds.BeforeUtc).Select(r => r.Amount).ToArrayAsync(ct)).Sum();
        }
        return new(facts, movements, gross, refunds);
    }
    private async Task<IReadOnlyList<ReportMetric>> FinancialMetricsAsync(ReportScope scope, ReportFilter f, ReportingWindow w, string code, CancellationToken ct)
        => FinancialMetrics(await FinancialSourcesAsync(scope, code, w, ct), w, code);
    private IReadOnlyList<ReportMetric> FinancialMetrics(FinancialSources source, ReportingWindow w, string code)
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
    private async Task<IReadOnlyList<ReportRecord>> FinancialRecordsAsync(ReportScope scope, string code, ReportFilter f, ReportingWindow w, CancellationToken ct)
    {
        var source = await FinancialSourcesAsync(scope, code, w, ct);
        return source.Obligations.Where(o => OperationalKpiCalculator.InWindow(o.DueDate, w)).Select(o =>
            new ReportRecord(o.Id, "OperatingObligation", o.Allocations.Count == 1 ? o.Allocations[0].PracticeId : null,
                o.DueDate, o.RecordedAtUtc, o.Id.ToString(), o.Status == OperatingObligationStatus.Cancelled ? "Cancelled" : o.Unsettled ? "Unpaid" : "Paid",
                Amount: o.Allocations.Sum(a => a.Amount), DetailRoute: $"/api/v1/doctors/me/{(o.Kind == OperatingObligationKind.Salary ? "salary-obligations" : "expenses/obligations")}/{o.Id}"))
            .Concat(source.Movements.Select(m => new ReportRecord(m.Id, "Operating" + m.Type,
                m.Allocations.Count == 1 ? m.Allocations[0].PracticeId : null, m.BusinessDate, m.RecordedAtUtc, m.Id.ToString(), m.Type,
                Amount: m.Amount, DetailRoute: m.DetailRoute))).ToArray();
    }
}
