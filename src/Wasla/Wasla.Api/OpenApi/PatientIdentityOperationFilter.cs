using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wasla.Api.Controllers;
using Wasla.Application.Features.PatientOnboarding;
namespace Wasla.Api.OpenApi;
public sealed class PatientIdentityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controller = context.MethodInfo.DeclaringType;
        var method = context.MethodInfo.Name;
        var legacyRegistration = controller == typeof(AuthController) && method == "RegisterPatient";
        var identity = controller == typeof(PatientOnboardingController) || controller == typeof(PatientsRegistrationController) ||
            controller == typeof(PatientClaimsAdminController) || controller == typeof(PatientDuplicatesAdminController) || legacyRegistration;
        if (!identity) return;
        operation.Parameters ??= [];
        if (context.ApiDescription.HttpMethod == "POST" && method is not ("Start" or "Verify" or "Activate"))
            operation.Parameters.Add(new OpenApiParameter { Name = "Idempotency-Key", In = ParameterLocation.Header,
                Required = method != "Matches", Description = method == "Matches" ? "Required for ConfirmReuse/ConfirmClaimIdentity; optional for Search." : "UUID; replay with identical payload.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid", MaxLength = 100 } });
        if (controller == typeof(PatientOnboardingController) && method is "ClaimStatus" or "Resubmit")
            operation.Parameters.Add(new OpenApiParameter { Name = "X-Onboarding-Token", In = ParameterLocation.Header, Required = true,
                Description = "Same verified applicant; 30-minute expiry. Mutation capability must be unused for resubmit.", Schema = new OpenApiSchema { Type = JsonSchemaType.String } });
        if (controller == typeof(PatientsRegistrationController) && method == "Matches")
            operation.Responses!["200"] = new OpenApiResponse { Description = "Search or confirmed identity attestation (selected by mode).",
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = new OpenApiSchema { OneOf = [
                    context.SchemaGenerator.GenerateSchema(typeof(PatientRegistrationMatchesResponse), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(PatientIdentityAttestationResponse), context.SchemaRepository)] } } } };
        operation.Responses ??= new OpenApiResponses();
        foreach (var code in new[] { "400", "401", "403", "404", "409", "422", "429" })
            operation.Responses.TryAdd(code, new OpenApiResponse { Description = "Localized Wasla ProblemDetails with stable domain code; see Phase 5 API handoff.",
                Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), context.SchemaRepository) } } });
        if (operation.RequestBody?.Content is not null)
        {
            var required = legacyRegistration || controller == typeof(PatientOnboardingController) && method == "Register"
                ? new[] { "emailVerificationToken" } : controller == typeof(PatientsRegistrationController) && method == "Full"
                ? new[] { "doctorPracticeId", "nameAr", "dateOfBirth", "gender", "email", "verifiedInPerson", "identityVerificationBasis" } : [];
            foreach (var content in operation.RequestBody.Content.Values)
                if (content.Schema is OpenApiSchema { Properties: not null } schema)
                    foreach (var field in schema.Properties.Keys.Where(f => required.Contains(f, StringComparer.OrdinalIgnoreCase)))
                    { schema.Required ??= new HashSet<string>(); schema.Required.Add(field); }
        }
        if (controller == typeof(PatientClaimsAdminController) && method == "Evidence")
            operation.Responses["200"] = new OpenApiResponse { Description = "Private identity evidence stream, Cache-Control:no-store; access audited.",
                Content = new Dictionary<string, OpenApiMediaType> { ["application/octet-stream"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } } } };
    }
}
