using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace BuildingBlock.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerIntegrationTestGroup : ICollectionFixture<SqlServerIntegrationFixture>
{
    public const string Name = "SqlServerIntegration";
}

[Collection(SqlServerIntegrationTestGroup.Name)]
public sealed class SqlServerIntegrationTests
{
    private readonly SqlServerIntegrationFixture _fixture;

    public SqlServerIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Connectivity_schema_and_actual_server_version_are_available()
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";

        var version = (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Equal(version, _fixture.ProductVersion);
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Real_unique_constraint_maps_to_safe_conflict_without_provider_details()
    {
        var externalId = $"unique-{Guid.NewGuid():N}";
        await using (var seed = _fixture.CreateContext())
        {
            seed.Aggregates.Add(NewAggregate(externalId, "first"));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var duplicateContext = _fixture.CreateContext();
        duplicateContext.Aggregates.Add(NewAggregate(externalId, "duplicate"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            duplicateContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        var mapper = new SqlServerExceptionToErrorMapper();

        Assert.True(mapper.TryMap(exception, out var error));
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.UniqueConstraint, error.Code);
        Assert.DoesNotContain(nameof(SqlAggregate), error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExternalId", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Real_rowversion_concurrency_is_enforced_and_maps_to_conflict()
    {
        var entity = NewAggregate($"rowversion-{Guid.NewGuid():N}", "original");
        await using (var seed = _fixture.CreateContext())
        {
            seed.Add(entity);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstCopy = await first.Aggregates.SingleAsync(
            item => item.Id == entity.Id,
            TestContext.Current.CancellationToken);
        var secondCopy = await second.Aggregates.SingleAsync(
            item => item.Id == entity.Id,
            TestContext.Current.CancellationToken);

        firstCopy.Name = "first update";
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondCopy.Name = "stale update";
        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            second.SaveChangesAsync(TestContext.Current.CancellationToken));

        var mapper = new BuildingBlock.Infrastructure.Exceptions.EfCoreExceptionToErrorMapper();
        Assert.True(mapper.TryMap(exception, out var error));
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal(ErrorCodes.Persistence.Concurrency, error.Code);
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Named_filters_auditing_soft_delete_restore_and_permanent_delete_use_real_sql_server()
    {
        var clock = new ManualClock(new DateTime(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc));
        var visible = NewAggregate($"soft-{Guid.NewGuid():N}", "visible");
        var hiddenDeleted = NewAggregate($"hidden-{Guid.NewGuid():N}", "hidden");
        hiddenDeleted.IsVisible = false;
        hiddenDeleted.IsDeleted = true;

        await using var context = _fixture.CreateContext(clock: clock);
        context.AddRange(visible, hiddenDeleted);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(clock.UtcNow, visible.CreatedOnUtc);
        context.ChangeTracker.Clear();

        var resolver = new SqlResolver(context);
        var normal = new EfReadRepository<SqlAggregate, SqlMarker>(resolver);
        var deletedRead = new EfSoftDeletedReadRepository<SqlAggregate, SqlMarker>(resolver);
        var deletedWrite = new EfSoftDeletedWriteRepository<SqlAggregate, SqlMarker>(resolver);
        Assert.Null(await deletedRead.GetDeletedByIdAsync(hiddenDeleted.Id, TestContext.Current.CancellationToken));

        var tracked = await context.Aggregates.SingleAsync(
            item => item.Id == visible.Id,
            TestContext.Current.CancellationToken);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        context.Remove(tracked);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Null(await normal.GetByIdAsync(visible.Id, TestContext.Current.CancellationToken));
        var deleted = await deletedWrite.GetDeletedTrackedByIdAsync(
            visible.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(deleted);
        Assert.Equal(clock.UtcNow, deleted.DeletedOnUtc);

        clock.UtcNow = clock.UtcNow.AddHours(1);
        deleted.IsDeleted = false;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var restored = await normal.GetByIdAsync(visible.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(clock.UtcNow, restored.RestoredOnUtc);

        context.Attach(restored!);
        context.Remove(restored!);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var deletedAgain = await deletedWrite.GetDeletedTrackedByIdAsync(
            visible.Id,
            TestContext.Current.CancellationToken);
        Assert.NotNull(deletedAgain);
        deletedWrite.PermanentDelete(deletedAgain);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, await CountRowsByIdAsync(visible.Id));
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Specifications_transactions_and_domain_event_retry_run_against_real_provider()
    {
        var prefix = $"spec-{Guid.NewGuid():N}";
        await using (var seed = _fixture.CreateContext())
        {
            seed.AddRange(
                NewAggregate($"{prefix}-1", "Charlie"),
                NewAggregate($"{prefix}-2", "Alpha"),
                NewAggregate($"{prefix}-3", "Bravo"));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var queryContext = _fixture.CreateContext())
        {
            var repository = new EfReadRepository<SqlAggregate, SqlMarker>(new SqlResolver(queryContext));
            var (page, count) = await repository.ListWithLongCountAsync(
                new PagedExternalIdSpecification(prefix),
                TestContext.Current.CancellationToken);
            Assert.Equal(3, count);
            Assert.Equal(new[] { "Alpha", "Bravo" }, page.Select(item => item.Name));
        }

        var rolledBackId = Guid.NewGuid();
        await using (var transactionContext = _fixture.CreateContext())
        {
            await using var transaction = await transactionContext.Database.BeginTransactionAsync(
                TestContext.Current.CancellationToken);
            transactionContext.Add(NewAggregate($"rollback-{Guid.NewGuid():N}", "rollback", rolledBackId));
            await transactionContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await using (var verification = _fixture.CreateContext())
        {
            Assert.False(await verification.Aggregates.AnyAsync(
                item => item.Id == rolledBackId,
                TestContext.Current.CancellationToken));
        }

        var duplicateExternalId = $"event-{Guid.NewGuid():N}";
        await using (var seed = _fixture.CreateContext())
        {
            seed.Add(NewAggregate(duplicateExternalId, "seed"));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var dispatcher = new MutatingDispatcher();
        await using var eventContext = _fixture.CreateContext(dispatcher);
        var eventEntity = NewAggregate(duplicateExternalId, "before event");
        eventEntity.RaiseRename("handled");
        eventContext.Add(eventEntity);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            eventContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Single(eventEntity.DomainEvents);
        Assert.Equal(1, dispatcher.DispatchCount);

        eventEntity.ExternalId = $"event-retry-{Guid.NewGuid():N}";
        await eventContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Empty(eventEntity.DomainEvents);
        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Equal("handled", eventEntity.Name);
    }

    private async Task<int> CountRowsByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [Aggregates] WHERE [Id] = @id";
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static SqlAggregate NewAggregate(string externalId, string name, Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            ExternalId = externalId,
            Name = name,
            IsVisible = true
        };

    private sealed class PagedExternalIdSpecification : Specification<SqlAggregate>
    {
        public PagedExternalIdSpecification(string prefix)
        {
            AddCriteria(item => item.ExternalId.StartsWith(prefix));
            AddOrderBy(item => item.Name);
            ApplyPaging(pageNumber: 1, pageSize: 2);
            TagWith("Phase3.SqlServer.Specification");
        }
    }
}

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    private const string ConnectionStringVariable = "BUILDINGBLOCK_SQLSERVER_CONNECTION_STRING";
    private const string AllowCreateVariable = "BUILDINGBLOCK_SQLSERVER_ALLOW_DATABASE_CREATE";
    private MsSqlContainer? _container;
    private string? _masterConnectionString;
    private string? _databaseName;

    public string ConnectionString { get; private set; } = string.Empty;
    public string ProductVersion { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync(TestContext.Current.CancellationToken);
            _masterConnectionString = _container.GetConnectionString();
        }
        else
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(AllowCreateVariable),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{AllowCreateVariable}=true is required before the integration suite may create an isolated test database.");
            }

            _masterConnectionString = configured;
        }

        _databaseName = $"BuildingBlockPhase3_{Guid.NewGuid():N}";
        var masterBuilder = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = "master"
        };
        _masterConnectionString = masterBuilder.ConnectionString;

        await using (var master = new SqlConnection(_masterConnectionString))
        {
            await master.OpenAsync(TestContext.Current.CancellationToken);
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var databaseBuilder = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = _databaseName
        };
        ConnectionString = databaseBuilder.ConnectionString;

        await using (var context = CreateContext())
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var version = connection.CreateCommand();
        version.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";
        ProductVersion = (string)(await version.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public SqlServerPhase3Context CreateContext(
        IDomainEventDispatcher? dispatcher = null,
        IDateTimeProvider? clock = null)
    {
        dispatcher ??= NoOpDispatcher.Instance;
        clock ??= new ManualClock(DateTime.UtcNow);
        var domainEvents = new DomainEventsInterceptor(
            dispatcher,
            NullLogger<DomainEventsInterceptor>.Instance);
        var softDelete = new SoftDeleteEntitiesInterceptor(clock);
        var auditing = new AuditableEntitiesInterceptor(clock);
        var options = new DbContextOptionsBuilder<SqlServerPhase3Context>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(domainEvents, softDelete, auditing)
            .Options;
        return new SqlServerPhase3Context(options);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        if (!string.IsNullOrWhiteSpace(_masterConnectionString) &&
            !string.IsNullOrWhiteSpace(_databaseName))
        {
            await using var master = new SqlConnection(_masterConnectionString);
            await master.OpenAsync(CancellationToken.None);
            await using var drop = master.CreateCommand();
            drop.CommandText =
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_databaseName}]";
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private sealed class NoOpDispatcher : IDomainEventDispatcher
    {
        public static readonly NoOpDispatcher Instance = new();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
            => Task.CompletedTask;
    }
}

public sealed class SqlServerPhase3Context : DbContext
{
    public SqlServerPhase3Context(DbContextOptions<SqlServerPhase3Context> options) : base(options)
    {
    }

    public DbSet<SqlAggregate> Aggregates => Set<SqlAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqlAggregate>(builder =>
        {
            builder.ToTable("Aggregates");
            builder.HasKey(entity => entity.Id);
            builder.HasIndex(entity => entity.ExternalId).IsUnique();
            builder.Property(entity => entity.ExternalId).HasMaxLength(160);
            builder.Property(entity => entity.Name).HasMaxLength(256);
            builder.Property(entity => entity.RowVersion).IsRowVersion();
            builder.Ignore(entity => entity.DomainEvents);
            builder.HasQueryFilter("Tests.Visibility", entity => entity.IsVisible);
        });
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}

public sealed class SqlAggregate : IAggregateRoot, ISoftDeleteEntity, IAuditableEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseRename(string name) => _domainEvents.Add(new RenameSqlAggregateEvent(this, name));

    public void RemoveDomainEvents(IReadOnlyCollection<IDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            _domainEvents.Remove(domainEvent);
        }
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed record RenameSqlAggregateEvent(SqlAggregate Aggregate, string Name) : IDomainEvent;

internal sealed class SqlMarker : IReadPersistenceMarker, IWritePersistenceMarker
{
}

internal sealed class SqlResolver : IEfDbContextResolver<SqlMarker>
{
    private readonly DbContext _context;

    public SqlResolver(DbContext context)
    {
        _context = context;
    }

    public DbContext Resolve() => _context;
}

internal sealed class ManualClock : IDateTimeProvider
{
    public ManualClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}

internal sealed class MutatingDispatcher : IDomainEventDispatcher
{
    public int DispatchCount { get; private set; }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var rename = Assert.IsType<RenameSqlAggregateEvent>(domainEvent);
        rename.Aggregate.Name = rename.Name;
        DispatchCount++;
        return Task.CompletedTask;
    }
}
