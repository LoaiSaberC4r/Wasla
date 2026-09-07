using BuildingBlock.Domain.Enums;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.SpecificationEvaluator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Tests;

public sealed class Phase3SpecificationTests
{
    [Fact]
    public void Value_type_ordering_keeps_typed_key_selector_without_boxing_convert()
    {
        var specification = new IntegerOrderSpec();

        var keySelector = Assert.Single(specification.OrderExpressions).KeySelector;

        Assert.Equal(typeof(int), keySelector.ReturnType);
        Assert.Equal(ExpressionType.MemberAccess, keySelector.Body.NodeType);
    }

    [Fact]
    public void Ordering_supports_strings_dates_descending_and_then_by_sequence()
    {
        var items = new[]
        {
            new OrderedEntity { Name = "B", Rank = 1, CreatedOnUtc = new DateTime(2026, 1, 2) },
            new OrderedEntity { Name = "A", Rank = 2, CreatedOnUtc = new DateTime(2026, 1, 1) },
            new OrderedEntity { Name = "A", Rank = 1, CreatedOnUtc = new DateTime(2026, 1, 3) }
        };

        var ordered = ApplyOrdering(items, new MultiOrderSpec());

        Assert.Equal(
            new[] { "A:2:2026-01-01", "A:1:2026-01-03", "B:1:2026-01-02" },
            ordered.Select(entity => $"{entity.Name}:{entity.Rank}:{entity.CreatedOnUtc:yyyy-MM-dd}"));
    }

