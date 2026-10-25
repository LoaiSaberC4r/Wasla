using BuildingBlock.Application.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Domain.Reservations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class Phase17SmtpBoundaryTests
{
    [Fact]
    public async Task O03_O04_smtp_admission_serializes_source_commit_without_holding_sql_transaction()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using var sendScope = app.Services.CreateAsyncScope(); var db = sendScope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var sender = new PausedSender(db);
        var processing = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions()), NullLogger<EmailOutboxProcessor>.Instance,
            sendScope.ServiceProvider.GetRequiredService<NotificationEmailGuard>()).ProcessBatchAsync(TestContext.Current.CancellationToken);
        await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        var cancellation = app.WithDbAsync(async mutationDb =>
        {
            var r = await mutationDb.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            r.Cancel(ReservationCancellationInitiator.Patient, ReservationActionInitiator.Patient, r.CreatedByApplicationUserId,
                ReservationCancellationReasons.PatientChangedPlans, null, app.Clock.UtcNow);
            await mutationDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        try
        {
            var first = await Task.WhenAny(cancellation, Task.Delay(200, TestContext.Current.CancellationToken));
            Assert.NotSame(cancellation, first);
        }
        finally { sender.Release.TrySetResult(); }
        await processing; await cancellation;
        Assert.Equal(1, sender.Accepted);
        Assert.Equal(EmailOutboxStatus.Sent, (await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.NotificationDeliveryId != null, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(ReservationStatus.Cancelled, (await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken)).Status);
        // SMTP acceptance precedes cancellation commit here; the provider cannot retract an accepted email.
    }
    private sealed class PausedSender(WaslaDbContext db) : IEmailSender
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Accepted { get; private set; }
        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Assert.Null(db.Database.CurrentTransaction);
            Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); Accepted++;
        }
    }
}
