using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class TicketOperationalDayBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TicketOperationalDayBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, string, Exception?> BatchFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4800, nameof(BatchFailed)),
            "Ticket operational-day batch failed with {ExceptionType}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        do
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                BatchFailed(logger, exception.GetType().Name, null);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var queueLock = scope.ServiceProvider.GetRequiredService<ITicketQueueLock>();
        var nowUtc = EnsureUtc(clock.UtcNow);
        var candidates = await dbContext.Tickets.AsNoTracking()
            .Where(item => item.Status == TicketStatus.Waiting || item.Status == TicketStatus.Called)
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.DoctorPracticeId)
            .Select(item => new { item.DoctorPracticeId, item.BusinessDate })
            .Distinct()
            .Take(25)
            .ToArrayAsync(cancellationToken);
        if (candidates.Length == 0)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var changed = false;
        foreach (var candidate in candidates)
        {
            var configuration = await dbContext.DoctorPracticeConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.DoctorPracticeId == candidate.DoctorPracticeId,
                    cancellationToken);
            if (configuration is null)
            {
                continue;
            }

            var periods = await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
                .Where(item => item.DoctorPracticeId == candidate.DoctorPracticeId)
                .ToArrayAsync(cancellationToken);
            var exceptions = await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
                .Where(item => item.DoctorPracticeId == candidate.DoctorPracticeId &&
                               item.Date == candidate.BusinessDate)
                .ToArrayAsync(cancellationToken);
            var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
                candidate.BusinessDate, periods, exceptions);
            if (effective.Count == 0)
            {
                continue;
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
            var localEnd = DateTime.SpecifyKind(
                candidate.BusinessDate.ToDateTime(effective.Max(item => item.EndTime)),
                DateTimeKind.Unspecified);
            if (nowUtc < TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone))
            {
                continue;
            }

            await queueLock.AcquirePracticeDayAsync(
                candidate.DoctorPracticeId, candidate.BusinessDate, cancellationToken);
            var tickets = await dbContext.Tickets
                .Include(item => item.History)
                .Where(item => item.DoctorPracticeId == candidate.DoctorPracticeId &&
                               item.BusinessDate == candidate.BusinessDate &&
                               (item.Status == TicketStatus.Waiting ||
                                item.Status == TicketStatus.Called))
                .OrderBy(item => item.TicketNumber)
                .Take(100)
                .ToArrayAsync(cancellationToken);
            foreach (var ticket in tickets)
            {
                changed |= ticket.CancelForOperationalDay(nowUtc).IsSuccess;
            }
        }

        if (changed)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
