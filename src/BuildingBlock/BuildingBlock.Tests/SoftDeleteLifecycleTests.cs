using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests;

public sealed class SoftDeleteLifecycleTests
{
    [Fact]
    public async Task Delete_restore_delete_restore_updates_lifecycle_and_audit_timestamps()
    {
        var createdAt = Utc(2026, 1, 1, 10);
        var firstDeleteAt = Utc(2026, 1, 1, 11);
        var firstRestoreAt = Utc(2026, 1, 1, 12);
        var secondDeleteAt = Utc(2026, 1, 1, 13);
        var secondRestoreAt = Utc(2026, 1, 1, 14);
        var clock = new ManualClock(createdAt);

        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, clock);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new LifecycleEntity { Id = 1, Name = "Entity" };
        db.Entities.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedOnUtc);
        Assert.Null(entity.RestoredOnUtc);
        Assert.Equal(createdAt, entity.CreatedOnUtc);
        Assert.Null(entity.ModifiedOnUtc);

        clock.UtcNow = firstDeleteAt;
        db.Entities.Remove(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(entity.IsDeleted);
        Assert.Equal(firstDeleteAt, entity.DeletedOnUtc);
        Assert.Null(entity.RestoredOnUtc);
        Assert.Equal(createdAt, entity.CreatedOnUtc);
        Assert.Equal(firstDeleteAt, entity.ModifiedOnUtc);

        clock.UtcNow = firstRestoreAt;
        entity.IsDeleted = false;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(entity.IsDeleted);
        Assert.Equal(firstDeleteAt, entity.DeletedOnUtc);
        Assert.Equal(firstRestoreAt, entity.RestoredOnUtc);
        Assert.Equal(createdAt, entity.CreatedOnUtc);
        Assert.Equal(firstRestoreAt, entity.ModifiedOnUtc);

        clock.UtcNow = secondDeleteAt;
        entity.IsDeleted = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(entity.IsDeleted);
        Assert.Equal(secondDeleteAt, entity.DeletedOnUtc);
        Assert.Null(entity.RestoredOnUtc);
        Assert.Equal(createdAt, entity.CreatedOnUtc);
        Assert.Equal(secondDeleteAt, entity.ModifiedOnUtc);

        clock.UtcNow = secondRestoreAt;
        entity.IsDeleted = false;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(entity.IsDeleted);
        Assert.Equal(secondDeleteAt, entity.DeletedOnUtc);
        Assert.Equal(secondRestoreAt, entity.RestoredOnUtc);
        Assert.Equal(createdAt, entity.CreatedOnUtc);
        Assert.Equal(secondRestoreAt, entity.ModifiedOnUtc);
    }

    [Fact]
    public async Task Unrelated_update_and_same_value_assignment_do_not_change_delete_timestamps()
    {
        var createdAt = Utc(2026, 2, 1, 10);
        var deleteAt = Utc(2026, 2, 1, 11);
        var restoreAt = Utc(2026, 2, 1, 12);
        var updateAt = Utc(2026, 2, 1, 13);
        var sameValueAt = Utc(2026, 2, 1, 14);
        var clock = new ManualClock(createdAt);

        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, clock);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new LifecycleEntity { Id = 1, Name = "Entity" };
        db.Entities.Add(entity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.UtcNow = deleteAt;
        entity.IsDeleted = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.UtcNow = restoreAt;
        entity.IsDeleted = false;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        clock.UtcNow = updateAt;
        entity.Name = "Renamed";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(deleteAt, entity.DeletedOnUtc);
        Assert.Equal(restoreAt, entity.RestoredOnUtc);
        Assert.Equal(updateAt, entity.ModifiedOnUtc);

        clock.UtcNow = sameValueAt;
        db.Entry(entity).Property(item => item.IsDeleted).IsModified = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(deleteAt, entity.DeletedOnUtc);
        Assert.Equal(restoreAt, entity.RestoredOnUtc);
    }

    private static LifecycleDbContext CreateContext(SqliteConnection connection, IDateTimeProvider clock)
        => new(new DbContextOptionsBuilder<LifecycleDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new SoftDeleteEntitiesInterceptor(clock),
                new AuditableEntitiesInterceptor(clock))
            .Options);

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static DateTime Utc(int year, int month, int day, int hour)
        => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed class LifecycleDbContext : DbContext
    {
        public LifecycleDbContext(DbContextOptions<LifecycleDbContext> options)
            : base(options)
        {
        }

        public DbSet<LifecycleEntity> Entities => Set<LifecycleEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LifecycleEntity>().HasKey(entity => entity.Id);
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private sealed class LifecycleEntity : ISoftDeleteEntity, IAuditableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public DateTime? ModifiedOnUtc { get; set; }
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
