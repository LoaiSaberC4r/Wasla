using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlock.Tests;

public sealed class Phase3EntityConfigurationTests
{
    [Fact]
    public void Read_and_write_configuration_scanners_apply_only_matching_typed_contracts()
    {
        WriteConfiguredEntityConfiguration.AppliedCount = 0;
        ReadConfiguredEntityConfiguration.AppliedCount = 0;

        using var writeContext = new WriteConfigurationContext(CreateOptions<WriteConfigurationContext>());
        using var readContext = new ReadConfigurationContext(CreateOptions<ReadConfigurationContext>());

        var writeEntity = writeContext.Model.FindEntityType(typeof(WriteConfiguredEntity))!;
        var writeSideReadEntity = writeContext.Model.FindEntityType(typeof(ReadConfiguredEntity))!;
        var readEntity = readContext.Model.FindEntityType(typeof(ReadConfiguredEntity))!;
        var readSideWriteEntity = readContext.Model.FindEntityType(typeof(WriteConfiguredEntity))!;

        Assert.Equal("write_entities", writeEntity.GetTableName());
        Assert.Null(writeSideReadEntity.FindProperty(nameof(ReadConfiguredEntity.Description))!.GetMaxLength());

        Assert.Equal("read_entities", readEntity.GetTableName());
        Assert.Null(readSideWriteEntity.FindProperty(nameof(WriteConfiguredEntity.Name))!.GetMaxLength());

        Assert.Equal(1, WriteConfiguredEntityConfiguration.AppliedCount);
        Assert.Equal(1, ReadConfiguredEntityConfiguration.AppliedCount);
    }

    [Fact]
    public void Configuration_contracts_are_strongly_typed_entity_type_configurations()
    {
        Assert.True(typeof(IEntityTypeConfiguration<WriteConfiguredEntity>)
            .IsAssignableFrom(typeof(WriteConfiguredEntityConfiguration)));
        Assert.True(typeof(IEntityTypeConfiguration<ReadConfiguredEntity>)
            .IsAssignableFrom(typeof(ReadConfiguredEntityConfiguration)));

        Assert.Contains(
            typeof(IEntityTypeConfiguration<>),
            typeof(IWriteEntityConfiguration<>).GetInterfaces().Select(type => type.GetGenericTypeDefinition()));
        Assert.Contains(
            typeof(IEntityTypeConfiguration<>),
            typeof(IReadEntityConfiguration<>).GetInterfaces().Select(type => type.GetGenericTypeDefinition()));
    }

    private static DbContextOptions<TContext> CreateOptions<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private sealed class WriteConfigurationContext : DbContext
    {
        public WriteConfigurationContext(DbContextOptions<WriteConfigurationContext> options)
            : base(options)
        {
        }

        public DbSet<WriteConfiguredEntity> WriteEntities => Set<WriteConfiguredEntity>();

        public DbSet<ReadConfiguredEntity> ReadEntities => Set<ReadConfiguredEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WriteConfiguredEntity>().HasKey(entity => entity.Id);
            modelBuilder.Entity<ReadConfiguredEntity>().HasKey(entity => entity.Id);
            modelBuilder.ApplyWriteConfigurations(typeof(Phase3EntityConfigurationTests).Assembly);
        }
    }

    private sealed class ReadConfigurationContext : DbContext
    {
        public ReadConfigurationContext(DbContextOptions<ReadConfigurationContext> options)
            : base(options)
        {
        }

        public DbSet<WriteConfiguredEntity> WriteEntities => Set<WriteConfiguredEntity>();

        public DbSet<ReadConfiguredEntity> ReadEntities => Set<ReadConfiguredEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WriteConfiguredEntity>().HasKey(entity => entity.Id);
            modelBuilder.Entity<ReadConfiguredEntity>().HasKey(entity => entity.Id);
            modelBuilder.ApplyReadConfigurations(typeof(Phase3EntityConfigurationTests).Assembly);
        }
    }

    private sealed class WriteConfiguredEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ReadConfiguredEntity
    {
        public int Id { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    private sealed class WriteConfiguredEntityConfiguration : IWriteEntityConfiguration<WriteConfiguredEntity>
    {
        public static int AppliedCount { get; set; }

        public void ConfigureAggregate(EntityTypeBuilder<WriteConfiguredEntity> builder)
        {
            AppliedCount++;
            builder.ToTable("write_entities");
            builder.Property(entity => entity.Name).HasMaxLength(32);
        }
    }

    private sealed class ReadConfiguredEntityConfiguration : IReadEntityConfiguration<ReadConfiguredEntity>
    {
        public static int AppliedCount { get; set; }

        public void ConfigureReadModel(EntityTypeBuilder<ReadConfiguredEntity> builder)
        {
            AppliedCount++;
            builder.ToTable("read_entities");
            builder.Property(entity => entity.Description).HasMaxLength(64);
        }
    }

    private abstract class AbstractWriteConfiguration : IWriteEntityConfiguration<WriteConfiguredEntity>
    {
        public void ConfigureAggregate(EntityTypeBuilder<WriteConfiguredEntity> builder)
            => throw new InvalidOperationException("Abstract configurations must not be applied.");
    }

    private sealed class OpenGenericWriteConfiguration<TEntity> : IWriteEntityConfiguration<TEntity>
        where TEntity : class
    {
        public void ConfigureAggregate(EntityTypeBuilder<TEntity> builder)
            => throw new InvalidOperationException("Open generic configurations must not be applied.");
    }
}
