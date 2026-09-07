using BuildingBlock.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    public DbSet<TestChild> Children => Set<TestChild>();

    public DbSet<TestProfile> Profiles => Set<TestProfile>();

    public DbSet<TestProfileDetail> ProfileDetails => Set<TestProfileDetail>();

    public DbSet<TestCompositeAggregate> CompositeAggregates => Set<TestCompositeAggregate>();

    public DbSet<TestReadModel> ReadModels => Set<TestReadModel>();

    public DbSet<OtherTestReadModel> OtherReadModels => Set<OtherTestReadModel>();

    public DbSet<TestCompositeReadModel> CompositeReadModels => Set<TestCompositeReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        TestWriteDbContext.ConfigureWriteModel(modelBuilder);
        TestReadDbContext.ConfigureReadModel(modelBuilder);
    }
}
