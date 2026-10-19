using BuildingBlock.Api.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Wasla.Api.Configuration;

// Keep the BuildingBlock envelope and all unrelated mappings. Phase 5's approved
// boundary distinguishes malformed input (400) from age/evidence rejection (422).
public sealed class PatientIdentityProblemDetailsFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";
        var identity = path.StartsWith("/api/v1/patient-onboarding", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/admin/patient-claims", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/admin/patient-duplicates", StringComparison.OrdinalIgnoreCase) ||
            path is "/api/v1/patients" or "/api/v1/patients/search" or "/api/v1/patients/me" or "/api/v1/patients/full" or "/api/v1/patients/registration-matches" or "/api/v1/auth/patients/register";
        if (identity && context.Result is IProblemDetailsErrorResult errors)
        {
            var mapper = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsMapper>();
            var problem = mapper.Map(context.HttpContext, errors.Errors);
            var rejection = errors.Errors.Any(e => e.Code is "PatientClaims.ProofInsufficient" or "Patient.FullRegistrationAgeRestricted" or "Patient.SelfRegistrationAgeRestricted");
            if (problem.Status == StatusCodes.Status422UnprocessableEntity && !rejection)
            {
                problem.Status = StatusCodes.Status400BadRequest;
                var mapped = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
                mapped.ContentTypes.Add("application/problem+json"); context.Result = mapped;
            }
        }
        await next();
    }
}
