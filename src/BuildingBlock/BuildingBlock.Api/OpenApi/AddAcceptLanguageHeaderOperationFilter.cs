using BuildingBlock.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BuildingBlock.Api.OpenApi
{
    internal sealed class AddAcceptLanguageHeaderOperationFilter : IOperationFilter
    {
        private readonly SharedLocalizationOptions _options;

        public AddAcceptLanguageHeaderOperationFilter(IOptions<SharedLocalizationOptions> options)
        {
            _options = options.Value;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<IOpenApiParameter>();

            if (operation.Parameters.Any(parameter =>
                    parameter.In == ParameterLocation.Header &&
                    string.Equals(parameter.Name, "Accept-Language", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Description = "Preferred response language.",
                Required = false,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = _options.SupportedCultures
                        .Select(culture => (JsonNode)JsonValue.Create(culture)!)
                        .ToList()
                }
            });
        }
    }
}
