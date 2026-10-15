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
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Wasla.Api.xml"));
    options.SchemaFilter<Wasla.Api.OpenApi.MedicalRecordSchemaFilter>();
    options.SchemaFilter<Wasla.Api.OpenApi.ArchiveSchemaFilter>();
    options.OperationFilter<Wasla.Api.OpenApi.ArchiveOperationFilter>();
});
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
builder.Services.Configure<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>(options => options.OperationFilter<ReportExportsOperationFilter>());
builder.Services.AddBuildingBlockLocalization(builder.Configuration);
builder.Services.AddBuildingBlockProblemDetails();
builder.Services.PostConfigure<LogRedactionOptions>(options =>
    options.SensitivePropertyNames = [.. options.SensitivePropertyNames, "ClinicalNotes", "DisplayText", "Notes", "Prescription", "ResponseJson", "Current", "Draft", "Versions", "Items", "Clinical", "Data", "NewMedication", "Medication", "DoctorNote", "Dose", "DoseText", "FrequencyText", "Instructions", "PrnReason", "MinimumIntervalText", "MaxPer24HoursText", "Reason", "CorrectionReason", "VoidReason", "MedicationNameSnapshot", "MedicationName",
        "BeforeSnapshot", "AfterSnapshot", "Changes", "Search", "Attachments", "Content", "File", "PatientNote", "PostVisitReason", "PatientInstructions", "DoctorInstructions", "ExternalProviderName", "CatalogClarificationNote", "SourceDataJson", "LabRequestDraft", "RadiologyRequestDraft", "CurrentResults", "Submission", "SafeContent", "Diagnoses", "Timeline", "Outputs", "Patient", "HiddenEncounterToken", "Cursor", "NextCursor", "OwnerDetails", "OpaqueEntryKey", "ReviewMessage", "RejectionReason", "ReasonOrMessage", "DecisionNote", "BasisReference", "EvidenceFiles", "Documents", "MediaKey", "RelationshipVerification"]);
builder.Services.PostConfigure<LogRedactionOptions>(options => options.SensitivePropertyNames = [.. options.SensitivePropertyNames,
    "Amount", "PlannedAmount", "Allocations", "Plan", "Expense", "Settlement", "Reversal", "Summary", "Description", "DescriptionAr", "DescriptionEn",
    "TitleAr", "TitleEn", "ResolutionNotes", "ClosureNotes", "Outcome", "Annotation", "EvidenceJson", "PolicyJson", "AppliedFiltersJson", "RowVersion", "ReceiptSnapshotJson"]);
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
    options.OnRejected = async (context, ct) =>
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 900;
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 429, Title = "Too many requests / طلبات كثيرة", Type = "https://www.rfc-editor.org/rfc/rfc6585#section-4" };
        problem.Extensions["code"] = "Request.RateLimited";
        await context.HttpContext.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: ct);
    };
    AddFixedPolicy("medical-record", 1200);
    AddFixedPolicy("reports-export", 12);
    AddFixedPolicy("archive-audit", 300);
    AddFixedPolicy("medical-record-media", 120);
    AddFixedPolicy("medical-record-request", 60);
    AddFixedPolicy("password-reset-request", 5);
    AddFixedPolicy("password-reset-verify", 30);
    AddFixedPolicy("password-reset-complete", 10);

    void AddFixedPolicy(string name, int permitLimit)
        => options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            name.StartsWith("medical-record", StringComparison.Ordinal) || name == "reports-export" || name == "archive-audit"
                ? context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
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
app.UseAuthentication();
app.UseRateLimiter();
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
