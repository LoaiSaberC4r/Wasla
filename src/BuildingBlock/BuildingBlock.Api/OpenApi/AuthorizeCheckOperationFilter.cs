using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BuildingBlock.Api.OpenApi
{
    internal sealed class AuthorizeCheckOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var attributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .Concat(context.MethodInfo.GetCustomAttributes(true))
                .ToArray() ?? context.MethodInfo.GetCustomAttributes(true);

            if (attributes.OfType<AllowAnonymousAttribute>().Any())
            {
                return;
            }

            var authorizeAttributes = attributes.OfType<AuthorizeAttribute>().ToArray();
            if (authorizeAttributes.Length == 0)
            {
                return;
            }

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            if (!operation.Security.Any(requirement => requirement.Keys.Any(scheme =>
                    scheme is OpenApiSecuritySchemeReference reference &&
                    string.Equals(reference.Reference.Id, "Bearer", StringComparison.Ordinal))))
            {
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", null!, null)] = []
                });
            }

            var roles = authorizeAttributes
                .Select(attribute => attribute.Roles)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var policies = authorizeAttributes
                .Select(attribute => attribute.Policy)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var requirements = new List<string>();
            if (roles.Length > 0)
            {
                requirements.Add("Roles: " + string.Join(", ", roles));
            }

            if (policies.Length > 0)
            {
                requirements.Add("Policies: " + string.Join(", ", policies));
            }

            if (requirements.Count > 0)
            {
                operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                    ? string.Join(Environment.NewLine, requirements)
                    : operation.Description + Environment.NewLine + string.Join(Environment.NewLine, requirements);
            }
        }
    }
}
