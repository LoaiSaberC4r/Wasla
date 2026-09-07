using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class TestReadDbContext : DbContext
{
    public TestReadDbContext(DbContextOptions<TestReadDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestReadModel> ReadModels => Set<TestReadModel>();

    public DbSet<OtherTestReadModel> OtherReadModels => Set<OtherTestReadModel>();

    public DbSet<TestCompositeReadModel> CompositeReadModels => Set<TestCompositeReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => ConfigureReadModel(modelBuilder);

    internal static void ConfigureReadModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestReadModel>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Name).HasMaxLength(256);
            builder.HasQueryFilter(entity => entity.IsVisible);
        });

        modelBuilder.Entity<OtherTestReadModel>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<TestCompositeReadModel>(builder =>
        {
            builder.HasKey(entity => new { entity.Partition, entity.LocalId });
            builder.Property(entity => entity.Partition).HasMaxLength(64);
            builder.Property(entity => entity.Name).HasMaxLength(256);
        });
    }
}
