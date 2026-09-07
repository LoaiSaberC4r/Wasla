using BuildingBlock.Application.Abstraction.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlock.Api.Security
{
    public static class SecurityServiceCollectionExtensions
    {
        public static IServiceCollection AddBuildingBlockCurrentUser(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
            return services;
        }
    }
}
