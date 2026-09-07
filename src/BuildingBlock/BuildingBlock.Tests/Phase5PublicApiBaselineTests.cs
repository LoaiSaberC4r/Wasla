using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer;
using BuildingBlock.Infrastructure.Persistence;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class Phase5PublicApiBaselineTests
{
    private static readonly string[] RemovedApiFragments =
    [
        ".Legacy.",
        "IGenericRepository",
        "IEfDbContextProvider",
        "Pagination<",
        "ThrowIfFailure",
        "ListWithCountAsync",
        ".CountAsync(",
        ".QueryTag.get",
        "SoftDeleteFilterState",
        "ISoftDeleteFilterContext",
        "SoftDeleteFilterDbContext",
        "AddingEmailService",
        "AddMediaService",
        "AddQrCodeService",
        "AddSecurity(",
        "AddBuildingBlockAuditingAndSoftDelete"
    ];

    public static TheoryData<string, Assembly> ApiAssemblies => new()
    {
        { "BuildingBlock.Domain", typeof(IAggregateRoot).Assembly },
        { "BuildingBlock.Application", typeof(IReadPersistenceMarker).Assembly },
        { "BuildingBlock.Infrastructure", typeof(ModelBuilderConfigExtensions).Assembly },
        { "BuildingBlock.Api", typeof(BuildingBlock.Api.ProblemDetails.IProblemDetailsMapper).Assembly },
        { "BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer", typeof(SqlServerExceptionToErrorMapper).Assembly }
    };

    [Theory]
    [MemberData(nameof(ApiAssemblies))]
    public void Shipped_baseline_covers_exported_types_and_members(string projectName, Assembly assembly)
    {
        var shipped = ReadBaseline(projectName, "Shipped");
        var unshipped = ReadBaseline(projectName, "Unshipped");
        var exportedTypes = assembly.GetExportedTypes().Select(FormatTypeName).ToArray();

        Assert.NotEmpty(shipped);
        Assert.Contains(shipped, line => line.Contains(" -> ", StringComparison.Ordinal));
        Assert.All(exportedTypes, typeName => Assert.Contains(typeName, shipped));
        Assert.Empty(unshipped);
        Assert.DoesNotContain(
            shipped,
            line => RemovedApiFragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)));
    }

    private static string[] ReadBaseline(string projectName, string state)
    {
        var path = Path.Combine(FindRepositoryRoot(), projectName, $"PublicAPI.{state}.txt");

        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatTypeName(Type type)
        => type.DeclaringType is null
            ? $"{type.Namespace}.{CleanName(type)}"
            : $"{FormatTypeName(type.DeclaringType)}.{CleanName(type)}";

    private static string CleanName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : $"{name[..tick]}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>";
    }

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
