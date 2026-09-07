namespace BuildingBlock.Application.Email
{
    public sealed record EmailTemplateRequest(
        string TemplateName,
        IReadOnlyDictionary<string, object?> Model,
        string? Culture = null);

    public sealed record EmailTemplateResult(
        string Subject,
        string? HtmlBody,
        string? TextBody);

    public interface IEmailTemplateRenderer
    {
        Task<EmailTemplateResult> RenderAsync(
            EmailTemplateRequest request,
            CancellationToken ct = default);
    }
}
