using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Api
{
    public static class ProblemDetailsMapping
    {
        public static IResult ToIResult<T>(this Result<T> result, HttpContext http)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(http);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : new BuildingBlockProblemHttpResult(result.Errors);
        }

        public static IResult ToIResult(this Result result, HttpContext http)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(http);

            return result.IsSuccess
                ? Results.NoContent()
                : new BuildingBlockProblemHttpResult(result.Errors);
        }

        public static IResult ToProblem(this IEnumerable<Error> errors, HttpContext http)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(http);

            return new BuildingBlockProblemHttpResult(errors);
        }
    }
}
