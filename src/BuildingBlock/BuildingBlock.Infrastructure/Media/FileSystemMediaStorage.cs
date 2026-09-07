using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Infrastructure.Media
{
    internal sealed class FileSystemMediaStorage : IMediaStorage
    {
        private static readonly Action<ILogger, Exception?> MediaFileDeleted =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(2000, nameof(MediaFileDeleted)),
                "Media file deleted.");

        private readonly MediaStorageOptions _options;
        private readonly ILogger<FileSystemMediaStorage> _logger;
        private readonly string _root;

        public FileSystemMediaStorage(
            IOptions<MediaStorageOptions> options,
            ILogger<FileSystemMediaStorage> logger)
        {
            _options = options.Value;
            _logger = logger;
            _root = ResolveRoot(_options);

            try
            {
                Directory.CreateDirectory(_root);
            }
            catch (Exception exception)
            {
                throw StorageUnavailable("Media storage root could not be created.", exception);
            }
        }

        public async Task<StoredMedia> SaveAsync(
            ValidatedMediaUpload upload,
            MediaStorageRequest request,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(upload);
            ArgumentNullException.ThrowIfNull(request);
            ct.ThrowIfCancellationRequested();

            var folder = NormalizeFolder(request.FolderName);
            var fileName = NormalizeStoredFileName(request.FileName, upload.Extension);
            var key = CombineRelative(folder, fileName);
            var finalPath = ResolvePhysicalPath(key);
            var directory = Path.GetDirectoryName(finalPath)
                ?? throw StorageUnavailable("Media storage path is invalid.");
            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");
            var originalPosition = upload.Content.CanSeek ? upload.Content.Position : 0;

            try
            {
                await using (var output = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    if (upload.Content.CanSeek)
                    {
                        upload.Content.Position = 0;
                    }

                    await upload.Content.CopyToAsync(output, ct);
                    await output.FlushAsync(ct);
                }

                File.Move(tempPath, finalPath, overwrite: false);
                return new StoredMedia(
                    key,
                    fileName,
                    upload.ContentType,
                    upload.Extension,
                    upload.Length);
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not MediaServiceException)
            {
                throw StorageUnavailable("Media file could not be saved.", exception);
            }
            finally
            {
                if (upload.Content.CanSeek)
                {
                    upload.Content.Position = originalPosition;
                }

                TryDeleteTempFile(tempPath);
            }
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var path = ResolvePhysicalPath(key);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    MediaFileDeleted(_logger, null);
                }

                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                throw StorageUnavailable("Media file could not be deleted.", exception);
            }
        }

        private static string ResolveRoot(MediaStorageOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.RootPath))
            {
                throw StorageUnavailable("Media storage root path is required.");
            }

            var root = Path.IsPathRooted(options.RootPath)
                ? options.RootPath
                : Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, options.RootPath);

            return Path.GetFullPath(root);
        }

        private static string NormalizeFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw InvalidPath("Media folder name is required.");
            }

            var normalized = folderName.Trim().Replace('\\', '/').Trim('/');
            if (Path.IsPathRooted(folderName) ||
                normalized.Contains(':', StringComparison.Ordinal) ||
                normalized.Split('/').Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                throw InvalidPath("Media folder path is invalid.");
            }

            return normalized;
        }

        private static string NormalizeStoredFileName(string? requestedFileName, string extension)
        {
            if (string.IsNullOrWhiteSpace(requestedFileName))
            {
                return $"{Guid.NewGuid():N}{extension}";
            }

            if (Path.IsPathRooted(requestedFileName) ||
                requestedFileName.Contains(':', StringComparison.Ordinal) ||
                requestedFileName.Contains('/', StringComparison.Ordinal) ||
                requestedFileName.Contains('\\', StringComparison.Ordinal))
            {
                throw InvalidPath("Media file name is invalid.");
            }

            var safeName = Path.GetFileName(requestedFileName);
            if (!string.Equals(safeName, requestedFileName, StringComparison.Ordinal) ||
                safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw InvalidPath("Media file name is invalid.");
            }

            var requestedExtension = Path.GetExtension(safeName);
            if (string.IsNullOrWhiteSpace(requestedExtension))
            {
                return safeName + extension;
            }

            if (!string.Equals(requestedExtension, extension, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidPath("Media file name extension does not match validated content.");
            }

            return safeName;
        }

        private string ResolvePhysicalPath(string relativePath)
        {
            var normalized = relativePath.Trim().Replace('\\', '/').Trim('/');
            if (Path.IsPathRooted(normalized) ||
                normalized.Contains(':', StringComparison.Ordinal) ||
                normalized.Split('/').Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
            {
                throw InvalidPath("Media path is invalid.");
            }

            var physicalPath = Path.GetFullPath(Path.Combine(
                _root,
                normalized.Replace('/', Path.DirectorySeparatorChar)));

            EnsureUnderRoot(physicalPath);
            return physicalPath;
        }

        private void EnsureUnderRoot(string physicalPath)
        {
            var root = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidPath("Media path resolves outside the storage root.");
            }
        }

        private static string CombineRelative(string folder, string fileName)
            => $"{folder.Trim('/')}/{fileName}".Replace('\\', '/');

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Temporary-file cleanup must not hide the original storage result.
            }
        }

        private static MediaServiceException InvalidPath(string message)
            => new(Error.Validation(ExternalServiceErrorCodes.Media.InvalidFile, message, source: "Media"));

        private static MediaServiceException StorageUnavailable(string message, Exception? innerException = null)
            => new(Error.Infra(ExternalServiceErrorCodes.Media.StorageUnavailable, message, source: "Media"), innerException);
    }
}
