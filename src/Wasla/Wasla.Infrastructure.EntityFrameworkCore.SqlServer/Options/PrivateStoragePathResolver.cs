namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public static class PrivateStoragePathResolver
{
    public static string Resolve(string contentRootPath, string? configuredPath, bool requireAppDataRoot = true)
    {
        const string error = "Private storage requires a valid relative path inside App_Data/Wasla.";
        if (string.IsNullOrWhiteSpace(contentRootPath) || string.IsNullOrWhiteSpace(configuredPath))
            throw new ArgumentException(error);

        try
        {
            if (!Path.IsPathFullyQualified(contentRootPath)) throw new ArgumentException(error);
            var normalized = configuredPath.Replace('\\', '/');
            if (requireAppDataRoot && (normalized.StartsWith('/') || Path.IsPathRooted(normalized) ||
                normalized.Contains(':', StringComparison.Ordinal) ||
                normalized.Split('/').Any(segment => segment is not ("." or "..") &&
                    (segment.EndsWith(' ') || segment.EndsWith('.')))))
                throw new ArgumentException(error);

            var resolved = Path.GetFullPath(normalized.Replace('/', Path.DirectorySeparatorChar), contentRootPath);
            var root = Path.GetFullPath(Path.Combine(contentRootPath, "App_Data", "Wasla")) + Path.DirectorySeparatorChar;
            if (requireAppDataRoot && !resolved.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException(error);
            return resolved;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Configuration values and physical paths must not appear in activation errors.
            throw new ArgumentException(error);
        }
    }
}
