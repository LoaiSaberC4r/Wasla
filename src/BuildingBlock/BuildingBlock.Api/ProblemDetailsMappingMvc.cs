using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlock.Api
{
    public static class ProblemDetailsMappingMvc
    {
        public static IActionResult ToIActionResult<T>(this Result<T> result, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess
                ? new OkObjectResult(result.Value)
                : result.Errors.ToActionProblem(ct);
        }

        public static IActionResult ToIActionResult(this Result result, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(result);

            return result.IsSuccess
                ? new NoContentResult()
                : result.Errors.ToActionProblem(ct);
        }

        public static IActionResult ToActionProblem(this IEnumerable<Error> errors, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ct.ThrowIfCancellationRequested();
            return new BuildingBlockProblemActionResult(errors);
        }
    }
}
