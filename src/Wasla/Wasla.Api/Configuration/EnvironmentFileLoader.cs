namespace Wasla.Api.Configuration;

internal static class EnvironmentFileLoader
{
    public static void LoadIfDevelopment(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var environment = ResolveEnvironment(args);
        if (!string.Equals(
                environment,
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = FindEnvironmentFile();
        if (path is null)
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            if (key.Length == 0 ||
                Environment.GetEnvironmentVariable(key) is not null)
            {
                continue;
            }

            var value = Unquote(trimmed[(separator + 1)..].Trim());
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string ResolveEnvironment(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(
                    args[index],
                    "--environment",
                    StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                return args[index + 1];
            }

            const string prefix = "--environment=";
            if (args[index].StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return args[index][prefix.Length..];
            }
        }

        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
    }

    private static string? FindEnvironmentFile()
    {
        var starts = new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var start in starts)
        {
            for (var directory = start;
                 directory is not null;
                 directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string Unquote(string value)
        => value.Length >= 2 &&
           (value[0] == '"' && value[^1] == '"' ||
            value[0] == '\'' && value[^1] == '\'')
            ? value[1..^1]
            : value;
}
