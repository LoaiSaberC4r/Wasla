using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class BuildingBlockProblemDetailsWriter : IBuildingBlockProblemDetailsWriter
    {
        private readonly IProblemDetailsService _problemDetailsService;

        public BuildingBlockProblemDetailsWriter(IProblemDetailsService problemDetailsService)
        {
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask WriteAsync(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(problemDetails);

            if (httpContext.Response.HasStarted)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            httpContext.RequestAborted.ThrowIfCancellationRequested();

            if (problemDetails.Status is { } status)
            {
                httpContext.Response.StatusCode = status;
            }

            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });
        }
    }
}
