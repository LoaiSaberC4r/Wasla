namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage()
    {
    }

    internal EmailOutboxMessage(
        Guid id,
        string idempotencyKey,
        string recipientEmail,
        string subject,
        string htmlBody,
        string? textBody,
        DateTime createdOnUtc)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        RecipientEmail = recipientEmail;
        Subject = subject;
        HtmlBody = htmlBody;
        TextBody = textBody;
        CreatedOnUtc = createdOnUtc;
        Status = EmailOutboxStatus.Pending;
    }

    public Guid Id { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string HtmlBody { get; private set; } = string.Empty;
    public string? TextBody { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptOnUtc { get; private set; }
    public string? LastError { get; private set; }
    public EmailOutboxStatus Status { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
