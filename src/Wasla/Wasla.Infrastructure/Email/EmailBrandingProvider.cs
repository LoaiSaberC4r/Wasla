using Microsoft.Extensions.Options;
using Wasla.Application.Email;

namespace Wasla.Infrastructure.Email;

public sealed class EmailBrandingOptions
{
    public const string SectionName = "EmailBranding";
    public string FooterImageUrl { get; set; } = string.Empty;
}

internal sealed class EmailBrandingProvider(IOptions<EmailBrandingOptions> options)
    : IEmailBrandingProvider
{
    public string FooterImageUrl { get; } = options.Value.FooterImageUrl;
}

