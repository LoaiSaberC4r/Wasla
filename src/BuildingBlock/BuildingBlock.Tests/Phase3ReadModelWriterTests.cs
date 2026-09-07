using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Tests;

public sealed class Phase3ReadModelWriterTests
{
    [Fact]
    public async Task Standard_ef_registration_resolves_marker_aware_writer()
    {
        using var connection = CreateOpenConnection();
        await using var provider = CreateProvider<ReadDbContext, ReadMarker, ReadResolver>(connection);
        await EnsureCreatedAsync<ReadDbContext>(provider);

        using var scope = provider.CreateScope();

        var writer = scope.ServiceProvider.GetRequiredService<IReadModelWriter<ReadProjection, ReadMarker>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<ReadMarker>>();

        Assert.IsType<EfReadModelWriter<ReadProjection, ReadMarker>>(writer);
        Assert.IsType<EfReadModelUnitOfWork<ReadMarker>>(unitOfWork);
    }

    [Fact]
    public async Task Read_model_writer_does_not_save_until_unit_of_work_saves()
    {
        using var connection = CreateOpenConnection();
        await using var provider = CreateProvider<ReadDbContext, ReadMarker, ReadResolver>(connection);
        await EnsureCreatedAsync<ReadDbContext>(provider);

        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<ReadMarker>>();
        var writer = unitOfWork.Writer<ReadProjection>();

        await writer.AddAsync(new ReadProjection { Id = 1, Name = "Pending" }, TestContext.Current.CancellationToken);

        Assert.False(await ProjectionExistsAsync<ReadDbContext>(connection, 1));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await ProjectionExistsAsync<ReadDbContext>(connection, 1));
    }

    [Fact]
    public async Task Multiple_writers_share_one_read_model_save_boundary()
    {
        using var connection = CreateOpenConnection();
        await using var provider = CreateProvider<ReadDbContext, ReadMarker, ReadResolver>(connection);
        await EnsureCreatedAsync<ReadDbContext>(provider);

        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<ReadMarker>>();

        await unitOfWork.Writer<ReadProjection>().AddAsync(new ReadProjection { Id = 10, Name = "Projection" }, TestContext.Current.CancellationToken);
        await unitOfWork.Writer<OtherReadProjection>().AddAsync(new OtherReadProjection { Id = 20, Name = "Other" }, TestContext.Current.CancellationToken);

        Assert.False(await ProjectionExistsAsync<ReadDbContext>(connection, 10));
        Assert.False(await OtherProjectionExistsAsync<ReadDbContext>(connection, 20));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await ProjectionExistsAsync<ReadDbContext>(connection, 10));
        Assert.True(await OtherProjectionExistsAsync<ReadDbContext>(connection, 20));
    }

    [Fact]
    public async Task Find_update_remove_and_any_respect_global_filters()
    {
        using var connection = CreateOpenConnection();
        await using var provider = CreateProvider<ReadDbContext, ReadMarker, ReadResolver>(connection);
        await EnsureCreatedAsync<ReadDbContext>(provider);

        await using (var seed = CreateContext<ReadDbContext>(connection))
        {
            seed.Projections.AddRange(
                new ReadProjection { Id = 1, Name = "Visible", IsVisible = true },
                new ReadProjection { Id = 2, Name = "Hidden", IsVisible = false });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<ReadMarker>>();
        var writer = unitOfWork.Writer<ReadProjection>();

        var visible = await writer.FindByIdAsync(1, TestContext.Current.CancellationToken);
        var hidden = await writer.FindByIdAsync(2, TestContext.Current.CancellationToken);
        var hiddenExists = await writer.AnyAsync(entity => entity.Id == 2, TestContext.Current.CancellationToken);

        Assert.Equal("Visible", visible?.Name);
        Assert.Null(hidden);
        Assert.False(hiddenExists);

        visible!.Name = "Updated";
        writer.Update(visible);
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Updated", await ProjectionNameAsync<ReadDbContext>(connection, 1));

        writer.Remove(visible);
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(await ProjectionExistsAsync<ReadDbContext>(connection, 1));
    }

    [Fact]
    public async Task Composite_key_single_id_lookup_fails_clearly()
    {
        using var connection = CreateOpenConnection();
        await using var provider = CreateProvider<ReadDbContext, ReadMarker, ReadResolver>(connection);
        await EnsureCreatedAsync<ReadDbContext>(provider);

        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider
            .GetRequiredService<IReadModelWriter<CompositeReadProjection, ReadMarker>>();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => writer.FindByIdAsync("A", TestContext.Current.CancellationToken));

        Assert.Contains("composite primary key", exception.Message);
        Assert.Contains("Use a Specification", exception.Message);
    }

    [Fact]
    public async Task Read_markers_are_isolated_across_two_dbcontexts()
    {
        using var firstConnection = CreateOpenConnection();
        using var secondConnection = CreateOpenConnection();

        var services = new ServiceCollection();
        services.AddDbContext<FirstReadDbContext>(options => options.UseSqlite(firstConnection));
        services.AddDbContext<SecondReadDbContext>(options => options.UseSqlite(secondConnection));
        services.AddScoped<IEfDbContextResolver<FirstReadMarker>, FirstReadResolver>();
        services.AddScoped<IEfDbContextResolver<SecondReadMarker>, SecondReadResolver>();
        services.AddBuildingBlockEntityFrameworkCore();

        await using var provider = services.BuildServiceProvider();
        await EnsureCreatedAsync<FirstReadDbContext>(provider);
        await EnsureCreatedAsync<SecondReadDbContext>(provider);

        using var scope = provider.CreateScope();
        var firstUnitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<FirstReadMarker>>();
        var secondUnitOfWork = scope.ServiceProvider.GetRequiredService<IReadModelUnitOfWork<SecondReadMarker>>();

        await firstUnitOfWork.Writer<ReadProjection>().AddAsync(new ReadProjection { Id = 1, Name = "First" }, TestContext.Current.CancellationToken);
        await secondUnitOfWork.Writer<ReadProjection>().AddAsync(new ReadProjection { Id = 1, Name = "Second" }, TestContext.Current.CancellationToken);

        await firstUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        await secondUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("First", await ProjectionNameAsync<FirstReadDbContext>(firstConnection, 1));
        Assert.Equal("Second", await ProjectionNameAsync<SecondReadDbContext>(secondConnection, 1));
    }

    private static ServiceProvider CreateProvider<TContext, TMarker, TResolver>(SqliteConnection connection)
        where TContext : DbContext
        where TMarker : IReadPersistenceMarker
        where TResolver : class, IEfDbContextResolver<TMarker>
    {
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(options => options.UseSqlite(connection));
        services.AddScoped<IEfDbContextResolver<TMarker>, TResolver>();
        services.AddBuildingBlockEntityFrameworkCore();

        return services.BuildServiceProvider();
    }

    private static async Task EnsureCreatedAsync<TContext>(ServiceProvider provider)
        where TContext : DbContext
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static TContext CreateContext<TContext>(SqliteConnection connection)
        where TContext : ReadDbContextBase
        => (TContext)Activator.CreateInstance(
            typeof(TContext),
            new DbContextOptionsBuilder<TContext>().UseSqlite(connection).Options)!;

    private static async Task<bool> ProjectionExistsAsync<TContext>(SqliteConnection connection, int id)
        where TContext : ReadDbContextBase
    {
        await using var context = CreateContext<TContext>(connection);
        return await context.Projections.AnyAsync(entity => entity.Id == id);
    }

    private static async Task<bool> OtherProjectionExistsAsync<TContext>(SqliteConnection connection, int id)
        where TContext : ReadDbContextBase
    {
        await using var context = CreateContext<TContext>(connection);
        return await context.OtherProjections.AnyAsync(entity => entity.Id == id);
    }

    private static async Task<string?> ProjectionNameAsync<TContext>(SqliteConnection connection, int id)
        where TContext : ReadDbContextBase
    {
        await using var context = CreateContext<TContext>(connection);
        return await context.Projections
            .Where(entity => entity.Id == id)
            .Select(entity => entity.Name)
            .SingleOrDefaultAsync();
    }

    private abstract class ReadDbContextBase : DbContext
    {
        protected ReadDbContextBase(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<ReadProjection> Projections => Set<ReadProjection>();

        public DbSet<OtherReadProjection> OtherProjections => Set<OtherReadProjection>();

        public DbSet<CompositeReadProjection> CompositeProjections => Set<CompositeReadProjection>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReadProjection>(builder =>
            {
                builder.HasKey(entity => entity.Id);
                builder.HasQueryFilter(entity => entity.IsVisible);
            });
            modelBuilder.Entity<OtherReadProjection>().HasKey(entity => entity.Id);
            modelBuilder.Entity<CompositeReadProjection>()
                .HasKey(entity => new { entity.Partition, entity.LocalId });
        }
    }

    private sealed class ReadDbContext : ReadDbContextBase
    {
        public ReadDbContext(DbContextOptions<ReadDbContext> options)
            : base(options)
        {
        }
    }

    private sealed class FirstReadDbContext : ReadDbContextBase
    {
        public FirstReadDbContext(DbContextOptions<FirstReadDbContext> options)
            : base(options)
        {
        }
    }

    private sealed class SecondReadDbContext : ReadDbContextBase
    {
        public SecondReadDbContext(DbContextOptions<SecondReadDbContext> options)
            : base(options)
        {
        }
    }

    private sealed class ReadProjection
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;
    }

    private sealed class OtherReadProjection
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class CompositeReadProjection
    {
        public string Partition { get; set; } = string.Empty;

        public int LocalId { get; set; }
    }

    private sealed class ReadMarker : IReadPersistenceMarker
    {
    }

    private sealed class FirstReadMarker : IReadPersistenceMarker
    {
    }

    private sealed class SecondReadMarker : IReadPersistenceMarker
    {
    }

    private sealed class ReadResolver : IEfDbContextResolver<ReadMarker>
    {
        private readonly ReadDbContext _context;

        public ReadResolver(ReadDbContext context)
        {
            _context = context;
        }

        public DbContext Resolve() => _context;
    }

    private sealed class FirstReadResolver : IEfDbContextResolver<FirstReadMarker>
    {
        private readonly FirstReadDbContext _context;

        public FirstReadResolver(FirstReadDbContext context)
        {
            _context = context;
        }

        public DbContext Resolve() => _context;
    }

    private sealed class SecondReadResolver : IEfDbContextResolver<SecondReadMarker>
    {
        private readonly SecondReadDbContext _context;

        public SecondReadResolver(SecondReadDbContext context)
        {
            _context = context;
        }

        public DbContext Resolve() => _context;
    }
}
