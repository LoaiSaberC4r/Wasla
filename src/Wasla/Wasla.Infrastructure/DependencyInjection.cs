using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWaslaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBuildingBlockCaching();
        services.AddBuildingBlockMailKitEmail(configuration);

        return services;
    }
}
