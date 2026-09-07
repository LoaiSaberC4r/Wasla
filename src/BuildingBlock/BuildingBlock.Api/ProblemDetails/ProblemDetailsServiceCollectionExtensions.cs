using BuildingBlock.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Api.ProblemDetails
{
    public static class ProblemDetailsServiceCollectionExtensions
    {
        public static IServiceCollection AddBuildingBlockProblemDetails(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddProblemDetails();
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
                options.SerializerOptions.TypeInfoResolverChain.Insert(
                    0,
                    BuildingBlockProblemDetailsJsonContext.Default));
            services.TryAddSingleton<IProblemDetailsMapper, ProblemDetailsMapper>();
            services.TryAddSingleton<IBuildingBlockProblemDetailsWriter, BuildingBlockProblemDetailsWriter>();
            services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, BuildingBlockAuthorizationMiddlewareResultHandler>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<
                IConfigureOptions<ApiBehaviorOptions>,
                ConfigureBuildingBlockApiBehaviorOptions>());

            return services;
        }

        public static IApplicationBuilder UseBuildingBlockProblemDetailsStatusCodes(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);
            return app.UseMiddleware<ProblemDetailsStatusCodeMiddleware>();
        }
    }
}
