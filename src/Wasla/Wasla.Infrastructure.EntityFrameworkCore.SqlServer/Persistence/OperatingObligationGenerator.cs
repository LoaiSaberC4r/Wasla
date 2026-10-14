using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.OperatingFinance;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class OperatingObligationGenerator(WaslaDbContext db, ITicketQueueLock coordination,
    IDateTimeProvider clock, IOptions<Phase18Options> options) : IOperatingObligationGenerator
{
    public async Task<int> GenerateAsync(Guid? doctorId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now,
            TimeZoneInfo.FindSystemTimeZoneById(options.Value.DefaultReportTimeZoneId)));
        var total = 0;
        Guid? after = null;
        while (true)
        {
            var ids = await db.OperatingFinancialPlans.AsNoTracking().Where(p => (doctorId == null || p.DoctorId == doctorId) &&
                p.EffectiveFrom <= today && (after == null || p.Id.CompareTo(after.Value) > 0))
                .OrderBy(p => p.Id).Select(p => p.Id).Take(100).ToArrayAsync(ct);
            if (ids.Length == 0) break;
            foreach (var id in ids)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var doctor = await db.OperatingFinancialPlans.AsNoTracking().Where(p => p.Id == id).Select(p => p.DoctorId).SingleAsync(ct);
                await coordination.AcquireIdempotencyAsync(Guid.Empty, "OperatingFinanceDoctor", doctor.ToString("N"), ct);
                var plan = await db.OperatingFinancialPlans.AsNoTracking().Include(p => p.Revisions).ThenInclude(r => r.Allocations)
                    .AsSplitQuery().SingleAsync(p => p.Id == id, ct);
                var generated = (await db.OperatingObligations.AsNoTracking().Where(o => o.PlanId == id)
                    .Select(o => o.OccurrenceKey!).ToArrayAsync(ct)).ToHashSet(StringComparer.Ordinal);
                var assigned = plan.Kind == OperatingPlanKind.Salary
                    ? await db.ReceptionPracticeAssignments.AsNoTracking().Where(a => a.ReceptionId == plan.ReceptionId && a.IsActive)
                        .Select(a => a.DoctorPracticeId).ToArrayAsync(ct) : [];
                var receptionActive = plan.Kind != OperatingPlanKind.Salary || await db.Receptions.AnyAsync(r => r.Id == plan.ReceptionId &&
                    r.OwnerDoctorId == doctor && db.ApplicationUsers.Any(u => u.Id == r.ApplicationUserId && u.IsActive), ct);
                var added = 0;
                for (var month = plan.FirstCoveredMonth; month <= new DateOnly(today.Year, today.Month, 1); month = month.AddMonths(1))
                {
                    if (added >= 24) break;
                    var key = month.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
                    if (generated.Contains(key)) continue;
                    var terms = plan.Revisions.Where(r => !r.IsLifecycleChange && r.EffectiveFrom <=
                        (plan.Kind == OperatingPlanKind.Salary ? month : OperatingFinancialPolicy.DueDate(month.Year, month.Month, r.DueDay)))
                        .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.RevisionNumber).FirstOrDefault();
                    if (terms is null) continue;
                    var due = OperatingFinancialPolicy.DueDate(month.Year, month.Month, terms.DueDay);
                    // Create the eligible current calendar occurrence before its due day, so Upcoming
                    // liabilities and the projected result are visible. No future calendar cycle is generated.
                    if (due < terms.EffectiveFrom || terms.EffectiveTo < due) continue;
                    var lifecycle = plan.Revisions.Where(r => r.IsLifecycleChange && r.EffectiveFrom <= due)
                        .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.RevisionNumber).FirstOrDefault();
                    if (lifecycle is not null && lifecycle.Status != OperatingPlanStatus.Active) continue;
                    if (plan.Kind == OperatingPlanKind.Salary)
                    {
                        var endOfMonth = month.AddMonths(1).AddDays(-1);
                        if (!receptionActive || terms.EffectiveTo < endOfMonth ||
                            plan.Revisions.Any(r => r.IsLifecycleChange && r.Status == OperatingPlanStatus.Ended && r.EffectiveFrom < endOfMonth) ||
                            terms.Allocations.Any(a => !assigned.Contains(a.PracticeId))) continue;
                    }
                    else
                    {
                        var anchor = new DateOnly(terms.EffectiveFrom.Year, terms.EffectiveFrom.Month, 1);
                        var monthDifference = (month.Year - anchor.Year) * 12 + month.Month - anchor.Month;
                        if (monthDifference < 0 || monthDifference % (int)terms.Cadence != 0) continue;
                    }
                    var created = OperatingObligation.Create(Guid.NewGuid(), doctor,
                        plan.Kind == OperatingPlanKind.Salary ? OperatingObligationKind.Salary : OperatingObligationKind.RecurringExpense,
                        id, plan.ReceptionId, key, month, due, terms.Amount, terms.AmountPolicy, terms.Category,
                        terms.DescriptionAr, terms.DescriptionEn, terms.Allocations.Select(a => new PracticeAllocationPercentage(a.PracticeId, a.Percentage)).ToArray(),
                        plan.CreatedByApplicationUserId, now);
                    if (created.IsFailure) throw new InvalidOperationException("OperatingFinance.InvalidStoredPlan");
                    db.OperatingObligations.Add(created.Value);
                    db.OperatingFinanceHistories.Add(new(Guid.NewGuid(), doctor, "Obligation", created.Value.Id,
                        "Generated", "ScheduledGeneration", null, now, created.Value.Revisions.Single().Id));
                    ++added;
                }
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); total += added;
                db.ChangeTracker.Clear();
            }
            after = ids[^1];
        }
        return total;
    }
}

internal sealed class OperatingObligationBackgroundService(IServiceScopeFactory factory,
    IOptions<Phase18Options> options, ILogger<OperatingObligationBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Guid, string, Exception?> LogGenerationFailure = LoggerMessage.Define<Guid, string>(
        LogLevel.Error, new EventId(18001, "GenerationFailure"), "Operating obligation generation failed. RunId {RunId}, ErrorType {ErrorType}");
    private static readonly Action<ILogger, Guid, Exception?> LogEvidenceFailure = LoggerMessage.Define<Guid>(
        LogLevel.Error, new EventId(18002, "GenerationEvidenceFailure"), "Generation failure evidence could not be persisted. RunId {RunId}");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ObligationGenerationEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ObligationGenerationIntervalSeconds));
        do
        {
            var runId = Guid.NewGuid();
            var started = DateTime.UtcNow;
            try
            {
                await using var scope = factory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
                db.Set<OperatingGenerationRun>().Add(new(runId, started)); await db.SaveChangesAsync(stoppingToken);
                var count = await scope.ServiceProvider.GetRequiredService<IOperatingObligationGenerator>().GenerateAsync(null, stoppingToken);
                var run = await db.Set<OperatingGenerationRun>().SingleAsync(r => r.Id == runId, stoppingToken);
                run.Finish(DateTime.UtcNow, count); await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Persist only a safe classification; exception text may contain database details.
                LogGenerationFailure(logger, runId, ex.GetType().Name, null);
                try
                {
                    await using var scope = factory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
                    var run = await db.Set<OperatingGenerationRun>().SingleOrDefaultAsync(r => r.Id == runId, stoppingToken);
                    if (run is null) { run = new(runId, started); db.Set<OperatingGenerationRun>().Add(run); }
                    run.Finish(DateTime.UtcNow, 0, "OperatingFinance.GenerationFailed"); await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception recordFailure) when (recordFailure is not OperationCanceledException)
                { LogEvidenceFailure(logger, runId, null); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
