using System.Text.Json;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Application.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

var root = Path.GetFullPath(args.FirstOrDefault() ?? Directory.GetCurrentDirectory());
var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "src/Wasla/Wasla.Api"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Production.json").Build();
var smtp = configuration.GetSection("Smtp").Get<SmtpOptions>()!;
var evidence = new Dictionary<string, object?> { ["checkedAtUtc"] = DateTime.UtcNow, ["messageSent"] = false };
var output = Path.Combine(root, "artifacts/family-smtp-evidence.json"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
try
{
    using var client = new SmtpClient { Timeout = 15000 };
    await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls);
    evidence["connectivity"] = true; evidence["startTls"] = client.IsSecure;
    await client.AuthenticateAsync(smtp.UserName, smtp.Password); evidence["authentication"] = client.IsAuthenticated;
    await client.DisconnectAsync(true);
    if (args.Contains("--send", StringComparer.Ordinal))
    {
        var recipient = configuration["ProductionVerification:TestRecipient"];
        if (!System.Net.Mail.MailAddress.TryCreate(recipient, out _)) throw new InvalidOperationException("Configure the explicitly approved test recipient.");
        var dbName = "WaslaSmtpProbe_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer($"Server=.\\SQLEXPRESS;Database={dbName};Integrated Security=True;TrustServerCertificate=True").Options;
        await using var db = new WaslaDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var services = new ServiceCollection(); services.AddLogging(); services.AddBuildingBlockMailKitEmail(configuration);
            await using var provider = services.BuildServiceProvider();
            var sender = new OnceOnlySender(provider.GetRequiredService<IEmailSender>(), Path.Combine(root, "artifacts/family-smtp-send-attempt.json"));
            var clock = new ProbeClock(); var queue = new EmailOutbox(db, clock);
            var message = new QueueEmailMessage("owner-approved-synthetic-smtp-20261108", recipient!, "Wasla synthetic SMTP and outbox verification",
                "<p>This is the single owner-approved synthetic Wasla SMTP/outbox test. It contains no patient or clinical information.</p>",
                "This is the single owner-approved synthetic Wasla SMTP/outbox test. No patient or clinical information.");
            await queue.QueueAsync(message); await queue.QueueAsync(message); await db.SaveChangesAsync();
            if (await db.EmailOutboxMessages.CountAsync() != 1) throw new InvalidOperationException("Outbox idempotency failed.");
            var processor = new EmailOutboxProcessor(db, sender, clock, Options.Create(new EmailOutboxOptions { Enabled = true, MaxAttempts = 1 }), NullLogger<EmailOutboxProcessor>.Instance);
            await processor.ProcessBatchAsync(); db.ChangeTracker.Clear();
            var row = await db.EmailOutboxMessages.SingleAsync();
            evidence["outboxStatus"] = row.Status.ToString(); evidence["attempts"] = row.AttemptCount; evidence["messageSent"] = row.Status == EmailOutboxStatus.Sent;
            evidence["secondProcessingCount"] = await processor.ProcessBatchAsync(); evidence["sendInvocations"] = sender.Count;
            evidence["deliveryVerification"] = "SMTP server acceptance only; recipient inbox receipt is not observable.";
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
catch (Exception error) { evidence["failureType"] = error.GetType().Name; Environment.ExitCode = 1; }
File.WriteAllText(output, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(evidence));

sealed class ProbeClock : IDateTimeProvider { public DateTime UtcNow => DateTime.UtcNow; }
sealed class OnceOnlySender(IEmailSender inner, string marker) : IEmailSender
{
    public int Count { get; private set; }
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using (var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(file, new { attemptedAtUtc = DateTime.UtcNow, synthetic = true }, cancellationToken: ct); }
        Count++; await inner.SendAsync(message, ct);
    }
}
