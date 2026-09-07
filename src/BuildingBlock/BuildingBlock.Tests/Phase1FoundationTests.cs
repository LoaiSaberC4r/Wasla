using System.Text.Json;
using System.Xml.Linq;

namespace BuildingBlock.Tests;

public sealed class Phase1FoundationTests
{
    [Fact]
    public void Global_json_pins_a_stable_dotnet10_feature_band_to_patch_roll_forward()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Matches(@"^10\.0\.\d{3}$", sdk.GetProperty("version").GetString()!);
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void Every_project_inherits_net10_and_csharp14_from_central_build_properties()
    {
        var root = FindRepositoryRoot();
        var buildProperties = LoadXml(Path.Combine(root, "Directory.Build.props"));

        Assert.Equal("net10.0", SingleValue(buildProperties, "TargetFramework"));
        Assert.Equal("14.0", SingleValue(buildProperties, "LangVersion"));

        foreach (var projectPath in ProjectFiles(root))
        {
            var project = LoadXml(projectPath);
            var targetFrameworks = Elements(project, "TargetFramework")
                .Concat(Elements(project, "TargetFrameworks"))
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            Assert.All(targetFrameworks, target => Assert.Equal("net10.0", target));
            Assert.DoesNotContain("net8.0", File.ReadAllText(projectPath), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("net9.0", File.ReadAllText(projectPath), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Central_package_management_has_no_project_versions_and_aligns_ef_core_10()
    {
        var root = FindRepositoryRoot();
        var centralPackages = LoadXml(Path.Combine(root, "Directory.Packages.props"));

        Assert.Equal("true", SingleValue(centralPackages, "ManagePackageVersionsCentrally"));
        Assert.Equal("false", SingleValue(centralPackages, "CentralPackageVersionOverrideEnabled"));

        var efCoreVersions = Elements(centralPackages, "PackageVersion")
            .Where(element => element.Attribute("Include")?.Value.StartsWith(
                "Microsoft.EntityFrameworkCore",
                StringComparison.Ordinal) == true)
            .Select(element => element.Attribute("Version")?.Value)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(efCoreVersions);
        Assert.Matches(@"^10\.0\.\d+$", efCoreVersions[0]);

        foreach (var projectPath in ProjectFiles(root))
        {
            var packageReferences = Elements(LoadXml(projectPath), "PackageReference");
            foreach (var packageReference in packageReferences)
            {
                Assert.Null(packageReference.Attribute("Version"));
                Assert.Null(packageReference.Attribute("VersionOverride"));
                Assert.DoesNotContain(packageReference.Elements(), element =>
                    element.Name.LocalName is "Version" or "VersionOverride");
            }
        }
    }

    [Fact]
    public void Project_metadata_preserves_layer_boundaries_and_contains_no_reference_cycles()
    {
        var root = FindRepositoryRoot();
        var projects = ProjectFiles(root)
            .ToDictionary(Path.GetFullPath, LoadProjectMetadata, StringComparer.OrdinalIgnoreCase);

        var domain = projects.Values.Single(project => project.Name == "BuildingBlock.Domain");
        Assert.Empty(domain.PackageReferences);
        Assert.Empty(domain.ProjectReferences);

        var application = projects.Values.Single(project => project.Name == "BuildingBlock.Application");
        Assert.DoesNotContain(application.PackageReferences, package =>
            package.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            package.Equals("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal));
        Assert.All(application.ProjectReferences, reference =>
            Assert.Equal("BuildingBlock.Domain", projects[reference].Name));

        var graph = projects.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ProjectReferences,
            StringComparer.OrdinalIgnoreCase);
        var indegree = graph.Keys.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var reference in graph.Values.SelectMany(references => references))
        {
            Assert.True(graph.ContainsKey(reference), $"Project reference is outside the solution: {reference}");
            indegree[reference]++;
        }

        var ready = new Queue<string>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (ready.TryDequeue(out var project))
        {
            visited++;
            foreach (var reference in graph[project])
            {
                indegree[reference]--;
                if (indegree[reference] == 0)
                {
                    ready.Enqueue(reference);
                }
            }
        }

        Assert.Equal(graph.Count, visited);
    }

    [Fact]
    public void Source_export_policy_explicitly_excludes_generated_artifacts()
    {
        var root = FindRepositoryRoot();
        var exportScript = File.ReadAllText(Path.Combine(root, "scripts", "export-source.ps1"));

        Assert.All(
            new[] { "bin", "obj", ".vs", "TestResults", "Generated", "*.g.cs", "*.AssemblyInfo.cs" },
            excluded => Assert.Contains(excluded, exportScript, StringComparison.Ordinal));
        Assert.Contains("ExcludeMigrations", exportScript, StringComparison.Ordinal);
    }

    private static ProjectMetadata LoadProjectMetadata(string projectPath)
    {
        var project = LoadXml(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var references = Elements(project, "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(reference => Path.GetFullPath(Path.Combine(projectDirectory, reference)))
            .ToArray();
        var packages = Elements(project, "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();

        return new ProjectMetadata(Path.GetFileNameWithoutExtension(projectPath), references, packages);
    }

    private static string[] ProjectFiles(string root)
        => Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !HasDirectorySegment(path, "bin") && !HasDirectorySegment(path, "obj"))
            .ToArray();

    private static bool HasDirectorySegment(string path, string segment)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static XDocument LoadXml(string path) => XDocument.Load(path);

    private static IEnumerable<XElement> Elements(XDocument document, string localName)
        => document.Descendants().Where(element => element.Name.LocalName == localName);

    private static string SingleValue(XDocument document, string localName)
        => Assert.Single(Elements(document, localName)).Value;

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

    private sealed record ProjectMetadata(
        string Name,
        IReadOnlyCollection<string> ProjectReferences,
        IReadOnlyCollection<string> PackageReferences);
}
