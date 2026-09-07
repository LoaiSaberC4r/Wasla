using BuildingBlock.Api.Options;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Api.OpenApi
{
    public static class SwaggerServiceCollectionExtensions
    {
        public static IServiceCollection AddBuildingBlockSwagger(
            this IServiceCollection services,
            Action<BuildingBlockSwaggerOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configure is not null)
            {
                services.Configure(configure);
            }
            else
            {
                services.AddOptions<BuildingBlockSwaggerOptions>();
            }

            services.AddOptions<SharedLocalizationOptions>()
                .Validate(SharedLocalizationOptionsValidator.IsValid, SharedLocalizationOptionsValidator.ValidationMessage)
                .ValidateOnStart();

            services.ConfigureOptions<ConfigureSwaggerOptions>();

            return services;
        }
    }
}
