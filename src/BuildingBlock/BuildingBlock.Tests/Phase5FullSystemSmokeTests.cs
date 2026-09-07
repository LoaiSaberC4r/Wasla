using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.QrCode;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.Repositories;
using BuildingBlock.Tests.Integration.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BuildingBlock.Tests;

public sealed class Phase5FullSystemSmokeTests : IDisposable
{
    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), $"bb-phase5-smoke-{Guid.NewGuid():N}");

    [Fact]
    public async Task Major_building_block_components_work_together_in_one_generic_workflow()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var services = CreateSmokeServices(database.Connection);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using (var setup = provider.CreateScope())
        {
            var context = setup.ServiceProvider.GetRequiredService<TestWriteDbContext>();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        using var scope = provider.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "phase5-user"),
                new Claim(ClaimTypes.Email, "phase5@example.test"),
                new Claim(ClaimTypes.Role, "Tester"),
                new Claim("permission", "buildingblock:smoke")
            }, "Test"))
        };
        var aggregateId = Guid.NewGuid();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork<TestWriteMarker>>();
        var writeRepository = unitOfWork.WriteRepository<TestAggregate>();
        var aggregate = NewAggregate(aggregateId, "smoke", "Smoke");
        aggregate.RaiseTouchedEvent();

        await writeRepository.AddAsync(aggregate, TestContext.Current.CancellationToken);
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var readRepository = serviceProvider.GetRequiredService<IReadRepository<TestAggregate, TestReadMarker>>();
        var queried = await readRepository.FirstOrDefaultAsync(new SmokeAggregateSpec("Smoke"), TestContext.Current.CancellationToken);
        Assert.Equal("Smoke", queried?.Name);

        var cache = serviceProvider.GetRequiredService<ICacheService>();
        await cache.SetAsync("smoke:aggregate", queried!.Name, TimeSpan.FromMinutes(5), new[] { "smoke" }, TestContext.Current.CancellationToken);
        Assert.True((await cache.TryGetAsync<string>("smoke:aggregate", TestContext.Current.CancellationToken)).found);

        var transactionManager = serviceProvider.GetRequiredService<IApplicationTransactionManager<TestWriteMarker>>();
        await using (var transaction = await transactionManager.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var tracked = await writeRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken);
            tracked!.Name = "Smoke Updated";
            tracked.RaiseTouchedEvent();
            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await cache.InvalidateByTagsAsync(new[] { "smoke" }, TestContext.Current.CancellationToken);
        Assert.False((await cache.TryGetAsync<string>("smoke:aggregate", TestContext.Current.CancellationToken)).found);

        var updated = await readRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        Assert.Equal("Smoke Updated", updated?.Name);
        Assert.True(updated?.EventWasHandled);

        writeRepository.Delete((await writeRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken))!);
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await readRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken));

        var deletedRepository = serviceProvider.GetRequiredService<ISoftDeletedReadRepository<TestAggregate, TestReadMarker>>();
        var deleted = await deletedRepository.GetDeletedByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        Assert.Equal("Smoke Updated", deleted?.Name);

        var deletedWriteRepository = serviceProvider.GetRequiredService<ISoftDeletedWriteRepository<TestAggregate, TestWriteMarker>>();
        var deletedTracked = await deletedWriteRepository.GetDeletedTrackedByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        deletedTracked!.IsDeleted = false;
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await readRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken));

        var problemDetails = serviceProvider.GetRequiredService<IProblemDetailsMapper>()
            .Map(new DefaultHttpContext(), new[]
            {
                Error.Validation("Smoke.Invalid", "Invalid smoke input.", source: "Smoke")
            });
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problemDetails.Status);

        var exceptionProblem = serviceProvider.GetRequiredService<IProblemDetailsMapper>()
            .MapException(new DefaultHttpContext(), new InvalidOperationException("smtp-password=secret"));
        Assert.DoesNotContain("secret", exceptionProblem.Detail, StringComparison.OrdinalIgnoreCase);

        var currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal("phase5-user", currentUser.UserName);

        var qr = serviceProvider.GetRequiredService<IQRCodeService>().Generate(new QrCodeRequest("smoke-payload"));
        Assert.NotEmpty(qr.Content);

        var stored = await serviceProvider.GetRequiredService<IMediaService>().SaveAsync(
            new MediaUpload(new MemoryStream(Png()), "smoke.png", "image/png"),
            new MediaStorageRequest("smoke", "smoke.png"),
            TestContext.Current.CancellationToken);
        Assert.True(File.Exists(Path.Combine(_mediaRoot, stored.Key.Replace('/', Path.DirectorySeparatorChar))));

        var emailSender = (FakeEmailSender)serviceProvider.GetRequiredService<IEmailSender>();
        await emailSender.SendAsync(new EmailMessage
        {
            To = new[] { "recipient@example.test" },
            Subject = "Smoke",
            TextBody = "Smoke body"
        }, TestContext.Current.CancellationToken);
        Assert.Single(emailSender.Sent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    private IServiceCollection CreateSmokeServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(connection);
        services.AddHttpContextAccessor();
        services.AddDbContext<TestWriteDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<SqliteConnection>())
                .UseBuildingBlockInterceptors(provider));
        services.AddBuildingBlockDbContext<TestWriteMarker, TestWriteDbContext>();
        services.AddBuildingBlockDbContext<TestReadMarker, TestWriteDbContext>();
        services.AddScoped<IDomainEventHandler<TestAggregateTouchedEvent>, TouchAggregateHandler>();
        services.AddBuildingBlockEntityFrameworkCore();
        services.AddBuildingBlockInterceptors();
        services.AddBuildingBlockCaching();
        services.AddBuildingBlockTokenReader();
        services.AddBuildingBlockCurrentUser();
        services.AddBuildingBlockProblemDetails();
        services.AddBuildingBlockMedia(options =>
        {
            options.RootPath = _mediaRoot;
            options.AllowedExtensions = new[] { ".png" };
            options.AllowedMimeTypes = new[] { "image/png" };
        });
        services.AddBuildingBlockFileSystemMediaStorage();
        services.AddBuildingBlockQrCode();
        services.AddScoped<IEmailSender, FakeEmailSender>();
        return services;
    }

    private static TestAggregate NewAggregate(Guid id, string externalId, string name)
        => new()
        {
            Id = id,
            ExternalId = externalId,
            Name = name
        };

    private static byte[] Png()
        => new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D
        };

    private sealed class SmokeAggregateSpec : Specification<TestAggregate>
    {
        public SmokeAggregateSpec(string name)
        {
            AddCriteria(entity => entity.Name == name);
        }
    }

    private sealed class TouchAggregateHandler : IDomainEventHandler<TestAggregateTouchedEvent>
    {
        private readonly TestWriteDbContext _context;

        public TouchAggregateHandler(TestWriteDbContext context)
        {
            _context = context;
        }

        public Task Handle(TestAggregateTouchedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var aggregate = _context.ChangeTracker
                .Entries<TestAggregate>()
                .Select(entry => entry.Entity)
                .SingleOrDefault(entity => entity.Id == domainEvent.AggregateId);

            if (aggregate is not null)
            {
                aggregate.EventWasHandled = true;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
