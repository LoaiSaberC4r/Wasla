using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Unit;

public sealed class PublicDiscoveryHostedServiceShutdownTests
{
    [Fact]
    public async Task Repeated_and_concurrent_shutdown_cancels_and_disposes_the_worker_once()
    {
        var refresher = new Refresher();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IPublicDiscoveryRankingProjectionRefresher>(refresher);
        services.AddSingleton<IOptions<PublicDiscoveryProjectionOptions>>(Options.Create(new PublicDiscoveryProjectionOptions { Enabled = true, RefreshIntervalHours = 1 }));
        var workerType = typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.PublicDiscoveryProjectionMaintenanceHostedService")!;
        services.AddSingleton(typeof(IHostedService), workerType);
        await using var provider = services.BuildServiceProvider();
        var worker = provider.GetRequiredService<IHostedService>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(worker.StopAsync(TestContext.Current.CancellationToken), worker.StopAsync(TestContext.Current.CancellationToken));
        await worker.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, refresher.RefreshCount);
    }
    private sealed class Refresher : IPublicDiscoveryRankingProjectionRefresher
    {
        internal int RefreshCount { get; private set; }
        public Task RefreshAllAsync(CancellationToken cancellationToken) { RefreshCount++; return Task.CompletedTask; }
        public Task RefreshDoctorsAsync(IReadOnlyCollection<Guid> doctorIds, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RefreshPracticesAsync(IReadOnlyCollection<Guid> practiceIds, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
