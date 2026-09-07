using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Wasla.Api.Configuration;

public static class CorsServiceCollectionExtensions
{
    public static IServiceCollection AddWaslaCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<WaslaCorsOptions>()
            .Bind(configuration.GetSection(WaslaCorsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<WaslaCorsOptions>,
            WaslaCorsOptionsValidator>();
        services.AddCors();
        services.AddSingleton<
            IConfigureOptions<CorsOptions>,
            WaslaCorsPolicyConfigurator>();

        return services;
    }
}

internal sealed class WaslaCorsPolicyConfigurator
    : IConfigureOptions<CorsOptions>
{
    private readonly IOptions<WaslaCorsOptions> _configuredOptions;

    public WaslaCorsPolicyConfigurator(
        IOptions<WaslaCorsOptions> configuredOptions)
    {
        ArgumentNullException.ThrowIfNull(configuredOptions);
        _configuredOptions = configuredOptions;
    }

    public void Configure(CorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configuredOptions = _configuredOptions.Value;
        var allowedOrigins = (configuredOptions.AllowedOrigins ?? [])
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.AddPolicy(CorsPolicyNames.Default, policy =>
        {
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();

            if (configuredOptions.AllowAnyOrigin)
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(allowedOrigins);
            }

            if (configuredOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });
    }
}
