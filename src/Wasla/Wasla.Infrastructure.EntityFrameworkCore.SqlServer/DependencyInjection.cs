using Wasla.Application.Email;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Governance;
using Wasla.Application.Features.Diagnostics;
using Wasla.Application.Persistence;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Features.Tickets.GetWalkInOptions;
using Wasla.Application.Features.Finance;
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
        services.AddScoped<IWaslaDataStore, WaslaDataStore>();
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
        services.AddScoped<IWalkInOptionsReader, WalkInOptionsReader>();
        services.AddScoped<IFinanceReadService, FinanceReadService>();
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
        services.AddScoped<IClinicalReadService, ClinicalReadService>();
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
        services.AddHostedService<ReservationExpirationBackgroundService>();
        services.AddHostedService<ReservationProjectionInvalidationBackgroundService>();
        services.AddHostedService<TicketOperationalDayBackgroundService>();

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
