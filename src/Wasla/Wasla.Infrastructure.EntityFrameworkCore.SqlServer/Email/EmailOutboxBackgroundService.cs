using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

internal sealed partial class EmailOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOutboxOptions> options,
    ILogger<EmailOutboxBackgroundService> logger)
    : BackgroundService
{
    private readonly EmailOutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                processed = await scope.ServiceProvider
                    .GetRequiredService<EmailOutboxProcessor>()
                    .ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ProcessorFailed(logger, exception.GetType().Name);
            }

            var delay = processed >= _options.BatchSize
                ? TimeSpan.FromMilliseconds(250)
                : TimeSpan.FromSeconds(_options.PollingIntervalSeconds);
            await Task.Delay(delay, stoppingToken);
        }
    }

    [LoggerMessage(
        EventId = 4320,
        Level = LogLevel.Error,
        Message = "Email outbox processing cycle failed with {ExceptionType}.")]
    private static partial void ProcessorFailed(ILogger logger, string exceptionType);
}
