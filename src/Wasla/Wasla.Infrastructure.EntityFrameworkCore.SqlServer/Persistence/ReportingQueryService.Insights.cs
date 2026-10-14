using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService
{
    public async Task<Result<IReadOnlyList<PracticeInsightEvidence>>> ReadComparisonsAsync(Guid? practiceId, CancellationToken cancellationToken)
    {
        var scope = await guard.AuthorizeAsync(ReportActor.Doctor, Phase18PermissionNames.InsightsView, practiceId, null, cancellationToken);
        if (scope.IsFailure) return Result<IReadOnlyList<PracticeInsightEvidence>>.Fail(scope.Errors);
        var resolved = time.Resolve(new());
        var now = resolved.Value.AsOfUtc;
        var practices = await db.DoctorPracticeConfigurations.AsNoTracking().Where(c => scope.Value.PracticeIds.Contains(c.DoctorPracticeId))
            .Select(c => new { c.DoctorPracticeId, c.TimeZoneId }).ToArrayAsync(cancellationToken);
        if (practices.Any(p => !TimeZoneInfo.TryFindSystemTimeZoneById(p.TimeZoneId, out _)))
            return Result<IReadOnlyList<PracticeInsightEvidence>>.Fail(ReportingErrors.InvalidFilter);
        if (practices.Length == 0) return Result<IReadOnlyList<PracticeInsightEvidence>>.Ok([]);
        var windows = practices.Select(p => new
        {
            Id = p.DoctorPracticeId,
            End = OperationalKpiCalculator.LocalDate(now, p.TimeZoneId).AddDays(-1)
        }).ToArray();
        var fromDate = windows.Min(p => p.End).AddDays(-13);
        var toDate = windows.Max(p => p.End);
        var allWindow = resolved.Value with { FromDate = fromDate, ToDate = toDate };
        var source = await OperationalSourcesAsync(scope.Value, new(fromDate, toDate, PracticeId: practiceId), allWindow, cancellationToken);
        return Result<IReadOnlyList<PracticeInsightEvidence>>.Ok(windows.Select(p =>
        {
            var current = allWindow with { FromDate = p.End.AddDays(-6), ToDate = p.End };
            var previous = allWindow with { FromDate = p.End.AddDays(-13), ToDate = p.End.AddDays(-7) };
            var reservations = source.Reservations.Where(r => r.PracticeId == p.Id).ToArray();
            var tickets = source.Tickets.Where(t => t.PracticeId == p.Id).ToArray();
            return new PracticeInsightEvidence(p.Id, new(current.FromDate, current.ToDate), new(previous.FromDate, previous.ToDate),
                calculator.Calculate(reservations, tickets, current), calculator.Calculate(reservations, tickets, previous), now);
        }).ToArray());
    }
}
