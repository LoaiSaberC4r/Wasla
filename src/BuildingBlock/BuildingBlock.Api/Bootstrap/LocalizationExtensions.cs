using BuildingBlock.Api.Localization.Providers;
using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace BuildingBlock.Api.Bootstrap
{
    public static class LocalizationExtensions
    {
        public static IServiceCollection AddBuildingBlockLocalization(
            this IServiceCollection services,
            Action<SharedLocalizationOptions>? configure = null)
        {
            services.AddLocalization();

            var optionsBuilder = services.AddOptions<SharedLocalizationOptions>();
            if (configure is not null)
            {
                optionsBuilder.Configure(configure);
            }

            optionsBuilder
                .Validate(SharedLocalizationOptionsValidator.IsValid, SharedLocalizationOptionsValidator.ValidationMessage)
                .ValidateOnStart();

            services.AddOptions<RequestLocalizationOptions>().Configure<IOptions<SharedLocalizationOptions>>(
                (options, localizationOptionsAccessor) =>
                {
                    var localizationOptions = localizationOptionsAccessor.Value;
                    var cultures = localizationOptions.SupportedCultures.Select(culture => new CultureInfo(culture)).ToList();

                    options.SupportedCultures = cultures;
                    options.SupportedUICultures = cultures;
                    options.DefaultRequestCulture = new RequestCulture(localizationOptions.DefaultCulture);

                    var providers = new List<IRequestCultureProvider>();
                    if (localizationOptions.AllowQueryStringLang)
                    {
                        providers.Add(new SupportedQueryStringRequestCultureProvider(localizationOptionsAccessor));
                    }

                    providers.Add(new SimpleAcceptLanguageProvider(localizationOptionsAccessor));
                    options.RequestCultureProviders = providers;
                });

            return services;
        }

        public static IServiceCollection AddBuildingBlockLocalization(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetRequiredSection(
                SharedLocalizationOptions.SectionName);

            var supportedCultures = section
                .GetSection(nameof(SharedLocalizationOptions.SupportedCultures))
                .Get<string[]>()
                ?? Array.Empty<string>();

            var defaultCulture =
                section[nameof(SharedLocalizationOptions.DefaultCulture)]
                ?? string.Empty;

            var allowQueryStringLang =
                section.GetValue<bool?>(
                    nameof(SharedLocalizationOptions.AllowQueryStringLang))
                ?? true;

            return services.AddBuildingBlockLocalization(options =>
            {
                options.SupportedCultures = supportedCultures;
                options.DefaultCulture = defaultCulture;
                options.AllowQueryStringLang = allowQueryStringLang;
            });
        }

        public static IApplicationBuilder UseBuildingBlockLocalization(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
            app.UseRequestLocalization(options);
            app.UseMiddleware<ResponseContentLanguageMiddleware>();
            return app;
        }
    }
}
