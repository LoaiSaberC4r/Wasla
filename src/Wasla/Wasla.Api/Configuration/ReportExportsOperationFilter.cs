using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Wasla.Api.Configuration;

internal sealed class ReportExportsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType?.Name is not ("DoctorReportExportsController" or "ReceptionReportExportsController" or "SuperAdminReportExportsController")) return;
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["200"] = new OpenApiResponse
        {
            Description = "Authorized attachment. Content-Disposition supplies the sanitized filename.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/pdf"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
                ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } }
            }
        };
    }
}
