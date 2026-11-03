using BuildingBlock.Application.Time;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Subscriptions;

public sealed partial class SubscriptionLifecycleProcessor(ISubscriptionDataStore store, IServiceScopeFactory scopes,
    IDateTimeProvider clock, IOptionsMonitor<SubscriptionCatalogOptions> options, ILogger<SubscriptionLifecycleProcessor> logger)
{
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var ids = await store.DueSubscriptionsAsync(clock.UtcNow, options.CurrentValue.BackgroundProcessing.BatchSize, ct);
        var processed = 0;
        foreach (var id in ids)
        {
            try
            {
                // Dispose failed transaction tracking before processing another doctor's lifecycle.
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessSubscriptionLifecycleCommand(id), ct);
                if (result.IsFailure) ItemFailed(logger, id, "ResultFailure");
                else processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) { ItemFailed(logger, id, exception.GetType().Name); }
        }
        return processed;
    }
    [LoggerMessage(EventId = 4361, Level = LogLevel.Error, Message = "Subscription lifecycle item {SubscriptionId} failed with {ExceptionType}.")]
    private static partial void ItemFailed(ILogger logger, Guid subscriptionId, string exceptionType);
}
internal sealed partial class SubscriptionLifecycleBackgroundService(IServiceScopeFactory scopes,
    IOptionsMonitor<SubscriptionCatalogOptions> options, ILogger<SubscriptionLifecycleBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastValid = options.CurrentValue.BackgroundProcessing;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                lastValid = options.CurrentValue.BackgroundProcessing;
                if (lastValid.Enabled)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<SubscriptionLifecycleProcessor>().ProcessBatchAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { ProcessingFailed(logger, e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(lastValid.PollingIntervalSeconds), stoppingToken);
        }
    }
    [LoggerMessage(EventId = 4360, Level = LogLevel.Error, Message = "Subscription lifecycle processing failed with {ExceptionType}.")]
    private static partial void ProcessingFailed(ILogger logger, string exceptionType);
}
