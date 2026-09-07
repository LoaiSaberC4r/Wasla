using System.Reflection;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

namespace Wasla.Tests.Architecture;

public sealed class ArchitectureTests
{
    private static readonly Assembly Domain =
        Wasla.Domain.AssemblyReference.Assembly;
    private static readonly Assembly Application =
        Wasla.Application.AssemblyReference.Assembly;
    private static readonly Assembly Infrastructure =
        Wasla.Infrastructure.AssemblyReference.Assembly;
    private static readonly Assembly SqlServer =
        typeof(EmailOutboxMessage).Assembly;
    private static readonly Assembly Api =
        typeof(Wasla.Api.Configuration.WaslaCorsOptions).Assembly;

    [Fact]
    public void Domain_DoesNotReferenceOuterLayersOrFrameworks()
    {
        var references = ReferenceNames(Domain);

        Assert.DoesNotContain("Wasla.Application", references);
        Assert.DoesNotContain("Wasla.Infrastructure", references);
        Assert.DoesNotContain(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer",
            references);
        Assert.DoesNotContain("Wasla.Api", references);
        Assert.DoesNotContain(
            references,
            name => name.StartsWith(
                "Microsoft.EntityFrameworkCore",
                StringComparison.Ordinal));
        Assert.DoesNotContain("MediatR", references);
    }

    [Fact]
    public void Application_DoesNotReferenceApiOrInfrastructure()
    {
        var references = ReferenceNames(Application);

        Assert.DoesNotContain("Wasla.Api", references);
        Assert.DoesNotContain("Wasla.Infrastructure", references);
        Assert.DoesNotContain(
            "Wasla.Infrastructure.EntityFrameworkCore.SqlServer",
            references);
    }

    [Fact]
    public void ProductProjectGraph_IsAcyclic()
    {
        var assemblies = new[]
        {
            Domain,
            Application,
            Infrastructure,
            SqlServer,
            Api
        };
        var productNames = assemblies
            .Select(assembly => assembly.GetName().Name!)
            .ToHashSet(StringComparer.Ordinal);
        var graph = assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null && productNames.Contains(name))
                .Select(name => name!)
                .ToArray(),
            StringComparer.Ordinal);

        foreach (var assemblyName in graph.Keys)
        {
            Assert.False(
                HasCycle(assemblyName, graph, [], []),
                $"A project-reference cycle starts at {assemblyName}.");
        }
    }

    [Fact]
    public void BuildingBlockAssemblies_DoNotReferenceWasla()
    {
        var assemblies = new[]
        {
            typeof(BuildingBlock.Domain.Results.Result).Assembly,
            typeof(BuildingBlock.Application.Email.EmailMessage).Assembly,
            typeof(BuildingBlock.Infrastructure.Options.SmtpOptions).Assembly,
            typeof(BuildingBlock.Api.Options.BuildingBlockSwaggerOptions).Assembly,
            typeof(BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer
                .SqlServerExceptionToErrorMapper).Assembly
        };

        foreach (var assembly in assemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name?.StartsWith(
                    "Wasla",
                    StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void ActiveProductAssemblies_ContainNoRemovedBusinessTypes()
    {
        var removedTerms = new[]
        {
            "Law" + "yer",
            "Cli" + "ent",
            "Consul" + "tation",
            "Legal" + "Specialization",
            "Gover" + "norate"
        };

        foreach (var assembly in new[]
                 {
                     Domain,
                     Application,
                     Infrastructure,
                     SqlServer,
                     Api
                 })
        {
            foreach (var type in assembly.GetTypes())
            {
                Assert.DoesNotContain(
                    removedTerms,
                    term => (type.FullName ?? type.Name).Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ActiveSource_ContainsNoObsoleteProductNamespace()
    {
        var root = FindRepositoryRoot();
        var obsoleteNamespace = "Law" + "yerPlatform.";
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            .Where(path => !HasGeneratedSegment(path));

        foreach (var file in files)
        {
            Assert.DoesNotContain(
                obsoleteNamespace,
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }
    }

    private static HashSet<string> ReferenceNames(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static bool HasCycle(
        string node,
        IReadOnlyDictionary<string, string[]> graph,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (!visiting.Add(node))
        {
            return true;
        }

        if (visited.Contains(node))
        {
            visiting.Remove(node);
            return false;
        }

        foreach (var dependency in graph[node])
        {
            if (HasCycle(dependency, graph, visiting, visited))
            {
                return true;
            }
        }

        visiting.Remove(node);
        visited.Add(node);
        return false;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wasla.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Unable to locate the Wasla repository root.");
    }

    private static bool HasGeneratedSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            segment is "bin" or "obj" or ".git" or ".vs" or "TestResults");
    }
}
