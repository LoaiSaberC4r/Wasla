using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class BuildingBlockAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly IProblemDetailsMapper _mapper;

        public BuildingBlockAuthorizationMiddlewareResultHandler(IProblemDetailsMapper mapper)
        {
            _mapper = mapper;
        }

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

            Error? error = null;
            if (authorizeResult.Challenged)
            {
                await ChallengeAsync(context, policy);
                error = Error.Unauthorized(
                    ErrorCodes.Common.Unauthorized,
                    "Authentication is required.",
                    source: "Authorization");
            }
            else if (authorizeResult.Forbidden)
            {
                await ForbidAsync(context, policy);
                error = Error.Security(
                    ErrorCodes.Common.Forbidden,
                    "Access is forbidden.",
                    source: "Authorization");
            }

            if (error is null || context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = authorizeResult.Challenged
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;

            if (context.Response.ContentLength is > 0 ||
                !string.IsNullOrWhiteSpace(context.Response.ContentType))
            {
                return;
            }

            var writer = context.RequestServices.GetRequiredService<IBuildingBlockProblemDetailsWriter>();
            var problem = _mapper.Map(context, new[] { error });
            await writer.WriteAsync(context, problem, context.RequestAborted);
        }

        private static async Task ChallengeAsync(HttpContext context, AuthorizationPolicy policy)
        {
            if (policy.AuthenticationSchemes.Count > 0)
            {
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    await context.ChallengeAsync(scheme);
                }

                return;
            }

            await context.ChallengeAsync();
        }

        private static async Task ForbidAsync(HttpContext context, AuthorizationPolicy policy)
        {
            if (policy.AuthenticationSchemes.Count > 0)
            {
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    await context.ForbidAsync(scheme);
                }

                return;
            }

            await context.ForbidAsync();
        }
    }
}
