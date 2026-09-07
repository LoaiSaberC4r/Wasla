using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlock.Api.ProblemDetails
{
    public interface IProblemDetailsMapper
    {
        Microsoft.AspNetCore.Mvc.ProblemDetails Map(HttpContext httpContext, IReadOnlyCollection<Error> errors);

        Microsoft.AspNetCore.Mvc.ProblemDetails MapException(HttpContext httpContext, Exception exception);
    }
}
