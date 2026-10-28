using Wasla.Application.Features.PatientOnboarding;
using Microsoft.AspNetCore.DataProtection;
using Wasla.Application.Email;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Governance;
using Wasla.Application.Features.Diagnostics;
using Wasla.Application.Persistence;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.GetWalkInOptions;
using Wasla.Application.Features.Finance;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Features.OperatingFinance;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Finance.Common;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlock.Application.Exceptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer;

public static class DependencyInjection
{
    public static IServiceCollection AddWaslaSqlServerPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' is not configured.");
        var migrationsAssemblyName = typeof(WaslaDbContext)
            .Assembly
            .GetName()
            .Name
            ?? throw new InvalidOperationException(
                "Unable to resolve the migrations assembly name.");

        services.Insert(0, ServiceDescriptor.Singleton<IExceptionToErrorMapper, ClinicalPersistenceErrorMapper>());
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionToErrorMapper, Phase18PersistenceErrorMapper>());
        services.AddBuildingBlockEntityFrameworkCore<WaslaWritePersistence>();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockSqlServerExceptionMapping();

        services.AddOptions<DatabaseInitializationOptions>()
            .Bind(configuration.GetSection(DatabaseInitializationOptions.SectionName));
        services.AddOptions<RootSuperAdminOptions>()
            .Bind(configuration.GetSection(RootSuperAdminOptions.SectionName));
        services.AddOptions<EmailOutboxOptions>()
            .Bind(configuration.GetSection(EmailOutboxOptions.SectionName))
            .Validate(
                options => options.PollingIntervalSeconds > 0 &&
                           options.BatchSize is > 0 and <= 100 &&
                           options.MaxAttempts is > 0 and <= 20 &&
                           options.ClaimLeaseSeconds >= 120,
                "Email outbox options are invalid.")
            .ValidateOnStart();
        services.AddOptions<PublicDiscoveryProjectionOptions>()
            .Bind(configuration.GetSection(PublicDiscoveryProjectionOptions.SectionName))
            .Validate(
                options => options.RefreshIntervalHours is >= 1 and <= 168,
                "Public discovery projection options are invalid.")
            .ValidateOnStart();

        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddOptions<Wasla.Application.Features.Notifications.NotificationOptions>().Bind(configuration.GetSection("Notifications"))
            .Validate(o => o.PollingIntervalSeconds is >= 1 and <= 3600 && o.BatchSize is >= 1 and <= 100 &&
                o.ClaimLeaseSeconds is >= 120 and <= 3600 && o.MaxAttempts is >= 1 and <= 20, "Invalid notification worker limits.").ValidateOnStart();
        services.AddOptions<Wasla.Application.Features.Notifications.NotificationPrivacyOptions>().Bind(configuration.GetSection("NotificationPrivacy"));
        services.AddScoped<Notifications.NotificationEligibility>();
        services.AddScoped<Notifications.NotificationEmailGuard>();
        services.AddScoped<Notifications.NotificationDispatchProcessor>();
        services.AddScoped<Wasla.Application.Features.Notifications.INotificationService, Notifications.NotificationService>();
        services.AddScoped<Wasla.Application.Features.Notifications.INotificationAuthorizationAudit, Notifications.NotificationAuthorizationAudit>();
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionToErrorMapper, Notifications.NotificationPersistenceErrorMapper>());
        services.AddScoped<IWaslaDataStore, WaslaDataStore>();
        services.AddScoped<IPatientIdentityStore, PatientIdentityStore>();
        services.AddScoped<PatientIdentityRetention>();
        services.AddHostedService<PatientIdentityRetentionBackgroundService>();
        services.AddScoped<IPatientIdentityMediaCompensation, PatientIdentityMediaCompensation>();
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionToErrorMapper, PatientIdentityPersistenceErrorMapper>());
        services.AddOptions<PatientIdentityOptions>().Bind(configuration.GetSection("PatientIdentity")).Validate(o => o.IsValid(), "Invalid patient identity options.").ValidateOnStart();
        services.AddScoped<IPublicDiscoveryService, PublicDiscoveryService>();
        services.AddScoped<ReservationPublicReaders>();
        services.AddScoped<IPracticeReservationOccupancyReader>(provider => provider.GetRequiredService<ReservationPublicReaders>());
        services.AddScoped<IPublicDoctorPopularityReader>(provider => provider.GetRequiredService<ReservationPublicReaders>());
        services.AddScoped<IPublicDiscoveryRankingProjectionRefresher,
            PublicDiscoveryRankingProjectionRefresher>();
        services.AddScoped<IReservationProjectionInvalidationOutbox,
            ReservationProjectionInvalidationOutbox>();
        services.AddScoped<ITicketQueueLock, TicketQueueLock>();
        services.AddScoped<ITicketNumberAllocator, TicketNumberAllocator>();
        services.AddScoped<IFinancialNumberAllocator, FinancialNumberAllocator>();
        services.AddScoped<ITicketQueueReader, TicketQueueReader>();
        services.AddScoped<Wasla.Application.Features.Archives.IArchiveLocationResolver, ArchiveLocationResolver>();
        services.AddScoped<ArchiveRegistryPopulation>();
        services.AddScoped<ArchiveOperationalDays>();
        services.AddScoped<ArchiveTokens>();
        services.AddScoped<Wasla.Application.Features.Archives.IArchiveQueries, ArchiveQueries>();
        services.AddScoped<Wasla.Application.Features.Archives.IArchiveOperations, ArchiveOperationsService>();
        services.AddScoped<Wasla.Application.Features.Archives.IAuditExplorer, AuditExplorer>();
        services.AddScoped<Wasla.Application.Features.Archives.IArchiveRequestAudit, ArchiveRequestAudit>();
        services.AddOptions<ArchiveOptions>().Bind(configuration.GetSection("Archive"))
            .Validate(x => x.BatchSize is >= 1 and <= 250 && x.IntervalSeconds is >= 30 and <= 3600, "Invalid archive limits.").ValidateOnStart();
        services.AddScoped<ArchiveEngine>();
        services.AddScoped<IWalkInOptionsReader, WalkInOptionsReader>();
        services.AddScoped<IFinanceReadService, FinanceReadService>();
        services.AddOptions<Phase18Options>().Bind(configuration.GetSection("Phase18"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DefaultReportTimeZoneId) &&
                TimeZoneInfo.TryFindSystemTimeZoneById(o.DefaultReportTimeZoneId, out _) &&
                o.ObligationGenerationIntervalSeconds is >= 30 and <= 86400 &&
                o.ReportingPolicyRefreshIntervalSeconds is >= 30 and <= 86400 &&
                o.PdfTimeoutSeconds is >= 5 and <= 120 && o.MaximumConcurrentPdfRenders is >= 1 and <= 4,
                "Invalid Phase 18 reporting configuration.").ValidateOnStart();
        services.AddScoped<IReportingAccessGuard, ReportingAccessGuard>();
        services.AddScoped<IReportingTimeScope, ReportingTimeScope>();
        services.AddScoped<ReportingQueryService>();
        services.AddScoped<IReportingQueryService>(p => p.GetRequiredService<ReportingQueryService>());
        services.AddScoped<IInsightEvidenceReader>(p => p.GetRequiredService<ReportingQueryService>());
        services.AddScoped<IDoctorInsightService, DoctorInsightService>();
        services.AddScoped<IHandoverService, HandoverService>();
        services.AddScoped<IInvestigationService, InvestigationService>();
        services.AddScoped<IPlatformOperationsService, PlatformOperationsService>();
        services.AddSingleton<IReportDocumentRenderer, Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports.ReportDocumentRenderer>();
        services.AddScoped<IReportExportService, Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports.ReportExportService>();
        services.AddScoped<IReceptionOperationsService, ReceptionOperationsService>();
        services.AddSingleton<IKpiDefinitionRegistry, KpiDefinitionRegistry>();
        services.AddScoped<IOperatingFinanceService, OperatingFinanceService>();
        services.AddScoped<IOperatingObligationGenerator, OperatingObligationGenerator>();
        services.AddScoped<MedicationReadService>();
        services.AddScoped<IMedicationReadService>(provider => provider.GetRequiredService<MedicationReadService>());
        services.AddScoped<IDrugCatalogManagerReader>(provider => provider.GetRequiredService<MedicationReadService>());
        services.AddScoped<IDrugCatalogImportService, DrugCatalogImportService>();
        services.AddScoped<DiagnosticReadService>();
        services.AddScoped<IDiagnosticReadService>(p => p.GetRequiredService<DiagnosticReadService>());
        services.AddScoped<IDiagnosticCoverageReader>(p => p.GetRequiredService<DiagnosticReadService>());
        services.AddScoped<IDiagnosticMediaReferenceReader>(p => p.GetRequiredService<DiagnosticReadService>());
        services.AddScoped<IMedicalCatalogManagerReader>(p => p.GetRequiredService<DiagnosticReadService>());
        services.AddScoped<IDiagnosticImportService, DiagnosticImportService>();
        services.AddOptions<DiagnosticCatalogImportOptions>().Bind(configuration.GetSection(DiagnosticCatalogImportOptions.SectionName))
            .Validate(o => o.MaxPackageBytes > 0 && o.MaxExpandedBytes >= o.MaxPackageBytes && o.MaxEntries > 0 && o.MaxRows > 0 && o.MaxCompressionRatio > 0 && o.SupportedVersions.Length > 0, "Invalid diagnostic import limits.").ValidateOnStart();
        services.AddOptions<FamilyMedicalPolicyOptions>().Bind(configuration.GetSection("FamilyMedicalPolicy"));
        services.AddScoped<FamilyMedicalAccessPolicy>();
        services.AddScoped<IFamilyMedicalAccessPolicy>(p => p.GetRequiredService<FamilyMedicalAccessPolicy>());
        services.AddScoped<IFamilyMedicalWorkflow, FamilyMedicalWorkflow>();
        services.AddScoped<IFamilyRelationshipMedicalVerification, FamilyRelationshipMedicalVerification>();
        services.AddScoped<IFamilyProofReconciler, FamilyProofReconciler>();
        services.AddScoped<IFamilyMedicalMediaCompensation, FamilyMedicalMediaCompensation>();
        services.AddOptions<MedicalRecordOptions>().Bind(configuration.GetSection("MedicalRecord"));
        services.AddScoped<ICurrentTreatmentReader, CurrentTreatmentReader>();
        services.AddScoped<IHiddenAccessReader, HiddenAccessReader>();
        services.AddHttpContextAccessor();
        services.AddScoped<MedicalRecordAuditWriter>();
        services.AddScoped<MedicalRecordReadService>();
        services.AddScoped<IMedicalRecordReadService>(p => p.GetRequiredService<MedicalRecordReadService>());
        services.AddScoped<IMedicalRecordMediaReader>(p => p.GetRequiredService<MedicalRecordReadService>());
        var protection = services.AddDataProtection().SetApplicationName("Wasla.Phase16");
        if (configuration["MedicalRecord:DataProtectionKeyRingPath"] is { Length: > 0 } keyPath)
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        services.AddSingleton<IMedicalRecordTokens, MedicalRecordTokens>();
        services.AddScoped<IEncounterVisibilityReader, EncounterVisibilityReader>();
        services.AddScoped<IClinicalReadService, ClinicalReadService>();
        services.AddScoped<Wasla.Application.Features.DoctorPatients.IDoctorPatientReadService, DoctorPatientReadService>();
        services.AddSingleton<Wasla.Application.Features.DoctorPatients.IPrescriptionPdfRenderer, Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports.PrescriptionPdfRenderer>();
        services.AddScoped<IReservationNoShowRuntimeReader, ReservationNoShowRuntimeReader>();
        services.AddScoped<WaslaSecuritySeeder>();
        services.AddScoped<MedicalSpecializationSeeder>();
        services.AddScoped<EgyptLocationSeedCoordinator>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddScoped<ReservationProjectionInvalidationProcessor>();
        services.AddSingleton<IDatabaseMigrationService, EfCoreDatabaseMigrationService>();
        services.AddHostedService<DatabaseInitializationHostedService>();
        services.AddHostedService<PublicDiscoveryProjectionMaintenanceHostedService>();
        services.AddHostedService<EmailOutboxBackgroundService>();
        services.AddHostedService<Notifications.NotificationDispatchBackgroundService>();
        services.AddHostedService<ReservationExpirationBackgroundService>();
        services.AddHostedService<ReservationProjectionInvalidationBackgroundService>();
        services.AddHostedService<TicketOperationalDayBackgroundService>();
        services.AddHostedService<ArchiveBackgroundService>();
        services.AddHostedService<OperatingObligationBackgroundService>();
        services.AddHostedService<ReportingPolicyReconciler>();
        services.AddHostedService<Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports.ExportRuntimeProbeHostedService>();

        services.AddDbContext<WaslaDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssemblyName))
                .UseBuildingBlockInterceptors(serviceProvider));

        services.AddBuildingBlockDbContext<WaslaReadPersistence, WaslaDbContext>();
        services.AddBuildingBlockDbContext<WaslaWritePersistence, WaslaDbContext>();

        return services;
    }
}
