using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class BuildingBlockProblemActionResult : IActionResult
    {
        private readonly IReadOnlyCollection<Error> _errors;

        public BuildingBlockProblemActionResult(IEnumerable<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var normalized = errors.ToArray();
            if (normalized.Any(error => error is null))
            {
                throw new ArgumentException("ProblemDetails errors cannot contain null values.", nameof(errors));
            }

            _errors = normalized;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var httpContext = context.HttpContext;
            var mapper = httpContext.RequestServices.GetRequiredService<IProblemDetailsMapper>();
            var writer = httpContext.RequestServices.GetRequiredService<IBuildingBlockProblemDetailsWriter>();
            var problem = mapper.Map(httpContext, _errors);

            await writer.WriteAsync(httpContext, problem, httpContext.RequestAborted);
        }
    }
}
