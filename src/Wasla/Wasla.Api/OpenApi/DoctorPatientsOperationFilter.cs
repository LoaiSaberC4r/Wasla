using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wasla.Api.Controllers;

namespace Wasla.Api.OpenApi;

public sealed class DoctorPatientsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var type = context.MethodInfo.DeclaringType;
        if (type != typeof(DoctorPatientsController) && type != typeof(DoctorPrescriptionPdfController)) return;
        operation.Description = "Active, first-login-complete Approved Doctor; current JWT and database permissions; own actual-attendance relationship. " +
            "DoctorPatients.ViewOwn plus each protected source's original permission; closed-Practice own details additionally require DoctorPatients.ViewHistoricalOwn. " +
            "Private, audited read. Unrelated patients/resources return safe 404. No clinical or booking mutation.";
        if (operation.Responses?.TryGetValue("200", out var response) != true || response is not OpenApiResponse success) return;
        success.Headers ??= new Dictionary<string, IOpenApiHeader>();
        success.Headers["Cache-Control"] = new OpenApiHeader { Description = "no-store", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        success.Headers["X-Content-Type-Options"] = new OpenApiHeader { Description = "nosniff", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        if (type == typeof(DoctorPrescriptionPdfController))
        {
            operation.Description = "Own issued version only; Prescriptions.ViewOwn and Prescriptions.ExportOwn; current account, approval, exact Encounter/Practice ownership and actual attendance. " +
                "Closed Practice additionally requires DoctorPatients.ViewOwn and DoctorPatients.ViewHistoricalOwn. Reauthorized after rendering, audited before releasing bytes. Drafts are unavailable. " +
                "Superseded/voided documents are marked not current/not valid. No legal certification.";
            success.Content = new Dictionary<string, OpenApiMediaType> { ["application/pdf"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } } };
            success.Headers["Content-Disposition"] = new OpenApiHeader { Description = "Validated inline or attachment; server-generated ASCII filename.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        }
    }
}
