using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.Options;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace BuildingBlock.Tests;

public sealed class OpenApiIntegrationTests
{
    [Fact]
    public async Task Versioned_documents_generate_with_security_localization_and_problem_contracts()
    {
        await using var application = await CreateApplicationAsync();
        var descriptionProvider = application.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        var swaggerProvider = application.Services.GetRequiredService<ISwaggerProvider>();
        var descriptions = descriptionProvider.ApiVersionDescriptions.ToArray();

        Assert.Equal(new[] { "v1", "v2" }, descriptions.Select(description => description.GroupName));
        Assert.False(descriptions[0].IsDeprecated);
        Assert.True(descriptions[1].IsDeprecated);

        foreach (var description in descriptions)
        {
            var document = swaggerProvider.GetSwagger(description.GroupName);
            Assert.NotNull(document.Info);
            Assert.Contains(description.ApiVersion.ToString(), document.Info.Title, StringComparison.Ordinal);
            Assert.NotEmpty(document.Paths);
            Assert.True(document.Components!.SecuritySchemes!.ContainsKey("Bearer"));

            if (description.IsDeprecated)
            {
                Assert.Contains("deprecated", document.Info.Description, StringComparison.OrdinalIgnoreCase);
            }

            var authorized = GetOperation(document, "/authorized", HttpMethod.Get);
            var anonymous = GetOperation(document, "/anonymous", HttpMethod.Get);
            var noContent = GetOperation(document, "/no-content", HttpMethod.Delete);
            var genericResult = GetOperation(document, "/generic-result", HttpMethod.Get);
            var nonGenericResult = GetOperation(document, "/non-generic-result", HttpMethod.Post);
            var existingHeader = GetOperation(document, "/existing-header", HttpMethod.Get);

            Assert.Single(authorized.Security!);
            Assert.Contains("Roles: Reader", authorized.Description, StringComparison.Ordinal);
            Assert.Contains("Policies: DocumentationPolicy", authorized.Description, StringComparison.Ordinal);
            Assert.True(anonymous.Security is null || anonymous.Security.Count == 0);

            var languageParameters = authorized.Parameters!
                .Where(parameter =>
                    parameter.In == ParameterLocation.Header &&
                    string.Equals(parameter.Name, "Accept-Language", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Single(languageParameters);
            var languageSchema = Assert.IsType<OpenApiSchema>(languageParameters[0].Schema);
            Assert.Equal(2, languageSchema.Enum!.Count);
            Assert.Single(existingHeader.Parameters!, parameter =>
                parameter.In == ParameterLocation.Header &&
                string.Equals(parameter.Name, "Accept-Language", StringComparison.OrdinalIgnoreCase));

            Assert.True(authorized.Responses!.ContainsKey("200"));
            Assert.NotNull(authorized.Responses["200"].Content!["application/json"].Schema);
            Assert.True(noContent.Responses!.ContainsKey("204"));
            Assert.True(genericResult.Responses!.ContainsKey("200"));
            Assert.NotNull(genericResult.Responses["200"].Content!["application/json"].Schema);
            Assert.True(nonGenericResult.Responses!.ContainsKey("204"));
            Assert.False(nonGenericResult.Responses.ContainsKey("200"));

            foreach (var status in new[] { "401", "403", "404", "409", "422", "429", "500", "503" })
            {
                var response = authorized.Responses[status];
                Assert.True(response.Content!.ContainsKey("application/problem+json"));
                var schema = Assert.IsType<OpenApiSchema>(response.Content["application/problem+json"].Schema);
                Assert.True(schema.Properties!.ContainsKey("traceId"));
                Assert.True(schema.Properties.ContainsKey("correlationId"));
                var errors = Assert.IsType<OpenApiSchema>(schema.Properties["errors"]);
                var error = Assert.IsType<OpenApiSchema>(errors.Items);
                Assert.Contains("code", error.Properties!.Keys);
                Assert.Contains("retryAfter", error.Properties.Keys);
            }
        }
    }

    [Fact]
    public async Task Invalid_localization_configuration_fails_during_startup()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var application = await BuildingBlockTestApplication.CreateAsync(services =>
            {
                services.AddControllers();
                services.AddApiVersioning()
                    .AddMvc()
                    .AddApiExplorer(options => options.GroupNameFormat = "'v'VVV");
                services.Configure<SharedLocalizationOptions>(options =>
                {
                    options.SupportedCultures = Array.Empty<string>();
                    options.DefaultCulture = "en";
                });
                services.AddBuildingBlockSwagger();
            });
        });
    }

    private static Task<BuildingBlockTestApplication> CreateApplicationAsync()
        => BuildingBlockTestApplication.CreateAsync(
            services =>
            {
                services.AddAuthorization(options =>
                    options.AddPolicy("DocumentationPolicy", policy => policy.RequireAuthenticatedUser()));
                services.AddControllers().AddApplicationPart(typeof(Phase2OpenApiController).Assembly);
                services.AddEndpointsApiExplorer();
                services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                    })
                    .AddMvc()
                    .AddApiExplorer(options =>
                    {
                        options.GroupNameFormat = "'v'VVV";
                        options.SubstituteApiVersionInUrl = true;
                    });
                services.Configure<SharedLocalizationOptions>(options =>
                {
                    options.SupportedCultures = new[] { "en", "ar" };
                    options.DefaultCulture = "en";
                });
                services.AddSwaggerGen();
                services.AddBuildingBlockSwagger(options => options.ApiTitle = "BuildingBlock Test API");
            },
            app => app.MapControllers());

    private static OpenApiOperation GetOperation(OpenApiDocument document, string pathSuffix, HttpMethod method)
    {
        var path = Assert.Single(document.Paths, candidate =>
            candidate.Key.EndsWith(pathSuffix, StringComparison.Ordinal));
        Assert.True(path.Value.Operations!.TryGetValue(method, out var operation));
        return operation;
    }
}

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0", Deprecated = true)]
[Authorize]
[Route("api/v{version:apiVersion}/openapi")]
public sealed class Phase2OpenApiController : ControllerBase
{
    [HttpGet("authorized")]
    [Authorize(Roles = "Reader", Policy = "DocumentationPolicy")]
    public ActionResult<Phase2OpenApiPayload> GetAuthorized()
        => Ok(new Phase2OpenApiPayload("phase2"));

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public ActionResult<Phase2OpenApiPayload> GetAnonymous()
        => Ok(new Phase2OpenApiPayload("anonymous"));

    [HttpDelete("no-content")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete()
        => NoContent();

    [HttpGet("generic-result")]
    public Result<Phase2OpenApiPayload> GetGenericResult()
        => Result<Phase2OpenApiPayload>.Ok(new Phase2OpenApiPayload("result"));

    [HttpPost("non-generic-result")]
    public Result PostNonGenericResult()
        => Result.Ok();

    [HttpGet("existing-header")]
    public ActionResult<Phase2OpenApiPayload> GetWithExistingHeader(
        [FromHeader(Name = "Accept-Language")] string? language)
        => Ok(new Phase2OpenApiPayload(language ?? "default"));
}

public sealed record Phase2OpenApiPayload(string Value);
