using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Features.Reporting;

namespace Wasla.Tests.Integration;

public sealed class Phase18ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> Controllers = ["OperatingFinance", "DoctorReporting", "DoctorInsights", "DoctorHandovers",
        "ReceptionReporting", "ReceptionHandovers", "PlatformReporting", "Investigations", "PatientDashboard",
        "DoctorReportExports", "ReceptionReportExports", "SuperAdminReportExports", "ExportRuntimeHealth"];
    [Fact]
    public async Task OpenApi_has_concrete_typed_routes_binary_exports_and_retry_headers()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var client = app.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json); var paths = document.RootElement.GetProperty("paths");
        var binary = paths.GetProperty("/api/v1/doctors/me/reports/{reportCode}/export").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("content").GetProperty("application/pdf").GetProperty("schema");
        Assert.Equal("binary", binary.GetProperty("format").GetString());
        var provider = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        var apis = provider.ApiDescriptionGroups.Items.SelectMany(g => g.Items).Where(d => d.ActionDescriptor is ControllerActionDescriptor action && Controllers.Contains(action.ControllerName)).ToArray();
        Assert.NotEmpty(apis);
        foreach (var api in apis)
        {
            Assert.Contains(api.SupportedResponseTypes, r => r.StatusCode == 200 && r.Type != null);
            if (api.HttpMethod is "POST" or "PUT" or "PATCH")
                Assert.Contains(api.ParameterDescriptions, p => p.Name == "Idempotency-Key" && p.IsRequired);
        }
        var contract = apis.OrderBy(a => a.RelativePath).ThenBy(a => a.HttpMethod).Select((d, i) =>
        {
            var a = (ControllerActionDescriptor)d.ActionDescriptor;
            return new { endpointId = "P18-" + (i + 1).ToString("000", System.Globalization.CultureInfo.InvariantCulture),
                method = d.HttpMethod, route = "/" + d.RelativePath, controller = a.ControllerName, action = a.ActionName,
                roles = a.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Roles).Where(x => x != null),
                policies = a.MethodInfo.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).Where(x => x != null),
                parameters = d.ParameterDescriptions.Select(p => new { p.Name, source = p.Source.DisplayName, type = p.Type?.FullName, p.IsRequired }),
                responses = d.SupportedResponseTypes.Select(r => new { r.StatusCode, type = r.Type?.FullName }) };
        }).ToArray();
        var output = Environment.GetEnvironmentVariable("WASLA_CONTRACT_OUTPUT_DIRECTORY");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            var allMetadata = provider.ApiDescriptionGroups.Items.SelectMany(g => g.Items).Where(d => d.ActionDescriptor is ControllerActionDescriptor)
                .Select(d =>
                {
                    var a = (ControllerActionDescriptor)d.ActionDescriptor;
                    var attributes = a.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>().Concat(a.MethodInfo.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
                    return new { method = d.HttpMethod, route = "/" + d.RelativePath, controller = a.ControllerName, action = a.ActionName,
                        roles = attributes.Select(x => x.Roles).Where(x => x != null), policies = attributes.Select(x => x.Policy).Where(x => x != null) };
                }).ToArray();
            await File.WriteAllTextAsync(Path.Combine(output, "all-api-manifest.json"), JsonSerializer.Serialize(allMetadata, JsonOptions), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(output, "openapi-all.json"), json, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(output, "phase18-contract-manifest.json"), JsonSerializer.Serialize(contract, JsonOptions), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(output, "kpi-definitions.json"), JsonSerializer.Serialize(app.Services.GetRequiredService<IKpiDefinitionRegistry>().Definitions, JsonOptions), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(output, "report-definitions.json"), JsonSerializer.Serialize(ReportDefinitionCatalog.Definitions, JsonOptions), TestContext.Current.CancellationToken);
        }
    }
}
