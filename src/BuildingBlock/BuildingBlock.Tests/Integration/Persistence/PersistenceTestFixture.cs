using BuildingBlock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class PersistenceTestFixture : IAsyncDisposable
{
    private PersistenceTestFixture(SqliteTestDatabase database)
    {
        Database = database;
    }

    public SqliteTestDatabase Database { get; }

    public static async Task<PersistenceTestFixture> CreateAsync(CancellationToken cancellationToken = default)
    {
        var database = await SqliteTestDatabase.CreateAsync(cancellationToken);
        var fixture = new PersistenceTestFixture(database);
        await fixture.EnsureAllCreatedAsync(cancellationToken);
        return fixture;
    }

    public TestDbContext CreateContext(
        Action<DbContextOptionsBuilder<TestDbContext>>? configure = null)
        => new(Database.CreateOptions(configure));

    public TestWriteDbContext CreateWriteContext(
        Action<DbContextOptionsBuilder<TestWriteDbContext>>? configure = null)
        => new(Database.CreateOptions(configure));

    public TestReadDbContext CreateReadContext(
        Action<DbContextOptionsBuilder<TestReadDbContext>>? configure = null)
        => new(Database.CreateOptions(configure));

    public TestWriteResolver CreateWriteResolver(DbContext context)
        => new(context);

    public TestReadResolver CreateReadResolver(DbContext context)
        => new(context);

    public async Task EnsureWriteCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateWriteContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task EnsureReadCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateReadContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task EnsureAllCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
    }
}

internal sealed class TestWriteResolver : IEfDbContextResolver<TestWriteMarker>
{
    private readonly DbContext _context;

    public TestWriteResolver(DbContext context)
    {
        _context = context;
    }

    public DbContext Resolve() => _context;
}

internal sealed class TestReadResolver : IEfDbContextResolver<TestReadMarker>
{
    private readonly DbContext _context;

    public TestReadResolver(DbContext context)
    {
        _context = context;
    }

    public DbContext Resolve() => _context;
}

internal sealed class OtherTestReadResolver : IEfDbContextResolver<OtherTestReadMarker>
{
    private readonly DbContext _context;

    public OtherTestReadResolver(DbContext context)
    {
        _context = context;
    }

    public DbContext Resolve() => _context;
}

internal sealed class OtherTestWriteResolver : IEfDbContextResolver<OtherTestWriteMarker>
{
    private readonly DbContext _context;

    public OtherTestWriteResolver(DbContext context)
    {
        _context = context;
    }

    public DbContext Resolve() => _context;
}
