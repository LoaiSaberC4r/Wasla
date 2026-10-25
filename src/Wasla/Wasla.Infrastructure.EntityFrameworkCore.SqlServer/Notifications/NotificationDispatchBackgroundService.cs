using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal sealed partial class NotificationDispatchBackgroundService(IServiceScopeFactory scopes, IOptions<NotificationOptions> options,
    ILogger<NotificationDispatchBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.DispatchEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationDispatchProcessor>().ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { CycleFailed(logger); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    [LoggerMessage(EventId = 4701, Level = LogLevel.Error, Message = "Notification dispatch cycle failed; durable work remains recoverable.")]
    private static partial void CycleFailed(ILogger logger);
}
