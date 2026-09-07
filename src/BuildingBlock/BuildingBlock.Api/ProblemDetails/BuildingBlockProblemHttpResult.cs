using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class BuildingBlockProblemHttpResult : IResult
    {
        private readonly IReadOnlyCollection<Error> _errors;

        public BuildingBlockProblemHttpResult(IEnumerable<Error> errors)
        {
            _errors = Normalize(errors);
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var mapper = httpContext.RequestServices.GetRequiredService<IProblemDetailsMapper>();
            var writer = httpContext.RequestServices.GetRequiredService<IBuildingBlockProblemDetailsWriter>();
            var problem = mapper.Map(httpContext, _errors);

            await writer.WriteAsync(httpContext, problem, httpContext.RequestAborted);
        }

        private static Error[] Normalize(IEnumerable<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var normalized = errors.ToArray();
            if (normalized.Any(error => error is null))
            {
                throw new ArgumentException("ProblemDetails errors cannot contain null values.", nameof(errors));
            }

            return normalized;
        }
    }
}