    [Fact]
    public async Task Projected_specifications_preserve_ordering()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateIncludeContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.OrderedEntities.AddRange(
            new OrderedEntity { Id = 1, Name = "B", Rank = 1 },
            new OrderedEntity { Id = 2, Name = "A", Rank = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var names = await SpecificationEvaluator<OrderedEntity>
            .BuildQuery(context.OrderedEntities, new OrderedNameProjectionSpec())
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "A", "B" }, names);
    }

    [Fact]
    public async Task Includes_support_reference_collection_and_nested_reference_paths()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateIncludeContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        context.Parents.Add(new IncludeParent
        {
            Id = 1,
            Name = "Parent",
            Profile = new IncludeProfile
            {
                Id = 10,
                Detail = new IncludeProfileDetail { Id = 100, Notes = "Nested" }
            },
            Children =
            {
                new IncludeChild { Id = 20, Name = "Child" }
            }
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var query = SpecificationEvaluator<IncludeParent>.BuildQuery(
            context.Parents,
            new IncludeGraphSpec());
        var parent = await query.SingleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(parent.Profile);
        Assert.NotNull(parent.Profile.Detail);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void Duplicate_includes_are_deduplicated_and_count_query_ignores_includes()
    {
        var specification = new DuplicateIncludeSpec();

        Assert.Single(specification.IncludeExpressions);

        var countQuery = SpecificationEvaluator<IncludeParent>.BuildCountQuery(
            Array.Empty<IncludeParent>().AsQueryable(),
            specification);

        Assert.DoesNotContain("Include(", countQuery.Expression.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_combines_criteria_without_invocation_and_appends_ordering()
    {
        var specification = new CombinedOrderedSpec(
            new NameCriteriaSpec("A"),
            new ActiveCriteriaSpec(),
            new StringOrderSpec(),
            new IntegerDescendingOrderSpec());

        var compiled = specification.Criteria.Compile();

        Assert.True(compiled(new OrderedEntity { Name = "A", IsActive = true }));
        Assert.False(compiled(new OrderedEntity { Name = "A", IsActive = false }));
        Assert.False(ContainsInvocation(specification.Criteria));
        Assert.Equal(2, specification.OrderExpressions.Count);
    }

    [Fact]
    public void Composition_rejects_conflicting_paging_and_query_shape()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CombinedOrderedSpec(new PagingSpec(1, 10), new PagingSpec(2, 10)));

        Assert.Throws<InvalidOperationException>(() =>
            new CombinedOrderedSpec(new SplitQuerySpec(), new SingleQuerySpec()));
    }

    [Fact]
    public void Composition_preserves_tags_tracking_and_distinct_rules()
    {
        var specification = new CombinedOrderedSpec(
            new TaggedSpec("first"),
            new TaggedSpec("second"),
            new IdentityResolutionSpec(),
            new TrackingSpec(),
            new DistinctSpec());

        Assert.Equal(new[] { "first", "second" }, specification.QueryTags);
        Assert.Equal(TrackingBehavior.TrackAll, specification.Tracking);
        Assert.True(specification.IsDistinct);
    }

    [Theory]
    [InlineData(0, 10, 200)]
    [InlineData(-1, 10, 200)]
    [InlineData(1, 0, 200)]
    [InlineData(1, 201, 200)]
    [InlineData(1, 10, 0)]
    public void Paging_rejects_invalid_values(int pageNumber, int pageSize, int maximumPageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PagingSpec(pageNumber, pageSize, maximumPageSize));
    }

    [Fact]
    public void Paging_allows_custom_maximum_and_rejects_offset_overflow()
    {
        var specification = new PagingSpec(3, 250, 500);

        Assert.Equal(500, specification.MaximumPageSize);
        Assert.Equal(500, specification.Skip);
        Assert.Equal(250, specification.Take);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PagingSpec(int.MaxValue, 200, 200));
    }

    private static IReadOnlyList<OrderedEntity> ApplyOrdering(
        IEnumerable<OrderedEntity> items,
        Specification<OrderedEntity> specification)
    {
        var query = items.AsQueryable();
        IOrderedQueryable<OrderedEntity>? ordered = null;

        foreach (var orderExpression in specification.OrderExpressions)
        {
            ordered = ordered is null
                ? orderExpression.ApplyInitial(query)
                : orderExpression.ApplyThen(ordered);
        }

        return (ordered ?? query).ToArray();
    }

    private static bool ContainsInvocation(LambdaExpression expression)
    {
        var visitor = new InvocationVisitor();
        visitor.Visit(expression);
        return visitor.ContainsInvocation;
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static IncludeDbContext CreateIncludeContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<IncludeDbContext>()
            .UseSqlite(connection)
            .Options);

    private sealed class InvocationVisitor : ExpressionVisitor
    {
        public bool ContainsInvocation { get; private set; }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            ContainsInvocation = true;
            return base.VisitInvocation(node);
        }
    }

    private sealed class OrderedEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Rank { get; set; }

        public DateTime CreatedOnUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }

    private sealed class IncludeParent
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public IncludeProfile? Profile { get; set; }

        public List<IncludeChild> Children { get; set; } = new();
    }

    private sealed class IncludeProfile
    {
        public int Id { get; set; }

        public int IncludeParentId { get; set; }

        public IncludeProfileDetail? Detail { get; set; }
    }

    private sealed class IncludeProfileDetail
    {
        public int Id { get; set; }

        public int IncludeProfileId { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    private sealed class IncludeChild
    {
        public int Id { get; set; }

        public int IncludeParentId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class IncludeDbContext : DbContext
    {
        public IncludeDbContext(DbContextOptions<IncludeDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrderedEntity> OrderedEntities => Set<OrderedEntity>();

        public DbSet<IncludeParent> Parents => Set<IncludeParent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderedEntity>().HasKey(entity => entity.Id);

            modelBuilder.Entity<IncludeParent>(builder =>
            {
                builder.HasKey(entity => entity.Id);
                builder.HasOne(entity => entity.Profile)
                    .WithOne()
                    .HasForeignKey<IncludeProfile>(entity => entity.IncludeParentId);
                builder.HasMany(entity => entity.Children)
                    .WithOne()
                    .HasForeignKey(entity => entity.IncludeParentId);
            });

            modelBuilder.Entity<IncludeProfile>(builder =>
            {
                builder.HasKey(entity => entity.Id);
                builder.HasOne(entity => entity.Detail)
                    .WithOne()
                    .HasForeignKey<IncludeProfileDetail>(entity => entity.IncludeProfileId);
            });

            modelBuilder.Entity<IncludeProfileDetail>().HasKey(entity => entity.Id);
            modelBuilder.Entity<IncludeChild>().HasKey(entity => entity.Id);
        }
    }

    private sealed class IntegerOrderSpec : Specification<OrderedEntity>
    {
        public IntegerOrderSpec()
        {
            AddOrderBy(entity => entity.Rank);
        }
    }

    private sealed class IntegerDescendingOrderSpec : Specification<OrderedEntity>
    {
        public IntegerDescendingOrderSpec()
        {
            AddOrderByDescending(entity => entity.Rank);
        }
    }

    private sealed class StringOrderSpec : Specification<OrderedEntity>
    {
        public StringOrderSpec()
        {
            AddOrderBy(entity => entity.Name);
        }
    }

    private sealed class MultiOrderSpec : Specification<OrderedEntity>
    {
        public MultiOrderSpec()
        {
            AddOrderBy(entity => entity.Name);
            AddOrderByDescending(entity => entity.Rank);
            AddOrderBy(entity => entity.CreatedOnUtc);
        }
    }

    private sealed class OrderedNameProjectionSpec : Specification<OrderedEntity, string>
    {
        public OrderedNameProjectionSpec()
        {
            AddOrderBy(entity => entity.Name);
            Select(entity => entity.Name);
        }
    }

    private sealed class IncludeGraphSpec : Specification<IncludeParent>
    {
        public IncludeGraphSpec()
        {
            Include(entity => entity.Profile!.Detail);
            Include(entity => entity.Children);
        }
    }

    private sealed class DuplicateIncludeSpec : Specification<IncludeParent>
    {
        public DuplicateIncludeSpec()
        {
            Include(entity => entity.Children);
            Include(parent => parent.Children);
        }
    }

    private sealed class NameCriteriaSpec : Specification<OrderedEntity>
    {
        public NameCriteriaSpec(string name)
        {
            AddCriteria(entity => entity.Name == name);
        }
    }

    private sealed class ActiveCriteriaSpec : Specification<OrderedEntity>
    {
        public ActiveCriteriaSpec()
        {
            AddCriteria(entity => entity.IsActive);
        }
    }

    private sealed class PagingSpec : Specification<OrderedEntity>
    {
        public PagingSpec(int pageNumber, int pageSize, int maximumPageSize = SpecificationDefaults.DefaultMaximumPageSize)
        {
            ApplyPaging(pageNumber, pageSize, maximumPageSize);
        }
    }

    private sealed class SplitQuerySpec : Specification<OrderedEntity>
    {
        public SplitQuerySpec()
        {
            UseSplitQuery();
        }
    }

    private sealed class SingleQuerySpec : Specification<OrderedEntity>
    {
        public SingleQuerySpec()
        {
            UseSingleQuery();
        }
    }

    private sealed class TaggedSpec : Specification<OrderedEntity>
    {
        public TaggedSpec(string tag)
        {
            TagWith(tag);
        }
    }

    private sealed class IdentityResolutionSpec : Specification<OrderedEntity>
    {
        public IdentityResolutionSpec()
        {
            UseNoTrackingWithIdentityResolution();
        }
    }

    private sealed class TrackingSpec : Specification<OrderedEntity>
    {
        public TrackingSpec()
        {
            UseTracking();
        }
    }

    private sealed class DistinctSpec : Specification<OrderedEntity>
    {
        public DistinctSpec()
        {
            EnableDistinct();
        }
    }

    private sealed class CombinedOrderedSpec : Specification<OrderedEntity>
    {
        public CombinedOrderedSpec(params Specification<OrderedEntity>[] specifications)
        {
            CombineWith(specifications);
        }
    }
}
