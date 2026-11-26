namespace Wasla.Application.Email;

public sealed record EmailNotificationContent(string Subject, string HtmlBody, string? TextBody = null);

public interface IEmailNotificationFactory
{
    Task<EmailNotificationContent> CreateAsync(EmailTemplateModel model, CancellationToken cancellationToken = default);
}
