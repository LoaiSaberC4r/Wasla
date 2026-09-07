using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

public sealed class RelationalPersistenceIntegrationTests
{
    [Fact]
    public async Task Sqlite_foundation_enforces_foreign_keys_and_isolates_test_databases()
    {
        await using var first = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var second = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);

        await using (var context = first.CreateWriteContext())
        {
            context.Children.Add(new TestChild
            {
                Id = Guid.NewGuid(),
                TestAggregateId = Guid.NewGuid(),
                Label = "Orphan"
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        var aggregateId = Guid.NewGuid();
        await using (var context = first.CreateWriteContext())
        {
            context.Aggregates.Add(NewAggregate(aggregateId, "first-db", "First database"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = second.CreateWriteContext())
        {
            Assert.False(await context.Aggregates.AnyAsync(entity => entity.Id == aggregateId, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Read_repository_uses_relational_queries_filters_projection_and_no_tracking()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        var alphaId = Guid.NewGuid();

        await using var context = fixture.CreateWriteContext();
        context.Aggregates.AddRange(
            NewAggregate(alphaId, "alpha", "Alpha"),
            NewAggregate(Guid.NewGuid(), "beta", "Beta"),
            NewAggregate(Guid.NewGuid(), "deleted", "Deleted", isDeleted: true),
            NewAggregate(Guid.NewGuid(), "hidden", "Hidden", isVisible: false));
        context.CompositeAggregates.Add(new TestCompositeAggregate
        {
            Partition = "A",
            LocalId = 1,
            Name = "Composite"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var repository = new EfReadRepository<TestAggregate, TestReadMarker>(fixture.CreateReadResolver(context));
        var compositeRepository = new EfReadRepository<TestCompositeAggregate, TestReadMarker>(
            fixture.CreateReadResolver(context));

        var byId = await repository.GetByIdAsync(alphaId, TestContext.Current.CancellationToken);
        var missing = await repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        var byPredicate = await repository.GetByPropertyAsync(entity => entity.Name == "Beta", TestContext.Current.CancellationToken);
        var anyAlpha = await repository.AnyAsync(entity => entity.ExternalId == "alpha", TestContext.Current.CancellationToken);
        var visibleCount = await repository.LongCountAsync(cancellationToken: TestContext.Current.CancellationToken);
        var ordered = await repository.ListAsync(new OrderedAggregateSpec(), TestContext.Current.CancellationToken);
        var names = await repository.ListAsync(new AggregateNameProjectionSpec(), TestContext.Current.CancellationToken);
        var first = await repository.FirstOrDefaultAsync(new ByNameSpec("Beta"), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Equal("Alpha", byId?.Name);
        Assert.Null(missing);
        Assert.Equal("Beta", byPredicate?.Name);
        Assert.True(anyAlpha);
        Assert.Equal(2L, visibleCount);
        Assert.Equal(new[] { "Alpha", "Beta" }, ordered.Select(entity => entity.Name));
        Assert.Equal(new[] { "Alpha", "Beta" }, names);
        Assert.Equal("Beta", first?.Name);
        Assert.Empty(context.ChangeTracker.Entries<TestAggregate>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.AnyAsync(entity => entity.Name == "Alpha", cancellation.Token));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            compositeRepository.GetByIdAsync("A", TestContext.Current.CancellationToken));
        Assert.Contains("composite primary key", exception.Message);
    }

    [Fact]
    public async Task Write_repository_and_unit_of_work_own_tracking_save_and_transaction_boundaries()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        var committedId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();

        await using (var context = fixture.CreateWriteContext())
        {
            var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
            var firstRepository = unitOfWork.WriteRepository<TestAggregate>();
            var secondRepository = unitOfWork.WriteRepository<TestAggregate>();
            var aggregate = NewAggregate(committedId, "committed", "Committed");

            Assert.Same(firstRepository, secondRepository);

            await firstRepository.AddAsync(aggregate, TestContext.Current.CancellationToken);
            Assert.False(await AggregateExistsAsync(fixture, committedId));

            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.True(await AggregateExistsAsync(fixture, committedId));

            context.ChangeTracker.Clear();
            var tracked = await firstRepository.GetByIdAsync(committedId, TestContext.Current.CancellationToken);
            Assert.NotNull(tracked);
            Assert.Equal(EntityState.Unchanged, context.Entry(tracked!).State);

            tracked.Name = "Updated";
            firstRepository.Update(tracked);
            Assert.Equal("Committed", await AggregateNameAsync(fixture, committedId));

            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Updated", await AggregateNameAsync(fixture, committedId));
        }

        await using (var context = fixture.CreateWriteContext())
        {
            var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
            await using var transaction = await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                NewAggregate(Guid.NewGuid(), "transaction-commit", "Transaction Commit"),
                TestContext.Current.CancellationToken);
            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = fixture.CreateWriteContext())
        {
            var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
            await using var transaction = await unitOfWork.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                NewAggregate(rolledBackId, "transaction-rollback", "Transaction Rollback"),
                TestContext.Current.CancellationToken);
            await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.False(await AggregateExistsAsync(fixture, rolledBackId));
        Assert.Throws<ArgumentException>(() =>
            typeof(IWriteRepository<,>).MakeGenericType(typeof(TestChild), typeof(TestWriteMarker)));
    }

    [Fact]
    public async Task Write_repository_deletes_only_when_unit_of_work_saves()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        var aggregateId = Guid.NewGuid();

        await using var context = fixture.CreateWriteContext();
        context.Aggregates.Add(NewAggregate(aggregateId, "delete-boundary", "Delete Boundary"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
        var repository = unitOfWork.WriteRepository<TestAggregate>();
        var aggregate = await repository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken);

        repository.Delete(aggregate!);
        Assert.True(await AggregateExistsAsync(fixture, aggregateId));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False(await AggregateExistsAsync(fixture, aggregateId));
    }

    [Fact]
    public async Task Read_model_writer_is_marker_aware_and_uses_one_read_unit_of_work_boundary()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateReadContext();
        var unitOfWork = new EfReadModelUnitOfWork<TestReadMarker>(fixture.CreateReadResolver(context));
        var writer = unitOfWork.Writer<TestReadModel>();

        await writer.AddAsync(new TestReadModel { Id = 1, Name = "Projection" }, TestContext.Current.CancellationToken);
        await unitOfWork.Writer<OtherTestReadModel>().AddAsync(new OtherTestReadModel { Id = 2, Name = "Other" }, TestContext.Current.CancellationToken);

        Assert.False(await ReadModelExistsAsync(fixture, 1));
        Assert.False(await OtherReadModelExistsAsync(fixture, 2));

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await ReadModelExistsAsync(fixture, 1));
        Assert.True(await OtherReadModelExistsAsync(fixture, 2));

        context.ChangeTracker.Clear();
        context.ReadModels.Add(new TestReadModel { Id = 3, Name = "Hidden", IsVisible = false });
        context.CompositeReadModels.Add(new TestCompositeReadModel
        {
            Partition = "A",
            LocalId = 1,
            Name = "Composite"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var visible = await writer.FindByIdAsync(1, TestContext.Current.CancellationToken);
        var hidden = await writer.FindByIdAsync(3, TestContext.Current.CancellationToken);
        var hiddenExists = await writer.AnyAsync(entity => entity.Id == 3, TestContext.Current.CancellationToken);
        var compositeWriter = unitOfWork.Writer<TestCompositeReadModel>();
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => compositeWriter.FindByIdAsync("A", TestContext.Current.CancellationToken));

        Assert.Equal("Projection", visible?.Name);
        Assert.Null(hidden);
        Assert.False(hiddenExists);
        Assert.Contains("composite primary key", exception.Message);
    }

    [Fact]
    public async Task Read_and_write_persistence_markers_resolve_isolated_contexts()
    {
        await using var first = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var second = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var firstContext = first.CreateReadContext();
        await using var secondContext = second.CreateReadContext();

        var firstUnitOfWork = new EfReadModelUnitOfWork<TestReadMarker>(new TestReadResolver(firstContext));
        var secondUnitOfWork = new EfReadModelUnitOfWork<OtherTestReadMarker>(new OtherTestReadResolver(secondContext));

        await firstUnitOfWork.Writer<TestReadModel>().AddAsync(new TestReadModel { Id = 1, Name = "First" }, TestContext.Current.CancellationToken);
        await secondUnitOfWork.Writer<TestReadModel>().AddAsync(new TestReadModel { Id = 1, Name = "Second" }, TestContext.Current.CancellationToken);
        await firstUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
        await secondUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("First", await ReadModelNameAsync(first, 1));
        Assert.Equal("Second", await ReadModelNameAsync(second, 1));
    }

    private static TestAggregate NewAggregate(
        Guid id,
        string externalId,
        string name,
        bool isDeleted = false,
        bool isVisible = true)
        => new()
        {
            Id = id,
            ExternalId = externalId,
            Name = name,
            IsDeleted = isDeleted,
            IsVisible = isVisible
        };

    private static async Task<bool> AggregateExistsAsync(PersistenceTestFixture fixture, Guid id)
    {
        await using var context = fixture.CreateWriteContext();
        return await context.Aggregates.AnyAsync(entity => entity.Id == id);
    }

    private static async Task<string?> AggregateNameAsync(PersistenceTestFixture fixture, Guid id)
    {
        await using var context = fixture.CreateWriteContext();
        return await context.Aggregates
            .Where(entity => entity.Id == id)
            .Select(entity => entity.Name)
            .SingleOrDefaultAsync();
    }

    private static async Task<bool> ReadModelExistsAsync(PersistenceTestFixture fixture, int id)
    {
        await using var context = fixture.CreateReadContext();
        return await context.ReadModels.AnyAsync(entity => entity.Id == id);
    }

    private static async Task<bool> OtherReadModelExistsAsync(PersistenceTestFixture fixture, int id)
    {
        await using var context = fixture.CreateReadContext();
        return await context.OtherReadModels.AnyAsync(entity => entity.Id == id);
    }

    private static async Task<string?> ReadModelNameAsync(PersistenceTestFixture fixture, int id)
    {
        await using var context = fixture.CreateReadContext();
        return await context.ReadModels
            .Where(entity => entity.Id == id)
            .Select(entity => entity.Name)
            .SingleOrDefaultAsync();
    }

    private sealed class ByNameSpec : Specification<TestAggregate>
    {
        public ByNameSpec(string name)
        {
            AddCriteria(entity => entity.Name == name);
        }
    }

    private sealed class OrderedAggregateSpec : Specification<TestAggregate>
    {
        public OrderedAggregateSpec()
        {
            AddOrderBy(entity => entity.Name);
        }
    }

    private sealed class AggregateNameProjectionSpec : Specification<TestAggregate, string>
    {
        public AggregateNameProjectionSpec()
        {
            AddOrderBy(entity => entity.Name);
            Select(entity => entity.Name);
        }
    }
}
