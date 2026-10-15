using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ArchiveBackgroundService(IServiceScopeFactory scopes,IOptions<ArchiveOptions> options,ILogger<ArchiveBackgroundService> logger):BackgroundService
{
    private static readonly ActivitySource Activities=new("Wasla.Archive");
    private static readonly Action<ILogger,string,Exception?> Failed=LoggerMessage.Define<string>(LogLevel.Error,new EventId(4820,"ArchiveCycleFailed"),"Archive cycle failed with {ExceptionType}.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
        do
        {
            if(options.Value.Enabled)
            {
                try
                {
                    using var activity=Activities.StartActivity("Archive.Cycle");
                    await using var scope=scopes.CreateAsyncScope();
                    var population=scope.ServiceProvider.GetRequiredService<ArchiveRegistryPopulation>();
                    await population.PopulateAsync(options.Value.BatchSize,stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<ArchiveEngine>().ProcessAsync(stoppingToken);
                }
                catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
                catch(Exception exception){Failed(logger,exception.GetType().Name,null);}
            }
        }while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
