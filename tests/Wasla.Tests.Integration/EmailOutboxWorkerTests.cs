using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Application.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class EmailOutboxWorkerTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Enabled_background_worker_processes_one_durable_message_and_duplicate_key_does_not_resend()
    {
        await using var f = await CreateAsync(true); string? connection = null;
        await f.WithDbAsync(db => { connection = db.Database.GetConnectionString(); return Task.CompletedTask; });
        var recorder = new SyntheticSender(); var options = Options.Create(new EmailOutboxOptions { Enabled = true, PollingIntervalSeconds = 1, BatchSize = 1 });
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(options); services.AddSingleton<IDateTimeProvider>(f.Clock); services.AddSingleton<IEmailSender>(recorder);
        services.AddScoped(_ => new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(connection).Options));
        services.AddScoped<EmailOutbox>(); services.AddScoped(p => new EmailOutboxProcessor(p.GetRequiredService<WaslaDbContext>(), recorder, f.Clock, options, NullLogger<EmailOutboxProcessor>.Instance));
        var workerType = typeof(EmailOutboxProcessor).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email.EmailOutboxBackgroundService")!;
        services.AddSingleton(p => (IHostedService)ActivatorUtilities.CreateInstance(p, workerType));
        await using var provider = services.BuildServiceProvider();
        async Task Queue()
        {
            await using var scope = provider.CreateAsyncScope(); var outbox = scope.ServiceProvider.GetRequiredService<EmailOutbox>();
            await outbox.QueueAsync(new QueueEmailMessage("synthetic-worker-once", "synthetic@example.test", "Synthetic worker test", "<p>Synthetic text only.</p>"));
            await scope.ServiceProvider.GetRequiredService<WaslaDbContext>().SaveChangesAsync();
        }
        await Queue(); var worker = provider.GetRequiredService<IHostedService>(); await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await recorder.Sent.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (true)
            {
                await using var scope = provider.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<WaslaDbContext>().EmailOutboxMessages.AnyAsync(m => m.Status == EmailOutboxStatus.Sent, TestContext.Current.CancellationToken)) break;
                Assert.True(DateTime.UtcNow < deadline, "Worker did not durably acknowledge the synthetic send.");
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
        }
        finally { await worker.StopAsync(TestContext.Current.CancellationToken); }
        await Queue();
        await using var check = provider.CreateAsyncScope();
        Assert.Equal(0, await check.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessBatchAsync(TestContext.Current.CancellationToken));
        var row = Assert.Single(await check.ServiceProvider.GetRequiredService<WaslaDbContext>().EmailOutboxMessages.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EmailOutboxStatus.Sent, row.Status); Assert.Equal(1, recorder.Count); Assert.Equal(1, row.AttemptCount);
    }
    private sealed class SyntheticSender : IEmailSender
    {
        internal int Count; internal TaskCompletionSource Sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Interlocked.Increment(ref Count); Sent.TrySetResult(); return Task.CompletedTask; }
    }
}
