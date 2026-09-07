namespace BuildingBlock.Tests;

public sealed class Phase4StaticCleanupTests
{
    [Fact]
    public void Durable_logging_options_are_removed_from_production_code()
    {
        var root = FindRepositoryRoot();
        var matches = ProductionFiles(root)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(item =>
                item.line.Contains("DurableOptions", StringComparison.Ordinal) ||
                item.line.Contains("ReplayOnStart", StringComparison.Ordinal) ||
                item.line.Contains("MaxReplayBatch", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.number}")
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void Obsolete_external_service_patterns_are_removed_from_primary_contracts()
    {
        var root = FindRepositoryRoot();
        var applicationFiles = ProductionFiles(root)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}BuildingBlock.Application{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var matches = applicationFiles
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { path, line, number = index + 1 }))
            .Where(item =>
                item.line.Contains("SaveVideoAsync", StringComparison.Ordinal) ||
                item.line.Contains("GenerateQRCode", StringComparison.Ordinal) ||
                item.line.Contains("string? From", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(root, item.path)}:{item.number}")
            .ToArray();

        Assert.Empty(matches);
    }

    private static IEnumerable<string> ProductionFiles(string root)
        => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
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
