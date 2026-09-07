using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Tests;

public sealed class Phase3QueryFilterCompositionTests
{
    [Fact]
    public void Existing_named_filter_is_preserved_and_repeated_application_is_idempotent()
    {
        using var context = Create<NamedFilterContext>();
        var filters = context.Model.FindEntityType(typeof(SoftRoot))!
            .GetDeclaredQueryFilters()
            .ToArray();

        Assert.Equal(2, filters.Length);
        Assert.Single(filters, filter => filter.Key == BuildingBlockQueryFilterNames.SoftDelete);
        Assert.Single(filters, filter => filter.Key == "Tests.Visibility");
    }

    [Fact]
    public void Existing_anonymous_filter_is_composed_without_expression_invoke()
    {
        using var context = Create<AnonymousFilterContext>();
        var filters = context.Model.FindEntityType(typeof(SoftRoot))!
            .GetDeclaredQueryFilters()
            .ToArray();

        Assert.Equal(2, filters.Length);
        Assert.All(filters, filter => Assert.False(filter.IsAnonymous));
        Assert.DoesNotContain(filters, filter => filter.Expression!.ToString().Contains("Invoke", StringComparison.Ordinal));
    }

    [Fact]
    public void Root_filter_covers_derived_types_while_owned_and_keyless_types_are_skipped()
    {
        using var context = Create<ShapeContext>();
        var root = context.Model.FindEntityType(typeof(SoftRoot))!;
        var derived = context.Model.FindEntityType(typeof(SoftDerived))!;
        var owned = context.Model.FindEntityType(typeof(OwnedSoft))!;
        var keyless = context.Model.FindEntityType(typeof(KeylessSoft))!;

        Assert.NotNull(root.FindDeclaredQueryFilter(BuildingBlockQueryFilterNames.SoftDelete));
        Assert.Null(derived.FindDeclaredQueryFilter(BuildingBlockQueryFilterNames.SoftDelete));
        Assert.True(owned.IsOwned());
        Assert.Empty(owned.GetDeclaredQueryFilters());
        Assert.Null(keyless.FindPrimaryKey());
        Assert.Empty(keyless.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Derived_only_soft_delete_mapping_is_rejected_clearly()
    {
        using var context = Create<InvalidDerivedContext>();

        var exception = Assert.Throws<InvalidOperationException>(() => _ = context.Model);

        Assert.Contains("derived entity type", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(ISoftDeleteEntity), exception.Message, StringComparison.Ordinal);
    }

    private static TContext Create<TContext>()
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    private sealed class NamedFilterContext : DbContext
    {
        public NamedFilterContext(DbContextOptions<NamedFilterContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftRoot>().HasKey(entity => entity.Id);
            modelBuilder.Entity<SoftRoot>().Ignore(entity => entity.Owned);
            modelBuilder.Entity<SoftRoot>()
                .HasQueryFilter("Tests.Visibility", entity => entity.Visible);
            modelBuilder.ApplySoftDeleteQueryFilter();
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private sealed class AnonymousFilterContext : DbContext
    {
        public AnonymousFilterContext(DbContextOptions<AnonymousFilterContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftRoot>().HasKey(entity => entity.Id);
            modelBuilder.Entity<SoftRoot>().Ignore(entity => entity.Owned);
            modelBuilder.Entity<SoftRoot>().HasQueryFilter(entity => entity.Visible);
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private sealed class ShapeContext : DbContext
    {
        public ShapeContext(DbContextOptions<ShapeContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftRoot>(builder =>
            {
                builder.HasKey(entity => entity.Id);
                builder.HasDiscriminator<string>("Kind");
                builder.OwnsOne(entity => entity.Owned);
            });
            modelBuilder.Entity<SoftDerived>();
            modelBuilder.Entity<KeylessSoft>().HasNoKey();
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private sealed class InvalidDerivedContext : DbContext
    {
        public InvalidDerivedContext(DbContextOptions<InvalidDerivedContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlainRoot>().HasKey(entity => entity.Id);
            modelBuilder.Entity<DerivedSoftOnly>();
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private class SoftRoot : ISoftDeleteEntity
    {
        public int Id { get; set; }
        public bool Visible { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
        public OwnedSoft Owned { get; set; } = new();
    }

    private sealed class SoftDerived : SoftRoot
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class OwnedSoft : ISoftDeleteEntity
    {
        public string Value { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
    }

    private sealed class KeylessSoft : ISoftDeleteEntity
    {
        public string Value { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
    }

    private class PlainRoot
    {
        public int Id { get; set; }
    }

    private sealed class DerivedSoftOnly : PlainRoot, ISoftDeleteEntity
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOnUtc { get; set; }
        public DateTime? RestoredOnUtc { get; set; }
    }
}
