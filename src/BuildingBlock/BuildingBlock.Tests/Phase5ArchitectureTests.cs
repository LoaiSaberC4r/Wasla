using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Persistence;
using MediatR;
using NetArchTest.Rules;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BuildingBlock.Tests;

public sealed class Phase5ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(IAggregateRoot).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IReadPersistenceMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(EfCoreExceptionToErrorMapper).Assembly;
    private static readonly Assembly ApiAssembly = typeof(BuildingBlock.Api.ProblemDetails.IProblemDetailsMapper).Assembly;

    [Fact]
    public void Layer_dependency_boundaries_are_enforced_by_architecture_tests()
    {
        AssertArchRule(Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "BuildingBlock.Application",
                "BuildingBlock.Infrastructure",
                "BuildingBlock.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "Microsoft.Data.SqlClient",
                "Serilog",
                "MailKit",
                "QRCoder")
            .GetResult());

        AssertArchRule(Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "BuildingBlock.Infrastructure",
                "BuildingBlock.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Http",
                "Microsoft.Data.SqlClient",
                "Serilog",
                "MailKit",
                "QRCoder")
            .GetResult());

        AssertArchRule(Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "BuildingBlock.Api",
                "Microsoft.Data.SqlClient")
            .GetResult());
    }

    [Fact]
    public void Public_contract_naming_conventions_are_enforced()
    {
        var assemblies = new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly };
        var exportedTypes = assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();

        Assert.Empty(exportedTypes
            .Where(type => type.IsInterface)
            .Where(type => !type.Name.StartsWith('I'))
            .Select(type => type.FullName));

        Assert.Empty(exportedTypes
            .Where(ImplementsPipelineBehavior)
            .Where(type => !CleanMetadataName(type).EndsWith("Behavior", StringComparison.Ordinal))
            .Select(type => type.FullName));

        Assert.Empty(exportedTypes
            .Where(type => type.Namespace == typeof(DomainEventsInterceptor).Namespace)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => !type.Name.EndsWith("Interceptor", StringComparison.Ordinal))
            .Select(type => type.FullName));

        Assert.Empty(exportedTypes
            .Where(type => type.Name.EndsWith("Option", StringComparison.Ordinal))
            .Select(type => type.FullName));

        Assert.DoesNotContain(exportedTypes, type =>
            type.Namespace?.Contains("OpenAi", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(assemblies, assembly =>
            assembly.GetName().Name?.Contains("Infrastracture", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Genericity_and_source_safety_rules_are_enforced_for_active_production_code()
    {
        var prohibitedTerms = new[]
        {
            "Qcontrol",
            "BranchAdmin",
            "TechnicalAdmin",
            "WaitingArea",
            "LeafService",
            "BuildingBlock.Infrastracture",
            "BuildingBlock.Api.OpenAi",
            "LanguageCode",
            "OrderSort",
            "SaveVideoAsync",
            "GenerateQRCode"
        };
        var prohibitedPatterns = new Func<string, bool>[]
        {
            line => Regex.IsMatch(line, "Ignore" + "QueryFilters\\s*\\(\\s*\\)", RegexOptions.CultureInvariant),
            line => line.Contains("async" + " void", StringComparison.Ordinal),
            line => line.Contains(".GetAwaiter()." + "GetResult()", StringComparison.Ordinal),
            line => Regex.IsMatch(line, @"\.\s*Wait\s*\(", RegexOptions.CultureInvariant),
            line => Regex.IsMatch(line, @"\.\s*Result\b", RegexOptions.CultureInvariant)
        };
        var root = FindRepositoryRoot();
        var violations = ProductionSourceFiles(root)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    Line = line,
                    Number = index + 1
                }))
            .Where(item => prohibitedTerms.Any(term =>
                    item.Line.Contains(term, StringComparison.Ordinal)) ||
                prohibitedPatterns.Any(pattern => pattern(item.Line)))
            .Select(item => $"{Path.GetRelativePath(root, item.Path)}:{item.Number}: {item.Line.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Repository_contracts_do_not_expose_unrestricted_queryables_to_application_consumers()
    {
        var repositoryContracts = new[]
        {
            typeof(IReadRepository<,>),
            typeof(IWriteRepository<,>),
            typeof(ISoftDeletedReadRepository<,>),
            typeof(ISoftDeletedWriteRepository<,>),
            typeof(IReadModelWriter<,>),
            typeof(IReadModelUnitOfWork<>),
            typeof(IUnitOfWork<>)
        };

        var queryableReturns = repositoryContracts
            .SelectMany(type => type.GetMethods())
            .Where(method => ContainsQueryable(method.ReturnType))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(queryableReturns);
    }

    private static void AssertArchRule(NetArchTest.Rules.TestResult result)
    {
        Assert.True(
            result.IsSuccessful,
            "Architecture rule failed for: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    private static bool ImplementsPipelineBehavior(Type type)
        => type.GetInterfaces().Any(candidate =>
            candidate.IsGenericType &&
            candidate.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

    private static string CleanMetadataName(Type type)
    {
        var tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    private static bool ContainsQueryable(Type type)
    {
        if (type == typeof(IQueryable) ||
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            return type.GetGenericArguments().Any(ContainsQueryable);
        }

        return false;
    }

    private static IEnumerable<string> ProductionSourceFiles(string root)
        => Directory.EnumerateFiles(Path.Combine(root, "src", "BuildingBlock"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}BuildingBlock.Tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

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
}
