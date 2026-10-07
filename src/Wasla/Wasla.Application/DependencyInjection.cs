using BuildingBlock.Application.Bootstrap;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Common.Validation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Practices;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Families;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Governance;
using Wasla.Application.Features.Diagnostics;

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
        // Wrap the transaction behavior so compensation observes failures after handler/idempotency saves or commit.
        services.AddTransient<MediatR.IPipelineBehavior<DiagnosticResultCommand, BuildingBlock.Domain.Results.Result<DiagnosticResultMutationResponse>>, DiagnosticMediaCompensationBehavior>();
        services.AddScoped<DiagnosticMediaCompensation>();
        services.AddBuildingBlockApplicationBehaviors();
        services.AddScoped<DoctorLifecycleService>();
        services.AddScoped<FamilyRelationshipWorkflowService>();
        services.AddScoped<IReceptionPracticeAuthorizationService, ReceptionPracticeAuthorizationService>();
        services.AddScoped<ReservationApplicationService>();
        services.AddScoped<ReservationScheduleGuard>();
        services.AddScoped<TicketAccessService>();
        services.AddScoped<FollowUpWorkflow>();
        services.AddScoped<MedicalCatalogManagerAccounts>();
        services.AddScoped<DiagnosticAccess>();
        services.AddScoped<DrugCatalogManagerAccounts>();
        services.AddScoped<MedicationAccess>();
        services.AddScoped<MedicationIdempotency>();
        services.AddScoped<ClinicalAccessService>();
        services.AddScoped<ClinicalMutationService>();
        services.AddScoped<ReservationCheckInWorkflow>();
        services.AddSingleton<IPracticeReservationOccupancyReader, EmptyPracticeReservationOccupancyReader>();
        services.AddSingleton<IPublicDoctorPopularityReader, EmptyPublicDoctorPopularityReader>();
        services.AddSingleton<IPublicDiscoveryRankingProjectionRefresher,
            EmptyPublicDiscoveryRankingProjectionRefresher>();
        services.AddScoped<IReservationNotificationRecipientResolver,
            ReservationNotificationRecipientResolver>();
        services.AddSingleton<IReservationReferenceGenerator, ReservationReferenceGenerator>();
        services.AddSingleton<IPatientAccessPolicy, PatientAccessPolicy>();
        services.AddSingleton<IEmailNotificationFactory, BilingualEmailNotificationFactory>();

        return services;
    }
}
