using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Api.ProblemDetails
{
    internal interface IBuildingBlockProblemDetailsWriter
    {
        ValueTask WriteAsync(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails,
            CancellationToken cancellationToken = default);
    }
}
