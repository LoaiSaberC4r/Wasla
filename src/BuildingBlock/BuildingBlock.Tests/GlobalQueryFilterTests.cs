using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace BuildingBlock.Tests;

public sealed class GlobalQueryFilterTests
{
    [Fact]
    public void Application_assembly_does_not_expose_soft_delete_filter_controls()
    {
        var applicationAssembly = typeof(IReadPersistenceMarker).Assembly;
        var exportedTypeNames = applicationAssembly.GetExportedTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.DoesNotContain("ISoftDelete" + "FilterState", exportedTypeNames);
        Assert.DoesNotContain("ISoftDelete" + "FilterScope", exportedTypeNames);
        var constructorParameters = applicationAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("BuildingBlock.Application", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()))
            .ToArray();

        Assert.DoesNotContain(
            constructorParameters,
            parameter => parameter.ParameterType.FullName?.Contains("SoftDeleteFilterState", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void BuildingBlock_source_does_not_call_unrestricted_ignore_query_filters()
    {
        var root = FindRepositoryRoot();
        var unsafeCall = new Regex("Ignore" + "QueryFilters\\s*\\(\\s*\\)", RegexOptions.Compiled);
        var matches = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}BuildingBlock.Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(item => unsafeCall.IsMatch(item.line))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.number}")
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Soft_deleted_entities_are_excluded_and_second_filter_is_composed()
    {
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.Entities.AddRange(
            new FilterEntity { Id = 1, Name = "Visible", IsVisible = true },
            new FilterEntity { Id = 2, Name = "Deleted", IsVisible = true, IsDeleted = true },
            new FilterEntity { Id = 3, Name = "Hidden", IsVisible = false });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var names = await db.Entities.Select(entity => entity.Name).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Visible" }, names);

        var filters = db.Model.FindEntityType(typeof(FilterEntity))!
            .GetDeclaredQueryFilters()
            .ToArray();

        Assert.Equal(2, filters.Length);
        Assert.All(filters, filter => Assert.False(filter.IsAnonymous));
        var softDeleteFilter = Assert.Single(
            filters,
            filter => filter.Key == BuildingBlockQueryFilterNames.SoftDelete);
        var visibilityFilter = Assert.Single(
            filters,
            filter => filter.Key != BuildingBlockQueryFilterNames.SoftDelete);
        Assert.Equal(ExpressionType.Equal, softDeleteFilter.Expression!.Body.NodeType);
        Assert.Equal(ExpressionType.MemberAccess, visibilityFilter.Expression!.Body.NodeType);
    }

    [Fact]
    public async Task Soft_deleted_repositories_open_only_soft_delete_filter()
    {
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedAsync(db);
        db.ChangeTracker.Clear();

        var resolver = new TestResolver(db);
        var readRepository = new EfReadRepository<FilterEntity, TestMarker>(resolver);
        var softReadRepository = new EfSoftDeletedReadRepository<FilterEntity, TestMarker>(resolver);
        var softWriteRepository = new EfSoftDeletedWriteRepository<FilterEntity, TestMarker>(resolver);

        var normalRead = await readRepository.GetByIdAsync(2, TestContext.Current.CancellationToken);
        var deletedRead = await softReadRepository.GetDeletedByIdAsync(2, TestContext.Current.CancellationToken);
        var deletedList = await softReadRepository.ListDeletedAsync(new AllEntitiesSpec(), TestContext.Current.CancellationToken);
        var trackedDeleted = await softWriteRepository.GetDeletedTrackedByIdAsync(2, TestContext.Current.CancellationToken);

        Assert.Null(normalRead);
        Assert.Equal("Deleted", deletedRead?.Name);
        Assert.Equal(new[] { "Deleted" }, deletedList.Select(entity => entity.Name));
        Assert.Equal(EntityState.Unchanged, db.Entry(trackedDeleted!).State);
        Assert.Null(await softReadRepository.GetDeletedByIdAsync(3, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Plain_dbcontext_uses_named_soft_delete_filter_without_compatibility_context()
    {
        using var connection = CreateOpenConnection();
        await using var db = CreatePlainContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedAsync(db);
        db.ChangeTracker.Clear();

        var resolver = new TestResolver(db);
        var readRepository = new EfReadRepository<FilterEntity, TestMarker>(resolver);
        var softReadRepository = new EfSoftDeletedReadRepository<FilterEntity, TestMarker>(resolver);
        var softWriteRepository = new EfSoftDeletedWriteRepository<FilterEntity, TestMarker>(resolver);

        var normalRead = await readRepository.GetByIdAsync(2, TestContext.Current.CancellationToken);
        var deletedRead = await softReadRepository.GetDeletedByIdAsync(2, TestContext.Current.CancellationToken);
        var deletedList = await softReadRepository.ListDeletedAsync(new AllEntitiesSpec(), TestContext.Current.CancellationToken);
        var trackedEntriesAfterDeletedReads = db.ChangeTracker.Entries<FilterEntity>().ToArray();
        var trackedDeleted = await softWriteRepository.GetDeletedTrackedByIdAsync(2, TestContext.Current.CancellationToken);

        Assert.Null(normalRead);
        Assert.Equal("Deleted", deletedRead?.Name);
        Assert.Equal(new[] { "Deleted" }, deletedList.Select(entity => entity.Name));
        Assert.DoesNotContain(trackedEntriesAfterDeletedReads, entry => entry.Entity.Id == 2);
        Assert.Equal(EntityState.Unchanged, db.Entry(trackedDeleted!).State);
        Assert.Null(await softReadRepository.GetDeletedByIdAsync(3, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Normal_specifications_do_not_expose_global_filter_bypass()
    {
        var specificationType = typeof(Specification<FilterEntity>);
        var methodName = "Ignore" + "GlobalFilters";
        var propertyName = "IsGlobalFilters" + "Ignored";

        Assert.Empty(specificationType.GetMember(methodName, System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic));
        Assert.Null(specificationType.GetProperty(propertyName));
    }

    private static FilterDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<FilterDbContext>()
            .UseSqlite(connection)
            .Options);

    private static PlainFilterDbContext CreatePlainContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<PlainFilterDbContext>()
            .UseSqlite(connection)
            .Options);

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static async Task SeedAsync(FilterDbContext db)
    {
        db.Entities.AddRange(
            new FilterEntity { Id = 1, Name = "Visible", IsVisible = true },
            new FilterEntity { Id = 2, Name = "Deleted", IsVisible = true, IsDeleted = true },
            new FilterEntity { Id = 3, Name = "HiddenDeleted", IsVisible = false, IsDeleted = true });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAsync(PlainFilterDbContext db)
    {
        db.Entities.AddRange(
            new FilterEntity { Id = 1, Name = "Visible", IsVisible = true },
            new FilterEntity { Id = 2, Name = "Deleted", IsVisible = true, IsDeleted = true },
            new FilterEntity { Id = 3, Name = "HiddenDeleted", IsVisible = false, IsDeleted = true });
        await db.SaveChangesAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuildingBlock.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class FilterDbContext : DbContext
    {
        public FilterDbContext(DbContextOptions<FilterDbContext> options)
            : base(options)
        {
        }

        public DbSet<FilterEntity> Entities => Set<FilterEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FilterEntity>().HasKey(entity => entity.Id);
            modelBuilder.ApplySoftDeleteQueryFilter();
            modelBuilder.Entity<FilterEntity>()
                .HasQueryFilter("Tests.Visibility", entity => entity.IsVisible);
        }
    }

    private sealed class PlainFilterDbContext : DbContext
    {
        public PlainFilterDbContext(DbContextOptions<PlainFilterDbContext> options)
            : base(options)
        {
        }

        public DbSet<FilterEntity> Entities => Set<FilterEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FilterEntity>().HasKey(entity => entity.Id);
            modelBuilder.ApplySoftDeleteQueryFilter();
            modelBuilder.Entity<FilterEntity>()
                .HasQueryFilter("Tests.Visibility", entity => entity.IsVisible);
        }
    }

    private sealed class FilterEntity : IAggregateRoot, ISoftDeleteEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
    }

    private sealed class TestMarker : IReadPersistenceMarker, IWritePersistenceMarker
    {
    }

    private sealed class TestResolver : IEfDbContextResolver<TestMarker>
    {
        private readonly DbContext _dbContext;

        public TestResolver(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public DbContext Resolve() => _dbContext;
    }

    private sealed class AllEntitiesSpec : Specification<FilterEntity>
    {
    }
}
