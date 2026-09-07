using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.Options;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.QrCode;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Application.Bootstrap;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Media;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Repositories;
using BuildingBlock.Tests.Integration.Persistence;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BuildingBlock.Tests;

public sealed class Phase5DependencyInjectionSmokeTests
{
    [Fact]
    public void Application_pipeline_registrations_resolve_in_documented_order()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockApplicationBehaviors();

        using var provider = BuildValidatedProvider(services);
        using var scope = provider.CreateScope();

        var behaviors = scope.ServiceProvider
            .GetRequiredService<IEnumerable<IPipelineBehavior<SmokeCommand, Result>>>()
            .Select(behavior => behavior.GetType().GetGenericTypeDefinition())
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(TracingBehavior<,>),
                typeof(LoggingBehavior<,>),
                typeof(ExceptionMappingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(CommandCacheInvalidationBehavior<,>),
                typeof(TransactionBehavior<,>),
                typeof(QueryCacheBehavior<,>)
            },
            behaviors);
    }

    [Fact]
    public void Ef_infrastructure_registrations_resolve_closed_repositories_units_of_work_and_interceptors()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(connection);
        services.AddDbContext<TestWriteDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddDbContext<TestDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddDbContext<TestReadDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddBuildingBlockDbContext<TestWriteMarker, TestWriteDbContext>();
        services.AddBuildingBlockDbContext<TestReadMarker, TestDbContext>();
        services.AddBuildingBlockEntityFrameworkCore();
        services.AddBuildingBlockInterceptors();

        using var provider = BuildValidatedProvider(services);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReadRepository<TestAggregate, TestReadMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWriteRepository<TestAggregate, TestWriteMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISoftDeletedReadRepository<TestAggregate, TestReadMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISoftDeletedWriteRepository<TestAggregate, TestWriteMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork<TestWriteMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReadModelWriter<TestReadModel, TestReadMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<TestReadMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IApplicationTransactionManager<TestWriteMarker>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DomainEventsInterceptor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SoftDeleteEntitiesInterceptor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AuditableEntitiesInterceptor>());
    }

    [Fact]
    public void Security_registrations_resolve_independently_and_invalid_encryption_fails()
    {
        var configuration = ConfigurationFor(
            ("Encryption:CurrentKeyId", "main"),
            ("Encryption:Keys:main", Key(7)));
        var services = new ServiceCollection();
        services.AddBuildingBlockTokenReader(configuration);
        services.AddBuildingBlockPasswordHashing(configuration);
        services.AddBuildingBlockEncryption(configuration);
        services.AddBuildingBlockCurrentUser();

        using var provider = BuildValidatedProvider(services);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITokenReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPasswordService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEncryptionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICurrentUser>());

        var invalidServices = new ServiceCollection();
        invalidServices.AddBuildingBlockEncryption(ConfigurationFor());
        using var invalidProvider = BuildValidatedProvider(invalidServices);

        Assert.Throws<OptionsValidationException>(() =>
            invalidProvider.GetRequiredService<IEncryptionService>());
    }

    [Fact]
    public void Api_registrations_resolve_problem_details_localization_openapi_logging_and_current_user()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddRouting();
        services.AddBuildingBlockProblemDetails();
        services.AddBuildingBlockLocalization(options =>
        {
            options.DefaultCulture = "en";
            options.SupportedCultures = new[] { "en", "ar" };
        });
        services.AddBuildingBlockSwagger();
        services.AddSingleton<IApiVersionDescriptionProvider>(new EmptyApiVersionDescriptionProvider());
        services.AddBuildingBlockLogRedaction();
        services.AddBuildingBlockTokenReader();
        services.AddOptions<RequestLoggingOptions>()
            .Validate(options => options.MaximumValueLength > 32, "Request logging options are invalid.")
            .ValidateOnStart();
        services.AddBuildingBlockCurrentUser();

        using var provider = BuildValidatedProvider(services);
        using var scope = provider.CreateScope();

        Assert.NotNull(provider.GetRequiredService<IProblemDetailsMapper>());
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.Name == "IAuthorizationMiddlewareResultHandler" &&
            descriptor.ImplementationType == typeof(BuildingBlockAuthorizationMiddlewareResultHandler));
        Assert.NotNull(provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
        Assert.NotNull(provider.GetRequiredService<IConfigureOptions<SwaggerGenOptions>>());
        Assert.NotNull(provider.GetRequiredService<ILogRedactionPolicy>());
        Assert.NotNull(provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
    }

    [Fact]
    public void Repeated_registration_is_idempotent_and_preserves_consumer_overrides()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddSingleton<ICacheService, ConsumerCacheService>();

        services.AddBuildingBlockApplicationBehaviors();
        services.AddBuildingBlockApplicationBehaviors();
        services.AddBuildingBlockEntityFrameworkCore();
        services.AddBuildingBlockEntityFrameworkCore();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockCaching();

        Assert.Equal(7, services.Count(descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>)));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DomainEventsInterceptor));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(SoftDeleteEntitiesInterceptor));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AuditableEntitiesInterceptor));

        using var provider = BuildValidatedProvider(services);
        Assert.IsType<ConsumerCacheService>(provider.GetRequiredService<ICacheService>());
    }

    [Fact]
    public void External_service_registrations_resolve_with_valid_options_and_reject_invalid_options()
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "buildingblock-phase5", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentUser, FakeCurrentUser>();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockMedia(options =>
        {
            options.RootPath = mediaRoot;
            options.AllowedExtensions = new[] { ".png" };
            options.AllowedMimeTypes = new[] { "image/png" };
        });
        services.AddBuildingBlockFileSystemMediaStorage();
        services.AddBuildingBlockMailKitEmail(options =>
        {
            options.Host = "localhost";
            options.Port = 25;
            options.RequireStartTls = false;
            options.FromEmail = "noreply@example.test";
            options.FromName = "BuildingBlock";
        });
        services.AddBuildingBlockQrCode(options =>
        {
            options.MaximumPayloadBytes = 512;
            options.MinimumPixelsPerModule = 1;
            options.MaximumPixelsPerModule = 20;
        });

        using var provider = BuildValidatedProvider(services);
        using var scope = provider.CreateScope();

        Assert.NotNull(provider.GetRequiredService<ICacheService>());
        Assert.NotNull(provider.GetRequiredService<IMalwareScanner>());
        Assert.NotNull(provider.GetRequiredService<IMediaUploadValidator>());
        Assert.NotNull(provider.GetRequiredService<IMediaStorage>());
        Assert.NotNull(provider.GetRequiredService<IMediaService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmailSender>());
        Assert.NotNull(provider.GetRequiredService<IQRCodeService>());

        var invalidServices = new ServiceCollection();
        invalidServices.AddLogging();
        invalidServices.AddBuildingBlockMedia(options =>
        {
            options.RootPath = "";
            options.AllowedExtensions = Array.Empty<string>();
            options.AllowedMimeTypes = Array.Empty<string>();
        });
        invalidServices.AddBuildingBlockFileSystemMediaStorage();
        using var invalidProvider = BuildValidatedProvider(invalidServices);

        Assert.Throws<OptionsValidationException>(() =>
            invalidProvider.GetRequiredService<IOptions<MediaStorageOptions>>().Value);
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services)
        => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

    private static IConfiguration ConfigurationFor(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static string Key(byte seed)
        => Convert.ToBase64String(Enumerable.Range(0, 32).Select(offset => (byte)(seed + offset)).ToArray());

    private sealed record SmokeCommand : ICommand;

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public string? UserName => "phase5";
        public string? Email => "phase5@example.test";
        public IReadOnlyCollection<string> Roles { get; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions { get; } = Array.Empty<string>();
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => Array.Empty<string>();
    }

    private sealed class ConsumerCacheService : ICacheService
    {
        public Task<(bool found, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult((false, default(T)));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyApiVersionDescriptionProvider : IApiVersionDescriptionProvider
    {
        public IReadOnlyList<ApiVersionDescription> ApiVersionDescriptions { get; } =
            new[] { new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false) };
    }
}
