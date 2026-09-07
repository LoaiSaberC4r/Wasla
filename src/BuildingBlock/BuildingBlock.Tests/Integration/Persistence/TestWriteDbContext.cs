using BuildingBlock.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class TestWriteDbContext : DbContext
{
    public TestWriteDbContext(DbContextOptions<TestWriteDbContext> options)
        : base(options)
    {
    }

    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    public DbSet<TestChild> Children => Set<TestChild>();

    public DbSet<TestProfile> Profiles => Set<TestProfile>();

    public DbSet<TestProfileDetail> ProfileDetails => Set<TestProfileDetail>();

    public DbSet<TestCompositeAggregate> CompositeAggregates => Set<TestCompositeAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => ConfigureWriteModel(modelBuilder);

    internal static void ConfigureWriteModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAggregate>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.HasIndex(entity => entity.ExternalId).IsUnique();
            builder.Property(entity => entity.ExternalId).HasMaxLength(128);
            builder.Property(entity => entity.Name).HasMaxLength(256);
            builder.Property(entity => entity.Version).IsConcurrencyToken();
            builder.Ignore(entity => entity.DomainEvents);
            builder.HasMany(entity => entity.Children)
                .WithOne(child => child.Aggregate)
                .HasForeignKey(child => child.TestAggregateId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(entity => entity.Profile)
                .WithOne(profile => profile.Aggregate)
                .HasForeignKey<TestProfile>(profile => profile.TestAggregateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestChild>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Label).HasMaxLength(256);
        });

        modelBuilder.Entity<TestProfile>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Summary).HasMaxLength(256);
            builder.HasOne(entity => entity.Detail)
                .WithOne(detail => detail.Profile)
                .HasForeignKey<TestProfileDetail>(detail => detail.TestProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestProfileDetail>(builder =>
        {
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Notes).HasMaxLength(256);
        });

        modelBuilder.Entity<TestCompositeAggregate>(builder =>
        {
            builder.HasKey(entity => new { entity.Partition, entity.LocalId });
            builder.Property(entity => entity.Partition).HasMaxLength(64);
            builder.Property(entity => entity.Name).HasMaxLength(256);
        });

        modelBuilder.ApplySoftDeleteQueryFilter();
        modelBuilder.Entity<TestAggregate>()
            .HasQueryFilter("Tests.Visibility", entity => entity.IsVisible);
    }
}
