using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Common.Validation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;

namespace Wasla.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWaslaApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidatorOptions.Global.LanguageManager = new ErrorMessageLanguageManager();

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly));
        services.AddValidatorsFromAssembly(
            AssemblyReference.Assembly,
            includeInternalTypes: true);
        services.AddBuildingBlockApplicationBehaviors();
        services.AddScoped<DoctorLifecycleService>();
        services.AddSingleton<IEmailNotificationFactory, BilingualEmailNotificationFactory>();

        return services;
    }
}
