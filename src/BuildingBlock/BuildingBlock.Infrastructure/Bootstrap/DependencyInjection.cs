using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.QrCode;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Exceptions;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Infrastructure.DomainEvents;
using BuildingBlock.Infrastructure.Email;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Media;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Repositories;
using BuildingBlock.Infrastructure.Service;
using BuildingBlock.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Infrastructure.Bootstrap
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBuildingBlockEntityFrameworkCore(this IServiceCollection services)
        {
            services.TryAddScoped(typeof(IReadRepository<,>), typeof(EfReadRepository<,>));
            services.TryAddScoped(typeof(IWriteRepository<,>), typeof(EfWriteRepository<,>));
            services.TryAddScoped(typeof(ISoftDeletedReadRepository<,>), typeof(EfSoftDeletedReadRepository<,>));
            services.TryAddScoped(typeof(ISoftDeletedWriteRepository<,>), typeof(EfSoftDeletedWriteRepository<,>));
            services.TryAddScoped(typeof(IUnitOfWork<>), typeof(EfUnitOfWork<>));
            services.TryAddScoped(typeof(IReadModelWriter<,>), typeof(EfReadModelWriter<,>));
            services.TryAddScoped(typeof(IReadModelUnitOfWork<>), typeof(EfReadModelUnitOfWork<>));
            services.TryAddScoped(typeof(IApplicationTransactionManager<>), typeof(EfApplicationTransactionManager<>));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, EfCoreExceptionToErrorMapper>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            return services;
        }

        public static IServiceCollection AddBuildingBlockEntityFrameworkCore<TWriteMarker>(this IServiceCollection services)
            where TWriteMarker : IWritePersistenceMarker
        {
            services.AddBuildingBlockEntityFrameworkCore();
            services.Replace(ServiceDescriptor.Scoped<IApplicationTransactionManager<TWriteMarker>, EfApplicationTransactionManager<TWriteMarker>>());
            return services;
        }

        public static IServiceCollection AddBuildingBlockCaching(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.TryAddSingleton<KeyedSemaphore>();
            services.TryAddSingleton<ICacheService, MemoryCacheService>();
            services.TryAddScoped<ICacheScopeValueProvider, DefaultCacheScopeValueProvider>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockInterceptors(this IServiceCollection services)
        {
            services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.TryAddScoped<DomainEventsInterceptor>();
            services.TryAddScoped<SoftDeleteEntitiesInterceptor>();
            services.TryAddScoped<AuditableEntitiesInterceptor>();
            return services;
        }

        public static DbContextOptionsBuilder UseBuildingBlockInterceptors(
            this DbContextOptionsBuilder options,
            IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            return options.AddInterceptors(
                serviceProvider.GetRequiredService<DomainEventsInterceptor>(),
                serviceProvider.GetRequiredService<SoftDeleteEntitiesInterceptor>(),
                serviceProvider.GetRequiredService<AuditableEntitiesInterceptor>());
        }

        public static IServiceCollection AddBuildingBlockAuditing(this IServiceCollection services)
        {
            services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.TryAddScoped<AuditableEntitiesInterceptor>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockSoftDelete(this IServiceCollection services)
        {
            services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
            services.TryAddScoped<SoftDeleteEntitiesInterceptor>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockDomainEvent(this IServiceCollection services)
        {
            services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.TryAddScoped<DomainEventsInterceptor>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockDbContext<TMarker, TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            services.TryAddScoped<IEfDbContextResolver<TMarker>, EfDbContextResolver<TMarker, TContext>>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockMailKitEmail(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection("Smtp"))
                .Validate(ValidateSmtpOptions, "SMTP options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            services.TryAddScoped<IEmailSender, MailKitEmailSender>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockMailKitEmail(
            this IServiceCollection services,
            Action<SmtpOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<SmtpOptions>()
                .Configure(configure)
                .Validate(ValidateSmtpOptions, "SMTP options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            services.TryAddScoped<IEmailSender, MailKitEmailSender>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockTokenReader(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            var optionsBuilder = services.AddOptions<CurrentUserClaimOptions>();
            if (configuration is not null)
            {
                optionsBuilder.Bind(configuration.GetSection(CurrentUserClaimOptions.SectionName));
            }

            return services.AddBuildingBlockTokenReaderCore(optionsBuilder);
        }

        public static IServiceCollection AddBuildingBlockTokenReader(
            this IServiceCollection services,
            Action<CurrentUserClaimOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var optionsBuilder = services.AddOptions<CurrentUserClaimOptions>().Configure(configure);
            return services.AddBuildingBlockTokenReaderCore(optionsBuilder);
        }

        public static IServiceCollection AddBuildingBlockPasswordHashing(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            var optionsBuilder = services.AddOptions<PasswordPolicyOptions>();
            if (configuration is not null)
            {
                optionsBuilder.Bind(configuration.GetSection(PasswordPolicyOptions.SectionName));
            }

            return services.AddBuildingBlockPasswordHashingCore(optionsBuilder);
        }

        public static IServiceCollection AddBuildingBlockPasswordHashing(
            this IServiceCollection services,
            Action<PasswordPolicyOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var optionsBuilder = services.AddOptions<PasswordPolicyOptions>().Configure(configure);
            return services.AddBuildingBlockPasswordHashingCore(optionsBuilder);
        }

        public static IServiceCollection AddBuildingBlockEncryption(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<EncryptionOptions>()
                .Bind(configuration.GetSection(EncryptionOptions.SectionName))
                .Validate(EncryptionOptionsValidator.IsValid, EncryptionOptionsValidator.ValidationMessage)
                .ValidateOnStart();

            services.TryAddSingleton<IEncryptionService, EncryptionService>();
            return services;
        }

        private static IServiceCollection AddBuildingBlockTokenReaderCore(
            this IServiceCollection services,
            OptionsBuilder<CurrentUserClaimOptions> optionsBuilder)
        {
            optionsBuilder
                .Validate(ValidateCurrentUserClaims, "Current user claim options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<ITokenReader, TokenReader>();
            return services;
        }

        private static IServiceCollection AddBuildingBlockPasswordHashingCore(
            this IServiceCollection services,
            OptionsBuilder<PasswordPolicyOptions> optionsBuilder)
        {
            optionsBuilder
                .Validate(ValidatePasswordPolicy, "Password policy options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<IPasswordService, PasswordService>();
            return services;
        }

        private static bool ValidateCurrentUserClaims(CurrentUserClaimOptions options)
            => !string.IsNullOrWhiteSpace(options.UserIdClaimType) &&
               !string.IsNullOrWhiteSpace(options.UserNameClaimType) &&
               !string.IsNullOrWhiteSpace(options.EmailClaimType) &&
               !string.IsNullOrWhiteSpace(options.RoleClaimType) &&
               !string.IsNullOrWhiteSpace(options.PermissionClaimType) &&
               options.MaximumClaimValueLength is > 0 and <= 16_384 &&
               options.MaximumRoles is > 0 and <= 1024 &&
               options.MaximumPermissions is > 0 and <= 4096;

        private static bool ValidatePasswordPolicy(PasswordPolicyOptions options)
            => options.RequiredLength > 0 &&
               options.MaximumLength >= options.RequiredLength &&
               options.MaximumLength <= 4096;

        public static IServiceCollection AddBuildingBlockMedia(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            var optionsBuilder = services.AddOptions<MediaStorageOptions>();
            if (configuration is not null)
            {
                optionsBuilder.Bind(configuration.GetSection("MediaStorage"));
            }

            optionsBuilder
                .Validate(ValidateMediaStorageOptions, "Media storage options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<IMalwareScanner, NoOpMalwareScanner>();
            services.TryAddSingleton<IMediaUploadValidator, MediaUploadValidator>();
            services.TryAddSingleton<IMediaService, MediaService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            return services;
        }

        public static IServiceCollection AddBuildingBlockMedia(
            this IServiceCollection services,
            Action<MediaStorageOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<MediaStorageOptions>()
                .Configure(configure)
                .Validate(ValidateMediaStorageOptions, "Media storage options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<IMalwareScanner, NoOpMalwareScanner>();
            services.TryAddSingleton<IMediaUploadValidator, MediaUploadValidator>();
            services.TryAddSingleton<IMediaService, MediaService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            return services;
        }

        public static IServiceCollection AddBuildingBlockFileSystemMediaStorage(this IServiceCollection services)
        {
            services.TryAddSingleton<IMediaStorage, FileSystemMediaStorage>();
            return services;
        }

        public static IServiceCollection AddBuildingBlockQrCode(this IServiceCollection services)
        {
            services.AddOptions<QrCodeOptions>()
                .Validate(ValidateQrCodeOptions, "QR code options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<IQRCodeService, QRCodeService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            return services;
        }

        public static IServiceCollection AddBuildingBlockQrCode(
            this IServiceCollection services,
            Action<QrCodeOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<QrCodeOptions>()
                .Configure(configure)
                .Validate(ValidateQrCodeOptions, "QR code options are invalid.")
                .ValidateOnStart();

            services.TryAddSingleton<IQRCodeService, QRCodeService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, ExternalServiceExceptionToErrorMapper>());
            return services;
        }

        private static bool ValidateMediaStorageOptions(MediaStorageOptions options)
            => !string.IsNullOrWhiteSpace(options.RootPath) &&
               options.MaxFileSizeBytes > 0 &&
               options.AllowedExtensions.Length > 0 &&
               options.AllowedExtensions.All(extension =>
                   !string.IsNullOrWhiteSpace(extension) &&
                   extension.StartsWith('.') &&
                   !extension.Contains('/', StringComparison.Ordinal) &&
                   !extension.Contains('\\', StringComparison.Ordinal)) &&
               options.AllowedMimeTypes.Length > 0 &&
               options.AllowedMimeTypes.All(mime => !string.IsNullOrWhiteSpace(mime));

        private static bool ValidateSmtpOptions(SmtpOptions options)
            => !string.IsNullOrWhiteSpace(options.Host) &&
               options.Port is > 0 and <= 65535 &&
               !string.IsNullOrWhiteSpace(options.FromEmail) &&
               options.FromEmail.Contains('@', StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(options.FromName) &&
               options.MaxAttachmentBytes > 0 &&
               options.MaxTotalAttachmentBytes >= options.MaxAttachmentBytes &&
               options.MaxRecipients > 0 &&
               options.MaxAttachments >= 0 &&
               options.TimeoutMilliseconds > 0;

        private static bool ValidateQrCodeOptions(QrCodeOptions options)
            => options.MaximumPayloadBytes > 0 &&
               options.MinimumPixelsPerModule > 0 &&
               options.MaximumPixelsPerModule >= options.MinimumPixelsPerModule;
    }
}
