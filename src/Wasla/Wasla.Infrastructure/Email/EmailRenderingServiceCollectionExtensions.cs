using BuildingBlock.Application.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wasla.Infrastructure.Email;

public static class EmailRenderingServiceCollectionExtensions
{
    public static IServiceCollection AddWaslaEmailRendering(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailBrandingOptions>().Bind(configuration.GetSection(EmailBrandingOptions.SectionName));
        services.TryAddSingleton<EmailBrandingProvider>();
        services.TryAddSingleton<EmailTemplateRegistry>();
        services.TryAddSingleton<IEmailTemplateRenderer, WaslaEmailTemplateRenderer>();
        return services;
    }
}
