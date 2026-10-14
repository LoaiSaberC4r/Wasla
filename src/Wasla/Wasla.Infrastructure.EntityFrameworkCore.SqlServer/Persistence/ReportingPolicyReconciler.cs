using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingPolicyReconciler(IServiceScopeFactory scopes, IOptions<Phase18Options> options,
    IDateTimeProvider clock, ILogger<ReportingPolicyReconciler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ReportingPolicyRefreshEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = Guid.NewGuid();
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
                var run = new ReportingRefreshRun(runId, clock.UtcNow); db.Add(run); await db.SaveChangesAsync(stoppingToken);
                Guid? after = null;
                while (true)
                {
                    var batch = await db.DoctorPractices.AsNoTracking().Where(p => after == null || p.Id.CompareTo(after.Value) > 0)
                        .OrderBy(p => p.Id).Select(p => p.Id).Take(100).ToArrayAsync(stoppingToken);
                    if (batch.Length == 0) break;
                    var written = await db.ReconcileReportingPoliciesAsync(batch, stoppingToken);
                    run.Progress(batch[^1], batch.Length, written); await db.SaveChangesAsync(stoppingToken);
                    after = batch[^1];
                }
                run.Finish(clock.UtcNow); await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                RefreshFailed(logger, e.GetType().Name);
                try
                {
                    await using var recovery = scopes.CreateAsyncScope();
                    var db = recovery.ServiceProvider.GetRequiredService<WaslaDbContext>();
                    var run = await db.Set<ReportingRefreshRun>().SingleOrDefaultAsync(r => r.Id == runId, stoppingToken);
                    if (run != null) { run.Finish(clock.UtcNow, "Reporting.PolicyRefreshFailed"); await db.SaveChangesAsync(stoppingToken); }
                }
                catch (Exception recoveryError) when (!stoppingToken.IsCancellationRequested) { RefreshFailed(logger, recoveryError.GetType().Name); }
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.ReportingPolicyRefreshIntervalSeconds), stoppingToken);
        }
    }
    [LoggerMessage(EventId = 4818, Level = LogLevel.Error, Message = "Reporting policy refresh failed ({ExceptionType}).")]
    private static partial void RefreshFailed(ILogger logger, string exceptionType);
}
