using BuildingBlock.Api.Options;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BuildingBlock.Api.OpenApi
{
    internal sealed class ConfigureSwaggerOptions : IConfigureNamedOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;
        private readonly BuildingBlockSwaggerOptions _swaggerOptions;

        public ConfigureSwaggerOptions(
            IApiVersionDescriptionProvider provider,
            IOptions<BuildingBlockSwaggerOptions> swaggerOptions)
        {
            _provider = provider;
            _swaggerOptions = swaggerOptions.Value;
        }

        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in _provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, CreateVersionInfo(description));
            }

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.OperationFilter<AddAcceptLanguageHeaderOperationFilter>();
            options.OperationFilter<AuthorizeCheckOperationFilter>();
            options.OperationFilter<ResultPatternOperationFilter>();
        }

        public void Configure(string? name, SwaggerGenOptions options)
            => Configure(options);

        private OpenApiInfo CreateVersionInfo(ApiVersionDescription apiVersionDescription)
        {
            var openApiInfo = new OpenApiInfo
            {
                Title = $"{_swaggerOptions.ApiTitle} v{apiVersionDescription.ApiVersion}",
                Version = apiVersionDescription.ApiVersion.ToString()
            };

            if (apiVersionDescription.IsDeprecated)
            {
                openApiInfo.Description += " This API version has been deprecated.";
            }

            return openApiInfo;
        }
    }
}
