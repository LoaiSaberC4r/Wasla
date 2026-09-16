using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReservationProjectionInvalidationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationProjectionInvalidationBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReservationProjectionInvalidationProcessor>()
                    .ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ProcessingFailed(logger, exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 4711, Level = LogLevel.Error,
        Message = "Reservation projection invalidation loop failed with {ExceptionType}.")]
    private static partial void ProcessingFailed(ILogger logger, string exceptionType);
}
