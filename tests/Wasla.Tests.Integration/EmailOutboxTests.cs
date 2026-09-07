using Wasla.Application.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Wasla.Tests.Integration;

public sealed class EmailOutboxTests
{
    [Fact]
    public async Task QueueAsync_UsesCallerCommit_AndPersistsOnce()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        var message = CreateMessage("queue-once");
        var testToken = TestContext.Current.CancellationToken;

        await fixture.Outbox.QueueAsync(message, testToken);
        await fixture.Outbox.QueueAsync(message, testToken);

        Assert.Equal(
            0,
            await fixture.DbContext.EmailOutboxMessages.CountAsync(testToken));
        Assert.Single(fixture.DbContext.EmailOutboxMessages.Local);

        await fixture.DbContext.SaveChangesAsync(testToken);
        fixture.DbContext.ChangeTracker.Clear();

        var persisted = await fixture.DbContext.EmailOutboxMessages
            .SingleAsync(testToken);
        Assert.Equal(EmailOutboxStatus.Pending, persisted.Status);
        Assert.Equal("queue-once", persisted.IdempotencyKey);
    }

    [Fact]
    public async Task Processor_SendsMessage_AndPropagatesCancellationToken()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        await fixture.PersistAsync(CreateMessage("send-success"));
        using var cancellationSource = new CancellationTokenSource();

        var processed = await fixture.Processor.ProcessBatchAsync(
            cancellationSource.Token);
        fixture.DbContext.ChangeTracker.Clear();

        var persisted = await fixture.DbContext.EmailOutboxMessages
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, processed);
        Assert.Equal(EmailOutboxStatus.Sent, persisted.Status);
        Assert.NotNull(persisted.ProcessedOnUtc);
        Assert.Equal(cancellationSource.Token, fixture.EmailSender.LastToken);
    }

    [Fact]
    public async Task TransientFailure_SchedulesRetry_WithoutDeletingMessage()
    {
        await using var fixture = await OutboxFixture.CreateAsync(
            sendException: new InvalidOperationException("SMTP unavailable"));
        await fixture.PersistAsync(CreateMessage("send-retry"));

        var processed = await fixture.Processor.ProcessBatchAsync(
            TestContext.Current.CancellationToken);
        fixture.DbContext.ChangeTracker.Clear();

        var persisted = await fixture.DbContext.EmailOutboxMessages
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, processed);
        Assert.Equal(EmailOutboxStatus.Pending, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.NotNull(persisted.NextAttemptOnUtc);
        Assert.Equal(
            ExternalServiceErrorCodes.Email.SendFailed,
            persisted.LastError);
    }

    [Fact]
    public async Task ExhaustedFailure_RemainsPersistedAsFailed()
    {
        await using var fixture = await OutboxFixture.CreateAsync(
            maxAttempts: 1,
            sendException: new InvalidOperationException("SMTP unavailable"));
        await fixture.PersistAsync(CreateMessage("send-failed"));

        await fixture.Processor.ProcessBatchAsync(
            TestContext.Current.CancellationToken);
        fixture.DbContext.ChangeTracker.Clear();

        var persisted = await fixture.DbContext.EmailOutboxMessages
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Failed, persisted.Status);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Null(persisted.NextAttemptOnUtc);
    }

    [Fact]
    public async Task Cancellation_StopsBeforeClaimingOrSending()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        await fixture.PersistAsync(CreateMessage("send-cancelled"));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Processor.ProcessBatchAsync(cancellationSource.Token));

        Assert.Equal(0, fixture.EmailSender.SendCount);
        fixture.DbContext.ChangeTracker.Clear();
        var persisted = await fixture.DbContext.EmailOutboxMessages
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Pending, persisted.Status);
        Assert.Equal(0, persisted.AttemptCount);
    }

    [Fact]
    public async Task ProcessingLease_PreventsEarlyClaimAndAllowsRecoveryAfterExpiry()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        await fixture.PersistAsync(CreateMessage("lease-recovery"));
        var leaseUntil = fixture.Clock.UtcNow.AddMinutes(1);
        await fixture.DbContext.EmailOutboxMessages.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(message => message.Status, EmailOutboxStatus.Processing)
                .SetProperty(message => message.NextAttemptOnUtc, leaseUntil)
                .SetProperty(message => message.ProcessingToken, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, await fixture.Processor.ProcessBatchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.EmailSender.SendCount);

        fixture.Clock.UtcNow = leaseUntil.AddSeconds(1);
        Assert.Equal(1, await fixture.Processor.ProcessBatchAsync(TestContext.Current.CancellationToken));
        fixture.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            EmailOutboxStatus.Sent,
            (await fixture.DbContext.EmailOutboxMessages.SingleAsync(TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task DbContext_MapsTrustAccessAndOutboxPersistence()
    {
        await using var fixture = await OutboxFixture.CreateAsync();

        var entityTypes = fixture.DbContext.Model
            .GetEntityTypes()
            .Select(entity => entity.ClrType)
            .ToArray();

        Assert.NotNull(fixture.DbContext.Model
            .FindEntityType(typeof(EmailOutboxMessage)));
        Assert.Contains(typeof(Wasla.Domain.Security.ApplicationUser), entityTypes);
        Assert.Contains(typeof(Wasla.Domain.Security.Role), entityTypes);
        Assert.Contains(typeof(Wasla.Domain.Security.Permission), entityTypes);
        Assert.Contains(typeof(Wasla.Domain.Doctors.Doctor), entityTypes);
        Assert.Contains(typeof(Wasla.Domain.Patients.Patient), entityTypes);
        Assert.Contains(typeof(Wasla.Domain.Security.PasswordResetChallenge), entityTypes);
    }

    [Fact]
    public void TrustAccessMigration_GeneratesRequiredSqlServerTables()
    {
        var options = new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=WaslaDb;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new WaslaDbContext(options);
        var migrator = dbContext.GetService<IMigrator>();
        var script = migrator.GenerateScript(
            options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains(
            "CREATE TABLE [EmailOutboxMessages]",
            script,
            StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [ApplicationUsers]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Doctors]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Patients]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [SuperAdmins]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [PasswordResetChallenges]", script, StringComparison.Ordinal);
        Assert.Contains("WHERE [NationalId] IS NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("WHERE [IsRootSuperAdmin] = 1", script, StringComparison.Ordinal);
        foreach (var removedTable in new[]
                 {
                     "Law" + "yers",
                     "Cli" + "ents",
                     "Consul" + "tationRequests",
                     "Legal" + "Specializations"
                 })
        {
            Assert.DoesNotContain(removedTable, script, StringComparison.Ordinal);
        }
    }

    private static QueueEmailMessage CreateMessage(string idempotencyKey)
        => new(
            idempotencyKey,
            "recipient@example.test",
            "Technical notification",
            "<p>Technical notification</p>",
            "Technical notification");

    private sealed class OutboxFixture : IAsyncDisposable
    {
        private OutboxFixture(
            SqliteConnection connection,
            WaslaDbContext dbContext,
            TestDateTimeProvider clock,
            RecordingEmailSender emailSender,
            EmailOutbox outbox,
            EmailOutboxProcessor processor)
        {
            Connection = connection;
            DbContext = dbContext;
            Clock = clock;
            EmailSender = emailSender;
            Outbox = outbox;
            Processor = processor;
        }

        public SqliteConnection Connection { get; }
        public WaslaDbContext DbContext { get; }
        public TestDateTimeProvider Clock { get; }
        public RecordingEmailSender EmailSender { get; }
        public EmailOutbox Outbox { get; }
        public EmailOutboxProcessor Processor { get; }

        public static async Task<OutboxFixture> CreateAsync(
            int maxAttempts = 5,
            Exception? sendException = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<WaslaDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new WaslaDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var clock = new TestDateTimeProvider(
                new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc));
            var emailSender = new RecordingEmailSender(sendException);
            var outbox = new EmailOutbox(dbContext, clock);
            var processor = new EmailOutboxProcessor(
                dbContext,
                emailSender,
                clock,
                Options.Create(new EmailOutboxOptions
                {
                    Enabled = true,
                    PollingIntervalSeconds = 1,
                    BatchSize = 20,
                    MaxAttempts = maxAttempts,
                    ClaimLeaseSeconds = 300
                }),
                NullLogger<EmailOutboxProcessor>.Instance);

            return new OutboxFixture(
                connection,
                dbContext,
                clock,
                emailSender,
                outbox,
                processor);
        }

        public async Task PersistAsync(QueueEmailMessage message)
        {
            await Outbox.QueueAsync(message);
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestDateTimeProvider(DateTime utcNow)
        : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class RecordingEmailSender(Exception? exception)
        : IEmailSender
    {
        public int SendCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task SendAsync(
            EmailMessage message,
            CancellationToken ct = default)
        {
            SendCount++;
            LastToken = ct;
            return exception is null
                ? Task.CompletedTask
                : Task.FromException(exception);
        }
    }
}
