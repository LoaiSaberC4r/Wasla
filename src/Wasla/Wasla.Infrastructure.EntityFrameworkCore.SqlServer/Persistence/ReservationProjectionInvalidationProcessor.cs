using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wasla.Application.Features.PublicDiscovery;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReservationProjectionInvalidationProcessor(
    WaslaDbContext dbContext,
    IPublicDiscoveryRankingProjectionRefresher projectionRefresher,
    ILogger<ReservationProjectionInvalidationProcessor> logger)
{
    private const int BatchSize = 100;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var ids = await dbContext.ReservationProjectionInvalidations.AsNoTracking()
            .Where(item => item.ProcessedOnUtc == null &&
                           (item.ProcessingToken == null || item.NextAttemptOnUtc <= nowUtc) &&
                           (item.NextAttemptOnUtc == null || item.NextAttemptOnUtc <= nowUtc))
            .OrderBy(item => item.CreatedOnUtc)
            .Select(item => item.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);
        if (ids.Length == 0)
        {
            return 0;
        }

        var token = Guid.NewGuid();
        var leaseUntil = nowUtc.AddMinutes(5);
        await dbContext.ReservationProjectionInvalidations
            .Where(item => ids.Contains(item.Id) && item.ProcessedOnUtc == null &&
                           (item.ProcessingToken == null || item.NextAttemptOnUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ProcessingToken, token)
                .SetProperty(item => item.NextAttemptOnUtc, leaseUntil)
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1), cancellationToken);

        var claimed = await dbContext.ReservationProjectionInvalidations.AsNoTracking()
            .Where(item => item.ProcessingToken == token)
            .ToArrayAsync(cancellationToken);
        if (claimed.Length == 0)
        {
            return 0;
        }

        try
        {
            var practiceIds = claimed.Where(item => item.RefreshAvailability)
                .Select(item => item.PracticeId).Distinct().ToArray();
            if (practiceIds.Length != 0)
            {
                // RefreshPractices also refreshes the affected doctors' ranking.
                await projectionRefresher.RefreshPracticesAsync(practiceIds, cancellationToken);
            }

            var practiceDoctors = claimed.Where(item => item.RefreshAvailability)
                .Select(item => item.DoctorId).ToHashSet();
            var doctorIds = claimed.Where(item => item.RefreshPopularity && !practiceDoctors.Contains(item.DoctorId))
                .Select(item => item.DoctorId).Distinct().ToArray();
            if (doctorIds.Length != 0)
            {
                await projectionRefresher.RefreshDoctorsAsync(doctorIds, cancellationToken);
            }

            var processedOnUtc = DateTime.UtcNow;
            await dbContext.ReservationProjectionInvalidations
                .Where(item => item.ProcessingToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.ProcessedOnUtc, processedOnUtc)
                    .SetProperty(item => item.NextAttemptOnUtc, (DateTime?)null)
                    .SetProperty(item => item.LastError, (string?)null)
                    .SetProperty(item => item.ProcessingToken, (Guid?)null), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = exception.GetType().Name;
            await dbContext.ReservationProjectionInvalidations
                .Where(item => item.ProcessingToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.NextAttemptOnUtc, DateTime.UtcNow.AddMinutes(2))
                    .SetProperty(item => item.LastError, error)
                    .SetProperty(item => item.ProcessingToken, (Guid?)null), cancellationToken);
            ProjectionRefreshFailed(logger, error);
        }

        return claimed.Length;
    }

    [LoggerMessage(EventId = 4710, Level = LogLevel.Warning,
        Message = "Reservation projection invalidation processing failed with {ExceptionType}.")]
    private static partial void ProjectionRefreshFailed(ILogger logger, string exceptionType);
}
