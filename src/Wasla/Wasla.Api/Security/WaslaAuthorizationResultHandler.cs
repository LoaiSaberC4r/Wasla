using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Wasla.Domain.Resources;

namespace Wasla.Api.Security;

internal sealed class WaslaAuthorizationResultHandler(IProblemDetailsMapper mapper)
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var error = authorizeResult.Challenged
            ? Error.Unauthorized("Common.Unauthorized", ErrorMessage.AuthenticationRequired, source: "Authorization")
            : Error.Security("Common.Forbidden", ErrorMessage.AccessForbidden, source: "Authorization");
        if (authorizeResult.Challenged)
        {
            await ChallengeAsync(context, policy);
        }
        else
        {
            await ForbidAsync(context, policy);
        }

        if (context.Response.HasStarted || context.Response.ContentLength is > 0)
        {
            return;
        }

        context.Response.StatusCode = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            mapper.Map(context, [error]),
            cancellationToken: context.RequestAborted);
    }

    private static async Task ChallengeAsync(HttpContext context, AuthorizationPolicy policy)
    {
        if (policy.AuthenticationSchemes.Count == 0)
        {
            await context.ChallengeAsync();
            return;
        }

        foreach (var scheme in policy.AuthenticationSchemes)
        {
            await context.ChallengeAsync(scheme);
        }
    }

    private static async Task ForbidAsync(HttpContext context, AuthorizationPolicy policy)
    {
        if (policy.AuthenticationSchemes.Count == 0)
        {
            await context.ForbidAsync();
            return;
        }

        foreach (var scheme in policy.AuthenticationSchemes)
        {
            await context.ForbidAsync(scheme);
        }
    }
}
