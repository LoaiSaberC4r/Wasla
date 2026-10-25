using Wasla.Application.Features.PatientOnboarding;
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
        services.AddTransient<MediatR.IPipelineBehavior<FamilyMedicalCommand, BuildingBlock.Domain.Results.Result<object>>, FamilyMedicalMediaCompensationBehavior>();
        services.AddScoped<PatientIdentityWorkflow>();
        services.AddScoped<Wasla.Application.Features.PatientIdentityMatching.PatientIdentityReuseGuard>();
        services.AddOptions<PatientIdentityOptions>();
        services.AddTransient<MediatR.IPipelineBehavior<PatientIdentityCommand, BuildingBlock.Domain.Results.Result<object>>, PatientIdentityMediaBehavior<PatientIdentityCommand, object>>();
        services.AddTransient<MediatR.IPipelineBehavior<Wasla.Application.Features.Registration.RegisterPatientCommand, BuildingBlock.Domain.Results.Result<Wasla.Application.Features.Registration.RegisterPatientResponse>>, PatientIdentityMediaBehavior<Wasla.Application.Features.Registration.RegisterPatientCommand, Wasla.Application.Features.Registration.RegisterPatientResponse>>();
        services.AddTransient<MediatR.IPipelineBehavior<Wasla.Application.Features.Patients.CreateReceptionPatientCommand, BuildingBlock.Domain.Results.Result<Wasla.Application.Features.Patients.CreateReceptionPatientResponse>>, PatientIdentityMediaBehavior<Wasla.Application.Features.Patients.CreateReceptionPatientCommand, Wasla.Application.Features.Patients.CreateReceptionPatientResponse>>();
        services.AddTransient<MediatR.IPipelineBehavior<Wasla.Application.Features.Patients.UpdateMyPatientProfileCommand, BuildingBlock.Domain.Results.Result<Wasla.Application.Features.Patients.PatientProfileResponse>>, PatientIdentityMediaBehavior<Wasla.Application.Features.Patients.UpdateMyPatientProfileCommand, Wasla.Application.Features.Patients.PatientProfileResponse>>();
        services.AddBuildingBlockApplicationBehaviors();
        services.AddScoped<DoctorLifecycleService>();
        services.AddScoped<Wasla.Application.Features.Dashboards.PatientDashboardComposer>();
        services.AddScoped<FamilyRelationshipWorkflowService>();
        services.AddScoped<IReceptionPracticeAuthorizationService, ReceptionPracticeAuthorizationService>();
        services.AddScoped<ReservationApplicationService>();
        services.AddOptions<Wasla.Application.Features.Notifications.NotificationOptions>();
        services.AddScoped<ReservationScheduleGuard>();
        services.AddScoped<TicketAccessService>();
        services.AddScoped<FollowUpWorkflow>();
        services.AddScoped<MedicalCatalogManagerAccounts>();
        services.AddScoped<DiagnosticAccess>();
        services.AddScoped<DrugCatalogManagerAccounts>();
        services.AddScoped<MedicationAccess>();
        services.AddScoped<MedicationIdempotency>();
        services.AddScoped<IMedicalRecordAccessPolicy, MedicalRecordAccessPolicy>();
        services.AddScoped<ClinicalAccessService>();
        services.AddScoped<ClinicalMutationService>();
        services.AddScoped<ReservationCheckInWorkflow>();
        services.AddScoped<Wasla.Application.Features.Archives.ArchiveTicketReplay>();
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
