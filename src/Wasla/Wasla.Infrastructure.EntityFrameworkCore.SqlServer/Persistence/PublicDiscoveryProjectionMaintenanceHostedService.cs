using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class PublicDiscoveryProjectionMaintenanceHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PublicDiscoveryProjectionOptions> options,
    ILogger<PublicDiscoveryProjectionMaintenanceHostedService> logger)
    : IHostedService
{
    private CancellationTokenSource? stoppingSource;
    private Task? maintenanceLoop;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            MaintenanceDisabled(logger);
            return;
        }

        await RefreshAsync(cancellationToken);
        stoppingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        maintenanceLoop = MaintainAsync(stoppingSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (maintenanceLoop is null || stoppingSource is null)
        {
            return;
        }

        await stoppingSource.CancelAsync();
        try
        {
            await maintenanceLoop.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during host shutdown.
        }
        finally
        {
            stoppingSource.Dispose();
        }
    }

    private async Task MaintainAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(options.Value.RefreshIntervalHours));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during host shutdown.
        }
        catch (Exception exception)
        {
            MaintenanceFailed(logger, exception);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IPublicDiscoveryRankingProjectionRefresher>()
            .RefreshAllAsync(cancellationToken);
        MaintenanceCompleted(logger);
    }

    [LoggerMessage(
        EventId = 4150,
        Level = LogLevel.Information,
        Message = "Public discovery projection maintenance is disabled.")]
    private static partial void MaintenanceDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 4151,
        Level = LogLevel.Information,
        Message = "Public discovery ranking projection refreshed.")]
    private static partial void MaintenanceCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 4152,
        Level = LogLevel.Error,
        Message = "Public discovery projection maintenance stopped after an unexpected failure.")]
    private static partial void MaintenanceFailed(ILogger logger, Exception exception);
}
