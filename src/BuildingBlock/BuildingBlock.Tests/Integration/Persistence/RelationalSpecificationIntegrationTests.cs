using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.SpecificationEvaluator;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Tests.Integration.Persistence;

public sealed class RelationalSpecificationIntegrationTests
{
    [Fact]
    public async Task Specifications_translate_criteria_ordering_paging_includes_split_query_and_tags()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        await SeedSpecificationDataAsync(context);
        context.ChangeTracker.Clear();

        var specification = new IncludedPagedMatchSpec();
        var query = SpecificationEvaluator<TestAggregate>.BuildQuery(context.Aggregates, specification);
        var sql = query.ToQueryString();
        var results = await query.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("phase-five-specification", sql);
        Assert.Single(results);
        Assert.Equal("Match A", results[0].Name);
        Assert.Single(results[0].Children);
        Assert.NotNull(results[0].Profile?.Detail);
        Assert.Empty(context.ChangeTracker.Entries<TestAggregate>());
    }

    [Fact]
    public async Task Projection_distinct_single_query_and_count_shape_translate_through_sqlite()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        await SeedSpecificationDataAsync(context);
        context.ChangeTracker.Clear();

        var projection = await SpecificationEvaluator<TestAggregate>
            .BuildQuery(context.Aggregates, new NameProjectionSpec())
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var distinctSql = SpecificationEvaluator<TestAggregate>
            .BuildQuery(context.Aggregates, new DistinctSingleQuerySpec())
            .ToQueryString();
        var countQuery = SpecificationEvaluator<TestAggregate>
            .BuildCountQuery(context.Aggregates, new CountShapeSpec());
        var countSql = countQuery.ToQueryString();
        var count = await countQuery.LongCountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Match A", "Match B" }, projection);
        Assert.Contains("DISTINCT", distinctSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2L, count);
        Assert.DoesNotContain("JOIN", countSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY", countSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIMIT", countSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composed_criteria_avoid_invocation_and_value_type_ordering_avoids_boxing()
    {
        var specification = new CombinedSpecification(
            new NameStartsWithSpec("Match"),
            new VersionOrderSpec());

        var keySelector = Assert.Single(specification.OrderExpressions).KeySelector;

        Assert.False(ContainsInvocation(specification.Criteria));
        Assert.Equal(typeof(int), keySelector.ReturnType);
        Assert.Equal(ExpressionType.MemberAccess, keySelector.Body.NodeType);
    }

    [Fact]
    public void Composition_conflicts_fail_explicitly_and_metadata_is_preserved()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CombinedSpecification(new PagingSpec(1, 10), new PagingSpec(2, 10)));
        Assert.Throws<InvalidOperationException>(() =>
            new CombinedSpecification(new SplitQuerySpec(), new SingleQuerySpec()));

        var composed = new CombinedSpecification(
            new TaggedSpec("first"),
            new TaggedSpec("second"),
            new DuplicateIncludeSpec(),
            new TrackingSpec());

        Assert.Equal(new[] { "first", "second" }, composed.QueryTags);
        Assert.Single(composed.IncludeExpressions);
    }

    private static async Task SeedSpecificationDataAsync(TestWriteDbContext context)
    {
        context.Aggregates.AddRange(
            NewAggregate("match-a", "Match A", version: 2),
            NewAggregate("match-b", "Match B", version: 1),
            NewAggregate("other", "Other", version: 3),
            NewAggregate("deleted", "Match Deleted", version: 4, isDeleted: true),
            NewAggregate("hidden", "Match Hidden", version: 5, isVisible: false));

        await context.SaveChangesAsync();
    }

    private static TestAggregate NewAggregate(
        string externalId,
        string name,
        int version,
        bool isDeleted = false,
        bool isVisible = true)
    {
        var aggregateId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        return new TestAggregate
        {
            Id = aggregateId,
            ExternalId = externalId,
            Name = name,
            Version = version,
            IsDeleted = isDeleted,
            IsVisible = isVisible,
            Children =
            {
                new TestChild
                {
                    Id = Guid.NewGuid(),
                    Label = $"{name} child"
                }
            },
            Profile = new TestProfile
            {
                Id = profileId,
                Summary = $"{name} profile",
                Detail = new TestProfileDetail
                {
                    Id = Guid.NewGuid(),
                    Notes = $"{name} detail"
                }
            }
        };
    }

    private static bool ContainsInvocation(LambdaExpression expression)
    {
        var visitor = new InvocationVisitor();
        visitor.Visit(expression);
        return visitor.ContainsInvocation;
    }

    private sealed class InvocationVisitor : ExpressionVisitor
    {
        public bool ContainsInvocation { get; private set; }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            ContainsInvocation = true;
            return base.VisitInvocation(node);
        }
    }

    private sealed class IncludedPagedMatchSpec : Specification<TestAggregate>
    {
        public IncludedPagedMatchSpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            Include(entity => entity.Children);
            Include(entity => entity.Profile!.Detail);
            AddOrderBy(entity => entity.Name);
            AddOrderByDescending(entity => entity.Version);
            ApplyPaging(1, 1);
            UseSplitQuery();
            TagWith("phase-five-specification");
        }
    }

    private sealed class NameProjectionSpec : Specification<TestAggregate, string>
    {
        public NameProjectionSpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            AddOrderBy(entity => entity.Name);
            Select(entity => entity.Name);
        }
    }

    private sealed class DistinctSingleQuerySpec : Specification<TestAggregate>
    {
        public DistinctSingleQuerySpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            Include(entity => entity.Profile);
            EnableDistinct();
            UseSingleQuery();
        }
    }

    private sealed class CountShapeSpec : Specification<TestAggregate>
    {
        public CountShapeSpec()
        {
            AddCriteria(entity => entity.Name.StartsWith("Match"));
            Include(entity => entity.Children);
            AddOrderByDescending(entity => entity.Name);
            ApplyPaging(1, 1);
        }
    }

    private sealed class NameStartsWithSpec : Specification<TestAggregate>
    {
        public NameStartsWithSpec(string prefix)
        {
            AddCriteria(entity => entity.Name.StartsWith(prefix));
        }
    }

    private sealed class VersionOrderSpec : Specification<TestAggregate>
    {
        public VersionOrderSpec()
        {
            AddOrderBy(entity => entity.Version);
        }
    }

    private sealed class PagingSpec : Specification<TestAggregate>
    {
        public PagingSpec(int pageNumber, int pageSize)
        {
            ApplyPaging(pageNumber, pageSize);
        }
    }

    private sealed class SplitQuerySpec : Specification<TestAggregate>
    {
        public SplitQuerySpec()
        {
            UseSplitQuery();
        }
    }

    private sealed class SingleQuerySpec : Specification<TestAggregate>
    {
        public SingleQuerySpec()
        {
            UseSingleQuery();
        }
    }

    private sealed class TaggedSpec : Specification<TestAggregate>
    {
        public TaggedSpec(string tag)
        {
            TagWith(tag);
        }
    }

    private sealed class DuplicateIncludeSpec : Specification<TestAggregate>
    {
        public DuplicateIncludeSpec()
        {
            Include(entity => entity.Children);
            Include(entity => entity.Children);
        }
    }

    private sealed class TrackingSpec : Specification<TestAggregate>
    {
        public TrackingSpec()
        {
            UseTracking();
        }
    }

    private sealed class CombinedSpecification : Specification<TestAggregate>
    {
        public CombinedSpecification(params Specification<TestAggregate>[] specifications)
        {
            CombineWith(specifications);
        }
    }
}
