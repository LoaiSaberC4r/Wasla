using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wasla.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWaslaApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(
            AssemblyReference.Assembly,
            includeInternalTypes: true);
        services.AddBuildingBlockApplicationBehaviors();

        return services;
    }
}
