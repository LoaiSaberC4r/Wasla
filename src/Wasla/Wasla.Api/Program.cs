using Wasla.Api.Configuration;
using Wasla.Application;
using Wasla.Infrastructure;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Asp.Versioning;
using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Wasla.Api.Security;

EnvironmentFileLoader.LoadIfDevelopment(args);

var builder = WebApplication.CreateBuilder(args);

builder.AddBuildingBlockSerilog("Wasla.Api");

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddApplicationPart(typeof(BuildingBlock.Api.ProblemDetailsMappingMvc).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddBuildingBlockSwagger(options =>
{
    options.ApiTitle = "Wasla API";
});
builder.Services.AddBuildingBlockLocalization(builder.Configuration);
builder.Services.AddBuildingBlockProblemDetails();
builder.Services.AddWaslaCors(builder.Configuration);
builder.Services.AddWaslaApplication();
builder.Services.AddWaslaInfrastructure(builder.Configuration);
builder.Services.AddWaslaSqlServerPersistence(builder.Configuration);
builder.Services.Configure<MediaStorageOptions>(options =>
    options.ContentRootPath = builder.Environment.ContentRootPath);
builder.Services.AddWaslaAuthentication();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    AddFixedPolicy("password-reset-request", 5);
    AddFixedPolicy("password-reset-verify", 30);
    AddFixedPolicy("password-reset-complete", 10);

    void AddFixedPolicy(string name, int permitLimit)
        => options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<WaslaDbContext>();

var app = builder.Build();

app.UseBuildingBlockSerilog();
app.UseHttpsRedirection();
app.UseBuildingBlockLocalization();
var emailAssetsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "email-assets");
if (Directory.Exists(emailAssetsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(emailAssetsPath),
        RequestPath = "/email-assets"
    });
}
app.UseRouting();
app.UseCors(CorsPolicyNames.Default);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("./v1/swagger.json", "Wasla API v1");
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
