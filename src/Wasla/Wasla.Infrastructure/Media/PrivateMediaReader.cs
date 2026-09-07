using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Wasla.Application.Media;

namespace Wasla.Infrastructure.Media;

internal sealed class PrivateMediaReader : IPrivateMediaReader
{
    private readonly string _root;

    public PrivateMediaReader(IOptions<MediaStorageOptions> options)
    {
        var value = options.Value;
        _root = Path.GetFullPath(Path.IsPathRooted(value.RootPath)
            ? value.RootPath
            : Path.Combine(value.ContentRootPath ?? AppContext.BaseDirectory, value.RootPath));
    }

    public Task<PrivateMedia?> OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key))
        {
            return Task.FromResult<PrivateMedia?>(null);
        }

        var normalized = key.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return Task.FromResult<PrivateMedia?>(null);
        }

        var path = Path.GetFullPath(Path.Combine(
            _root,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return Task.FromResult<PrivateMedia?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<PrivateMedia?>(new PrivateMedia(
            stream,
            ContentTypeFor(Path.GetExtension(path)),
            Path.GetFileName(path)));
    }

    private static string ContentTypeFor(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
}
