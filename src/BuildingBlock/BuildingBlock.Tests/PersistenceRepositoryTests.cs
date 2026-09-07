using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Enums;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlock.Tests;

public sealed class PersistenceRepositoryTests
{
    [Fact]
    public async Task Repositories_load_entities_by_metadata_primary_key_name()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        var key = Guid.NewGuid();
        db.Entities.Add(new TestEntity { Key = key, Name = "Alpha" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var resolver = new TestResolver(db);
        var readRepository = new EfReadRepository<TestEntity, TestMarker>(resolver);
        var writeRepository = new EfWriteRepository<TestEntity, TestMarker>(resolver);

        var readEntity = await readRepository.GetByIdAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal("Alpha", readEntity?.Name);
        Assert.Empty(db.ChangeTracker.Entries<TestEntity>());

        var writeEntity = await writeRepository.GetByIdAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal("Alpha", writeEntity?.Name);
        Assert.Equal(EntityState.Unchanged, db.Entry(writeEntity!).State);
    }

    [Fact]
    public async Task Composite_primary_key_lookup_throws_clear_exception()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        db.CompositeEntities.Add(new CompositeKeyEntity { Partition = "A", LocalId = 1, Name = "Composite" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new EfReadRepository<CompositeKeyEntity, TestMarker>(new TestResolver(db));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => repository.GetByIdAsync("A", TestContext.Current.CancellationToken));
        Assert.Contains("composite primary key", exception.Message);
        Assert.Contains("Use a Specification", exception.Message);
    }

    [Fact]
    public async Task Read_repository_never_tracks_entities()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        db.Entities.Add(new TestEntity { Key = Guid.NewGuid(), Name = "TrackedRequested" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var repository = new EfReadRepository<TestEntity, TestMarker>(new TestResolver(db));

        var propertyResult = await repository.GetByPropertyAsync(entity => entity.Name == "TrackedRequested", TestContext.Current.CancellationToken);
        var specificationResult = await repository.FirstOrDefaultAsync(new TrackingByNameSpec("TrackedRequested"), TestContext.Current.CancellationToken);

        Assert.NotNull(propertyResult);
        Assert.NotNull(specificationResult);
        Assert.Empty(db.ChangeTracker.Entries<TestEntity>());
    }

    [Fact]
    public async Task Write_repository_returns_tracked_entities()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        db.Entities.Add(new TestEntity { Key = Guid.NewGuid(), Name = "WriteTracked" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var repository = new EfWriteRepository<TestEntity, TestMarker>(new TestResolver(db));

        var loaded = await repository.FirstOrDefaultAsync(new ByNameSpec("WriteTracked"), TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(EntityState.Unchanged, db.Entry(loaded!).State);
    }

    [Fact]
    public async Task Specification_count_keeps_criteria_and_filters_but_ignores_shape_options()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        await SeedCountEntitiesAsync(db);
        db.ChangeTracker.Clear();

        var repository = new EfReadRepository<TestEntity, TestMarker>(new TestResolver(db));

        var (data, count) = await repository.ListWithLongCountAsync(new PagedOrderedIncludedMatchSpec(), TestContext.Current.CancellationToken);

        Assert.Single(data);
        Assert.Equal(2L, count);
        Assert.All(data, entity => Assert.StartsWith("Match", entity.Name, StringComparison.Ordinal));
        Assert.Empty(db.ChangeTracker.Entries<TestEntity>());
    }

    [Fact]
    public async Task Projection_specification_requires_selector_and_counts_entity_rows()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        await SeedCountEntitiesAsync(db);
        db.ChangeTracker.Clear();

        var repository = new EfReadRepository<TestEntity, TestMarker>(new TestResolver(db));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ListAsync(new ProjectionWithoutSelectorSpec(), TestContext.Current.CancellationToken));
        Assert.Equal(
            "Projection selector is not defined. Call Select(...) inside the specification constructor.",
            exception.Message);

        var (names, count) = await repository.ListWithLongCountAsync(new PagedNameProjectionSpec(), TestContext.Current.CancellationToken);

        Assert.Single(names);
        Assert.Equal(2L, count);
        Assert.StartsWith("Match", names[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Soft_deleted_repositories_open_only_soft_delete_scope()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        var visibleDeletedKey = Guid.NewGuid();
        var hiddenDeletedKey = Guid.NewGuid();
        db.Entities.AddRange(
            new TestEntity { Key = Guid.NewGuid(), Name = "Visible", IsVisible = true },
            new TestEntity { Key = visibleDeletedKey, Name = "VisibleDeleted", IsVisible = true, IsDeleted = true },
            new TestEntity { Key = hiddenDeletedKey, Name = "HiddenDeleted", IsVisible = false, IsDeleted = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var resolver = new TestResolver(db);
        var readRepository = new EfReadRepository<TestEntity, TestMarker>(resolver);
        var softReadRepository = new EfSoftDeletedReadRepository<TestEntity, TestMarker>(resolver);
        var softWriteRepository = new EfSoftDeletedWriteRepository<TestEntity, TestMarker>(resolver);

        var normalRead = await readRepository.GetByIdAsync(visibleDeletedKey, TestContext.Current.CancellationToken);
        var deletedRead = await softReadRepository.GetDeletedByIdAsync(visibleDeletedKey, TestContext.Current.CancellationToken);
        var deletedList = await softReadRepository.ListDeletedAsync(new AllEntitiesSpec(), TestContext.Current.CancellationToken);
        var trackedDeleted = await softWriteRepository.GetDeletedTrackedByIdAsync(visibleDeletedKey, TestContext.Current.CancellationToken);

        Assert.Null(normalRead);
        Assert.Equal("VisibleDeleted", deletedRead?.Name);
        Assert.Equal(new[] { "VisibleDeleted" }, deletedList.Select(entity => entity.Name));
        Assert.Equal(EntityState.Unchanged, db.Entry(trackedDeleted!).State);
        Assert.Null(await softReadRepository.GetDeletedByIdAsync(hiddenDeletedKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Soft_deleted_composite_key_lookup_throws_clear_exception()
    {
        await using var db = new PersistenceDbContext(CreateOptions());
        db.CompositeSoftDeletedEntities.Add(new CompositeSoftDeletedEntity
        {
            Partition = "A",
            LocalId = 1,
            IsDeleted = true
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new EfSoftDeletedReadRepository<CompositeSoftDeletedEntity, TestMarker>(
            new TestResolver(db));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => repository.GetDeletedByIdAsync("A", TestContext.Current.CancellationToken));
        Assert.Contains("composite primary key", exception.Message);
    }

    [Fact]
    public async Task Unit_of_work_is_the_only_save_boundary_for_repository_changes()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var databaseRoot = new InMemoryDatabaseRoot();
        var key = Guid.NewGuid();

        await using var db = new PersistenceDbContext(CreateOptions(databaseRoot, databaseName));
        var unitOfWork = new EfUnitOfWork<TestMarker>(new TestResolver(db));
        var repository = unitOfWork.WriteRepository<TestEntity>();
        var entity = new TestEntity { Key = key, Name = "Original" };

        await repository.AddAsync(entity, TestContext.Current.CancellationToken);
        Assert.False(await EntityExistsAsync(databaseRoot, databaseName, key));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Original", await EntityNameAsync(databaseRoot, databaseName, key));

        entity.Name = "Updated";
        repository.Update(entity);
        Assert.Equal("Original", await EntityNameAsync(databaseRoot, databaseName, key));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Updated", await EntityNameAsync(databaseRoot, databaseName, key));

        repository.Delete(entity);
        Assert.True(await EntityExistsAsync(databaseRoot, databaseName, key));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await EntityExistsAsync(databaseRoot, databaseName, key));
    }

    [Fact]
    public void Specification_combines_tracking_deterministically_and_validates_tags()
    {
        var trackAll = new CombinedSpec(new IdentityResolutionSpec(), new TrackingByNameSpec("Alpha"));
        var identityResolution = new CombinedSpec(new IdentityResolutionSpec(), new ByNameSpec("Alpha"));
        var trimmedTag = new TaggedSpec("  phase-two  ");

        Assert.Equal(TrackingBehavior.TrackAll, trackAll.Tracking);
        Assert.Equal(TrackingBehavior.NoTrackingWithIdentityResolution, identityResolution.Tracking);
        Assert.Equal(new[] { "phase-two" }, trimmedTag.QueryTags);
        Assert.Throws<ArgumentException>(() => new TaggedSpec(new string('x', 257)));
        Assert.Throws<ArgumentException>(() => new TaggedSpec("   "));
    }

    private static DbContextOptions<PersistenceDbContext> CreateOptions(
        InMemoryDatabaseRoot? databaseRoot = null,
        string? databaseName = null)
    {
        var builder = new DbContextOptionsBuilder<PersistenceDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"), databaseRoot);

        return builder.Options;
    }

    private static async Task SeedCountEntitiesAsync(PersistenceDbContext db)
    {
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();
        db.Entities.AddRange(
            new TestEntity
            {
                Key = firstKey,
                Name = "Match B",
                Children = { new TestChild { Id = Guid.NewGuid(), Label = "First child" } }
            },
            new TestEntity
            {
                Key = secondKey,
                Name = "Match A",
                Children = { new TestChild { Id = Guid.NewGuid(), Label = "Second child" } }
            },
            new TestEntity { Key = Guid.NewGuid(), Name = "Other" },
            new TestEntity { Key = Guid.NewGuid(), Name = "Match Deleted", IsDeleted = true },
            new TestEntity { Key = Guid.NewGuid(), Name = "Match Hidden", IsVisible = false });

        await db.SaveChangesAsync();
    }

    private static async Task<bool> EntityExistsAsync(
        InMemoryDatabaseRoot databaseRoot,
        string databaseName,
        Guid key)
    {
        await using var db = new PersistenceDbContext(CreateOptions(databaseRoot, databaseName));
        return await db.Entities.AnyAsync(entity => entity.Key == key);
    }

    private static async Task<string?> EntityNameAsync(
        InMemoryDatabaseRoot databaseRoot,
        string databaseName,
        Guid key)
    {
        await using var db = new PersistenceDbContext(CreateOptions(databaseRoot, databaseName));
        return await db.Entities
            .Where(entity => entity.Key == key)
            .Select(entity => entity.Name)
            .FirstOrDefaultAsync();
    }

    private sealed class PersistenceDbContext : DbContext
    {
        public PersistenceDbContext(DbContextOptions<PersistenceDbContext> options)
            : base(options)
        {
        }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        public DbSet<TestChild> Children => Set<TestChild>();

        public DbSet<CompositeKeyEntity> CompositeEntities => Set<CompositeKeyEntity>();

        public DbSet<CompositeSoftDeletedEntity> CompositeSoftDeletedEntities => Set<CompositeSoftDeletedEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(builder =>
            {
                builder.HasKey(entity => entity.Key);
                builder.HasMany(entity => entity.Children)
                    .WithOne()
                    .HasForeignKey(child => child.ParentKey);
            });
            modelBuilder.Entity<TestChild>().HasKey(child => child.Id);
            modelBuilder.Entity<CompositeKeyEntity>().HasKey(entity => new { entity.Partition, entity.LocalId });
            modelBuilder.Entity<CompositeSoftDeletedEntity>().HasKey(entity => new { entity.Partition, entity.LocalId });

            modelBuilder.ApplySoftDeleteQueryFilter();
            modelBuilder.Entity<TestEntity>()
                .HasQueryFilter("Tests.Visibility", entity => entity.IsVisible);
        }
    }

    private sealed class TestEntity : IAggregateRoot, ISoftDeleteEntity
    {
        public Guid Key { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime? DeletedOnUtc { get; set; }

        public DateTime? RestoredOnUtc { get; set; }

        public List<TestChild> Children { get; set; } = new();
    }

    private sealed class TestChild
    {
        public Guid Id { get; set; }

        public Guid ParentKey { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class CompositeKeyEntity
    {
        public string Partition { get; set; } = string.Empty;

        public int LocalId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class CompositeSoftDeletedEntity : ISoftDeleteEntity
    {
        public string Partition { get; set; } = string.Empty;

        public int LocalId { get; set; }

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

    private sealed class AllEntitiesSpec : Specification<TestEntity>
    {
    }

    private sealed class ByNameSpec : Specification<TestEntity>
    {
        public ByNameSpec(string name)
        {
            AddCriteria(entity => entity.Name == name);
        }
    }

    private sealed class TrackingByNameSpec : Specification<TestEntity>
    {
        public TrackingByNameSpec(string name)
        {
            AddCriteria(entity => entity.Name == name);
            UseTracking();
        }
    }

    private sealed class IdentityResolutionSpec : Specification<TestEntity>
    {
        public IdentityResolutionSpec()
        {
            UseNoTrackingWithIdentityResolution();
        }
    }

    private sealed class PagedOrderedIncludedMatchSpec : Specification<TestEntity>
    {
        public PagedOrderedIncludedMatchSpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            Include(entity => entity.Children);
            AddOrderByDescending(entity => entity.Name);
            ApplyPaging(1, 1);
        }
    }

    private sealed class ProjectionWithoutSelectorSpec : Specification<TestEntity, string>
    {
    }

    private sealed class PagedNameProjectionSpec : Specification<TestEntity, string>
    {
        public PagedNameProjectionSpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            AddOrderBy(entity => entity.Name);
            ApplyPaging(1, 1);
            Select(entity => entity.Name);
        }
    }

    private sealed class CombinedSpec : Specification<TestEntity>
    {
        public CombinedSpec(params Specification<TestEntity>[] specifications)
        {
            CombineWith(specifications);
        }
    }

    private sealed class TaggedSpec : Specification<TestEntity>
    {
        public TaggedSpec(string tag)
        {
            TagWith(tag);
        }
    }
}
