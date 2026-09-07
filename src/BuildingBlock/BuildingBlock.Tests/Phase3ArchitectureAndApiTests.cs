using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Bootstrap;
using BuildingBlock.Infrastructure.Persistence;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class Phase3ArchitectureAndApiTests
{
    [Fact]
    public void Aggregate_root_boundaries_are_expressed_as_generic_constraints()
    {
        Assert.Contains(
            typeof(IAggregateRoot),
            EntityTypeParameter(typeof(IWriteRepository<,>)).GetGenericParameterConstraints());
        Assert.Contains(
            typeof(IAggregateRoot),
            EntityTypeParameter(typeof(ISoftDeletedWriteRepository<,>)).GetGenericParameterConstraints());

        var writeRepositoryMethod = typeof(IUnitOfWork<>).GetMethod(nameof(IUnitOfWork<IWritePersistenceMarker>.WriteRepository))!;
        Assert.Contains(
            typeof(IAggregateRoot),
            writeRepositoryMethod.GetGenericArguments()[0].GetGenericParameterConstraints());

        Assert.DoesNotContain(
            typeof(IAggregateRoot),
            EntityTypeParameter(typeof(IReadRepository<,>)).GetGenericParameterConstraints());
        Assert.DoesNotContain(
            typeof(IAggregateRoot),
            EntityTypeParameter(typeof(IReadModelWriter<,>)).GetGenericParameterConstraints());

        Assert.IsAssignableFrom<IAggregateRoot>(new TestAggregateRoot(1));
        Assert.Throws<ArgumentException>(() =>
            typeof(IWriteRepository<,>).MakeGenericType(typeof(ChildEntity), typeof(TestWriteMarker)));
        Assert.Throws<ArgumentException>(() =>
            typeof(ISoftDeletedWriteRepository<,>).MakeGenericType(typeof(SoftDeletedChildEntity), typeof(TestWriteMarker)));
    }

    [Fact]
    public void Public_api_surface_keeps_generic_contracts_and_removes_non_generic_technical_types()
    {
        var applicationTypes = typeof(IReadPersistenceMarker).Assembly.GetExportedTypes();
        var domainTypes = typeof(IAggregateRoot).Assembly.GetExportedTypes();

        Assert.Contains(typeof(IReadModelWriter<,>), applicationTypes);
        Assert.Contains(typeof(IReadModelUnitOfWork<>), applicationTypes);
        Assert.Contains(typeof(IWriteRepository<,>), applicationTypes);
        Assert.Contains(typeof(IAggregateRoot), domainTypes);
        Assert.Contains(typeof(IWriteEntityConfiguration<>), typeof(ModelBuilderConfigExtensions).Assembly.GetExportedTypes());

        Assert.DoesNotContain(applicationTypes, type => type.FullName == "BuildingBlock.Application.Option.SmtpOptions");
        Assert.DoesNotContain(applicationTypes, type => type.Name is "IReadEntityConfiguration" or "IWriteEntityConfiguration");
        Assert.DoesNotContain(domainTypes, type => type.Name is "LanguageCode" or "OrderSort" or "SearchParameters");

        Assert.False(typeof(EfPrimaryKeyExpressionBuilder).IsPublic);
    }

    [Fact]
    public void Legacy_repository_is_removed_from_the_v2_public_api()
    {
        var applicationTypes = typeof(IReadRepository<,>).Assembly.GetExportedTypes();
        var infrastructureTypes = typeof(ModelBuilderConfigExtensions).Assembly.GetExportedTypes();

        Assert.DoesNotContain(applicationTypes, type => type.Namespace == "BuildingBlock.Application.Legacy");
        Assert.DoesNotContain(infrastructureTypes, type => type.Namespace == "BuildingBlock.Infrastructure.Legacy");
    }

    [Fact]
    public void Non_legacy_production_code_does_not_reference_legacy_repository()
    {
        var root = FindRepositoryRoot();
        var matches = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => IsProductionSource(path))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(item => item.line.Contains("IGenericRepository", StringComparison.Ordinal) ||
                item.line.Contains("IEfDbContextProvider", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.number}")
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void Project_references_preserve_clean_architecture_direction()
    {
        AssertNoReferences(
            typeof(IAggregateRoot).Assembly,
            "Serilog",
            "MailKit",
            "QRCoder",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "MediatR",
            "Microsoft.Data.SqlClient",
            "BuildingBlock.Application",
            "BuildingBlock.Infrastructure",
            "BuildingBlock.Api");

        AssertNoReferences(
            typeof(IReadPersistenceMarker).Assembly,
            "Serilog",
            "MailKit",
            "QRCoder",
            "BuildingBlock.Infrastructure",
            "BuildingBlock.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore.Http");

        AssertNoReferences(
            typeof(ModelBuilderConfigExtensions).Assembly,
            "BuildingBlock.Api",
            "Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Obsolete_typo_infrastructure_project_is_not_active_source()
    {
        var root = FindRepositoryRoot();
        var typoDirectory = Path.Combine(root, "BuildingBlock.Infrastracture");

        if (!Directory.Exists(typoDirectory))
        {
            return;
        }

        Assert.Empty(Directory.EnumerateFiles(typoDirectory, "*.csproj", SearchOption.AllDirectories));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(typoDirectory, "*.cs", SearchOption.AllDirectories),
            IsProductionSource);
    }

    private static Type EntityTypeParameter(Type openGenericContract)
        => openGenericContract.GetGenericArguments()[0];

    private static void AssertNoReferences(Assembly assembly, params string[] forbiddenNames)
    {
        var references = assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(forbiddenName, references);
        }
    }

    private static bool IsProductionSource(string path)
        => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
           !path.Contains($"{Path.DirectorySeparatorChar}BuildingBlock.Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuildingBlock.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class TestAggregateRoot : AggregateRoot<int>
    {
        public TestAggregateRoot(int id)
            : base(id)
        {
        }
    }

    private sealed class ChildEntity
    {
    }

    private sealed class SoftDeletedChildEntity : ISoftDeleteEntity
    {
        public bool IsDeleted { get; set; }

        public DateTime? DeletedOnUtc { get; set; }

        public DateTime? RestoredOnUtc { get; set; }
    }

    private sealed class TestWriteMarker : IWritePersistenceMarker
    {
    }
}
