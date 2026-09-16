using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Application.Features.Reservations;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ReservationExpirationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpirationBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, string, Exception?> BatchFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(4700, nameof(BatchFailed)),
            "Reservation expiration batch failed with {ExceptionType}.");

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
        var projectionOutbox = scope.ServiceProvider
            .GetRequiredService<IReservationProjectionInvalidationOutbox>();
        var nowUtc = EnsureUtc(clock.UtcNow);
        var candidates = await dbContext.Reservations
            .Include(item => item.History)
            .Where(item => item.Status == ReservationStatus.Active && item.ScheduledStartUtc < nowUtc)
            .OrderBy(item => item.ScheduledStartUtc)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        if (candidates.Length == 0)
        {
            return;
        }

        var practiceIds = candidates.Select(item => item.DoctorPracticeId).Distinct().ToArray();
        var configurations = await dbContext.DoctorPracticeConfigurations.AsNoTracking()
            .Where(item => practiceIds.Contains(item.DoctorPracticeId))
            .ToDictionaryAsync(item => item.DoctorPracticeId, cancellationToken);
        var periods = (await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
                .Where(item => practiceIds.Contains(item.DoctorPracticeId))
                .ToArrayAsync(cancellationToken))
            .ToLookup(item => item.DoctorPracticeId);
        var dates = candidates.Select(item => item.BusinessDate).Distinct().ToArray();
        var exceptions = (await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
                .Where(item => practiceIds.Contains(item.DoctorPracticeId) && dates.Contains(item.Date))
                .ToArrayAsync(cancellationToken))
            .ToLookup(item => item.DoctorPracticeId);

        var changed = false;
        foreach (var reservation in candidates)
        {
            if (!configurations.TryGetValue(reservation.DoctorPracticeId, out var configuration))
            {
                continue;
            }

            var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
                reservation.BusinessDate,
                periods[reservation.DoctorPracticeId],
                exceptions[reservation.DoctorPracticeId]);
            if (effective.Count == 0)
            {
                continue;
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(reservation.TimeZoneIdSnapshot);
            var localEnd = DateTime.SpecifyKind(
                reservation.BusinessDate.ToDateTime(effective.Max(item => item.EndTime)),
                DateTimeKind.Unspecified);
            var operationalEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
            if (nowUtc >= operationalEndUtc && reservation.Expire(nowUtc).IsSuccess)
            {
                changed = true;
                await projectionOutbox.QueueAsync(new QueueReservationProjectionInvalidation(
                    $"reservation-expired:{reservation.Id:N}:{reservation.History.Last().Id:N}",
                    reservation.DoctorPracticeId,
                    reservation.DoctorId,
                    RefreshAvailability: true,
                    RefreshPopularity: true), cancellationToken);
            }
        }

        if (!changed)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker or lifecycle mutation won. The next idempotent batch re-reads state.
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
