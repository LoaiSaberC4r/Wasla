using System.Net.Mail;
using Wasla.Application.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

public sealed class EmailOutbox(
    WaslaDbContext dbContext,
    IDateTimeProvider clock)
    : IEmailOutbox
{
    public async Task QueueAsync(
        QueueEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(message);

        var idempotencyKey = message.IdempotencyKey.Trim();
        var alreadyTracked = dbContext.EmailOutboxMessages.Local.Any(
            item => item.IdempotencyKey == idempotencyKey);
        if (alreadyTracked || await dbContext.EmailOutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    item => item.IdempotencyKey == idempotencyKey,
                    cancellationToken))
        {
            return;
        }

        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage(
            Guid.NewGuid(),
            idempotencyKey,
            message.RecipientEmail.Trim(),
            message.Subject.Trim(),
            message.HtmlBody,
            message.TextBody,
            RequireUtc(clock.UtcNow)));
    }

    private static void Validate(QueueEmailMessage message)
    {
        var recipient = message.RecipientEmail?.Trim() ?? string.Empty;
        var subject = message.Subject?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message.IdempotencyKey) ||
            message.IdempotencyKey.Trim().Length > 450 ||
            recipient.Length > 320 ||
            recipient.Contains('\r', StringComparison.Ordinal) ||
            recipient.Contains('\n', StringComparison.Ordinal) ||
            !MailAddress.TryCreate(recipient, out var parsedRecipient) ||
            !string.Equals(parsedRecipient.Address, recipient, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(subject) ||
            subject.Length > 500 ||
            subject.Contains('\r', StringComparison.Ordinal) ||
            subject.Contains('\n', StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(message.HtmlBody) &&
            string.IsNullOrWhiteSpace(message.TextBody))
        {
            throw new ArgumentException("The email outbox message is invalid.", nameof(message));
        }
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
