using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Wasla.Application.Features.Archives;
using BuildingBlock.Api.ProblemDetails;

namespace Wasla.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ArchiveDeniedRequestAttribute() : TypeFilterAttribute(typeof(ArchiveDeniedRequestFilter));

public sealed class ArchiveDeniedRequestFilter(IArchiveRequestAudit audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var completed = await next();
        if (completed.Exception != null) return;
        var status = completed.Result switch
        {
            ObjectResult value => value.StatusCode,
            StatusCodeResult value => value.StatusCode,
            IProblemDetailsErrorResult value => context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsMapper>()
                .Map(context.HttpContext, value.Errors).Status,
            _ => null
        };
        if (status is not (403 or 404 or 409)) return;
        var control = context.ActionDescriptor.AttributeRouteInfo?.Template is { } route &&
            (route.EndsWith("operations/pause", StringComparison.Ordinal) || route.EndsWith("operations/resume", StringComparison.Ordinal));
        await audit.RejectedAsync(control, status.Value, context.HttpContext.RequestAborted);
    }
}
