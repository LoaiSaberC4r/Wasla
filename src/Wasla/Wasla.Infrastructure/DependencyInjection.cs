using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Email;
using Wasla.Application.Media;
using Wasla.Application.Security;
using Wasla.Infrastructure.Email;
using Wasla.Infrastructure.Media;
using Wasla.Infrastructure.Security;

namespace Wasla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWaslaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBuildingBlockCaching();
        services.AddBuildingBlockMailKitEmail(configuration);
        services.AddBuildingBlockPasswordHashing(configuration);
        services.AddBuildingBlockTokenReader(configuration);
        services.AddBuildingBlockMedia(configuration);
        services.AddBuildingBlockFileSystemMediaStorage();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.Issuer) &&
                !string.IsNullOrWhiteSpace(options.Audience) &&
                options.SigningKey.Length >= 32 &&
                options.ExpirationMinutes is > 0 and <= 1440,
                "JWT options are invalid.")
            .ValidateOnStart();
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .Validate(options =>
                options.OtpLength is >= 4 and <= 9 &&
                options.OtpExpirationMinutes is > 0 and <= 60 &&
                options.MaximumVerificationAttempts is > 0 and <= 20 &&
                options.ResendCooldownSeconds is >= 0 and <= 3600 &&
                options.ResetTokenExpirationMinutes is > 0 and <= 120 &&
                options.HmacSecret.Length >= 32,
                "Password reset options are invalid.")
            .ValidateOnStart();
        services.AddOptions<EmailBrandingOptions>()
            .Bind(configuration.GetSection(EmailBrandingOptions.SectionName))
            .Validate(options =>
                Uri.TryCreate(options.FooterImageUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "Email branding FooterImageUrl must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<IPasswordResetProtector, PasswordResetProtector>();
        services.AddSingleton<IEmailBrandingProvider, EmailBrandingProvider>();
        services.AddSingleton<IPrivateMediaReader, PrivateMediaReader>();

        return services;
    }
}
