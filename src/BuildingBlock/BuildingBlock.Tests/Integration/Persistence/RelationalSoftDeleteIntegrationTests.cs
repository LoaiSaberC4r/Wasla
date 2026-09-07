using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

public sealed class RelationalSoftDeleteIntegrationTests
{
    [Fact]
    public async Task Soft_deleted_repositories_open_only_soft_delete_filter_and_keep_other_filters_active()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        var visibleId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();
        var deletedVisibleId = Guid.NewGuid();
        var deletedHiddenId = Guid.NewGuid();

        context.Aggregates.AddRange(
            NewAggregate(visibleId, "visible", "Visible"),
            NewAggregate(hiddenId, "hidden", "Hidden", isVisible: false),
            NewAggregate(deletedVisibleId, "deleted-visible", "Deleted Visible", isDeleted: true),
            NewAggregate(deletedHiddenId, "deleted-hidden", "Deleted Hidden", isDeleted: true, isVisible: false));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var normalRead = new EfReadRepository<TestAggregate, TestReadMarker>(fixture.CreateReadResolver(context));
        var deletedRead = new EfSoftDeletedReadRepository<TestAggregate, TestReadMarker>(
            fixture.CreateReadResolver(context));
        var deletedWrite = new EfSoftDeletedWriteRepository<TestAggregate, TestWriteMarker>(
            fixture.CreateWriteResolver(context));

        var normalResult = await normalRead.GetByIdAsync(deletedVisibleId, TestContext.Current.CancellationToken);
        var normalList = await normalRead.ListAsync(new AllAggregatesSpec(), TestContext.Current.CancellationToken);
        var activeHiddenAsDeleted = await deletedRead.GetDeletedByIdAsync(hiddenId, TestContext.Current.CancellationToken);
        var deletedResult = await deletedRead.GetDeletedByIdAsync(deletedVisibleId, TestContext.Current.CancellationToken);
        var deletedList = await deletedRead.ListDeletedAsync(new AllAggregatesSpec(), TestContext.Current.CancellationToken);
        var hiddenDeleted = await deletedRead.GetDeletedByIdAsync(deletedHiddenId, TestContext.Current.CancellationToken);
        var trackedDeleted = await deletedWrite.GetDeletedTrackedByIdAsync(deletedVisibleId, TestContext.Current.CancellationToken);

        Assert.Null(normalResult);
        Assert.Equal(new[] { "Visible" }, normalList.Select(entity => entity.Name));
        Assert.Null(activeHiddenAsDeleted);
        Assert.Equal("Deleted Visible", deletedResult?.Name);
        Assert.Equal(new[] { "Deleted Visible" }, deletedList.Select(entity => entity.Name));
        Assert.Null(hiddenDeleted);
        Assert.Equal(EntityState.Unchanged, context.Entry(trackedDeleted!).State);
    }

    [Fact]
    public async Task Restore_and_delete_lifecycle_updates_visibility_and_timestamps_with_relational_queries()
    {
        var deletedAt = Utc(2026, 7, 16, 9);
        var restoredAt = Utc(2026, 7, 16, 10);
        var deletedAgainAt = Utc(2026, 7, 16, 11);
        var clock = new ManualClock(restoredAt);
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        var aggregateId = Guid.NewGuid();

        await using var context = fixture.CreateWriteContext(builder =>
            builder.AddInterceptors(new SoftDeleteEntitiesInterceptor(clock)));
        context.Aggregates.Add(NewAggregate(
            aggregateId,
            "lifecycle",
            "Lifecycle",
            isDeleted: true,
            deletedOnUtc: deletedAt));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var normalRead = new EfReadRepository<TestAggregate, TestReadMarker>(fixture.CreateReadResolver(context));
        var deletedWrite = new EfSoftDeletedWriteRepository<TestAggregate, TestWriteMarker>(
            fixture.CreateWriteResolver(context));

        var deleted = await deletedWrite.GetDeletedTrackedByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        deleted!.IsDeleted = false;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var restored = await normalRead.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        Assert.Equal("Lifecycle", restored?.Name);
        Assert.Equal(deletedAt, restored?.DeletedOnUtc);
        Assert.Equal(restoredAt, restored?.RestoredOnUtc);

        clock.UtcNow = deletedAgainAt;
        var trackedRestored = await deletedWrite.GetDeletedTrackedByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        Assert.Null(trackedRestored);

        var writeRepository = new EfWriteRepository<TestAggregate, TestWriteMarker>(
            fixture.CreateWriteResolver(context));
        var visible = await writeRepository.GetByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        writeRepository.Delete(visible!);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var deletedAgain = await deletedWrite.GetDeletedTrackedByIdAsync(aggregateId, TestContext.Current.CancellationToken);
        Assert.True(deletedAgain?.IsDeleted);
        Assert.Equal(deletedAgainAt, deletedAgain?.DeletedOnUtc);
        Assert.Null(deletedAgain?.RestoredOnUtc);
    }

    private static TestAggregate NewAggregate(
        Guid id,
        string externalId,
        string name,
        bool isDeleted = false,
        bool isVisible = true,
        DateTime? deletedOnUtc = null)
        => new()
        {
            Id = id,
            ExternalId = externalId,
            Name = name,
            IsDeleted = isDeleted,
            IsVisible = isVisible,
            DeletedOnUtc = deletedOnUtc
        };

    private static DateTime Utc(int year, int month, int day, int hour)
        => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed class AllAggregatesSpec : Specification<TestAggregate>
    {
    }

    private sealed class ManualClock : IDateTimeProvider
    {
        public ManualClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
