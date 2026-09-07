namespace Wasla.Application.Email;

public sealed record QueueEmailMessage(
    string IdempotencyKey,
    string RecipientEmail,
    string Subject,
    string HtmlBody,
    string? TextBody = null);
