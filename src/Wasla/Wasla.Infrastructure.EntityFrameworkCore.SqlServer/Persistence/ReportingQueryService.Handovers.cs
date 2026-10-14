using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Operations;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    private async Task<IReadOnlyList<ReportMetric>> HandoverMetricsAsync(ReportScope scope, ReportFilter filter, ReportingWindow w, CancellationToken ct)
    {
        var permission = scope.Actor == ReportActor.Doctor ? Phase18PermissionNames.DoctorHandoversView : Phase18PermissionNames.HandoverView;
        var access = await guard.AuthorizeAsync(scope.Actor, permission, filter.PracticeId, null, ct);
        string[] codes = ["OpenHandovers", "HandoversCreated", "HandoverAcknowledgements", "HandoversClosed"];
        if (access.IsFailure || scope.PracticeIds.Any(p => !access.Value.PracticeIds.Contains(p)))
            return codes.Select(code => calculator.Missing(code, ReportDataStatus.NotAvailable, "Report.HandoverDelegationRequiredForWholeScope")).ToArray();
        var history = await (from h in db.Set<ReceptionHandoverHistory>().AsNoTracking()
            join handover in db.Set<ReceptionHandover>().AsNoTracking() on h.HandoverId equals handover.Id
            where scope.PracticeIds.Contains(handover.DoctorPracticeId) && h.RecordedAtUtc <= w.AsOfUtc
            select new { h.HandoverId, h.Operation, h.Status, h.RecordedAtUtc, h.SequenceNumber }).ToArrayAsync(ct);
        var boundaries = ReportingTimeScope.Bounds(w.FromDate, w.ToDate, w.ReportTimeZoneId);
        var events = history.Where(h => h.RecordedAtUtc >= boundaries.FromUtc && h.RecordedAtUtc < boundaries.BeforeUtc).ToArray();
        return [calculator.Count("OpenHandovers", history.GroupBy(h => h.HandoverId).Count(g => g.MaxBy(h => h.SequenceNumber)!.Status != HandoverStatus.Closed)),
            calculator.Count("HandoversCreated", events.Count(h => h.Operation == "Created")),
            calculator.Count("HandoverAcknowledgements", events.Count(h => h.Operation == "Acknowledged")),
            calculator.Count("HandoversClosed", events.Count(h => h.Operation is "Closed" or "DoctorDirectClosure"))];
    }
}
