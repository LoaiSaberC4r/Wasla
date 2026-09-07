using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Infrastructure.Media
{
    internal sealed class MediaUploadValidator : IMediaUploadValidator
    {
        private static readonly Dictionary<string, MediaSignature> KnownTypes =
            new Dictionary<string, MediaSignature>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = new("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
                [".jpeg"] = new("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
                [".png"] = new("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),
                [".gif"] = new("image/gif", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } }),
                [".pdf"] = new("application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }),
                [".mp4"] = new("video/mp4", Array.Empty<byte[]>())
            };

        private static readonly HashSet<string> ActiveContentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".svg",
            ".html",
            ".htm",
            ".js",
            ".mjs",
            ".exe",
            ".bat",
            ".cmd",
            ".ps1"
        };

        private readonly MediaStorageOptions _options;
        private readonly IMalwareScanner[] _scanners;

        public MediaUploadValidator(
            IOptions<MediaStorageOptions> options,
            IEnumerable<IMalwareScanner> scanners)
        {
            _options = options.Value;
            _scanners = scanners
                .Where(scanner => scanner is not NoOpMalwareScanner)
                .ToArray();

            if (_options.RequireMalwareScanner && _scanners.Length == 0)
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.MalwareDetected,
                    "A malware scanner is required but no scanner is registered.");
            }
        }

        public async Task<ValidatedMediaUpload> ValidateAsync(MediaUpload upload, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(upload);
            ArgumentNullException.ThrowIfNull(upload.Content);
            ct.ThrowIfCancellationRequested();

            var safeFileName = ValidateFileName(upload.FileName);
            var extension = NormalizeExtension(Path.GetExtension(safeFileName));
            ValidateExtension(extension);

            return upload.Content.CanSeek
                ? await ValidateSeekableAsync(upload, safeFileName, extension, ct)
                : await ValidateNonSeekableAsync(upload, safeFileName, extension, ct);
        }

        private async Task<ValidatedMediaUpload> ValidateSeekableAsync(
            MediaUpload upload,
            string safeFileName,
            string extension,
            CancellationToken ct)
        {
            var originalPosition = upload.Content.Position;
            try
            {
                var actualLength = upload.Content.Length;
                ValidateDeclaredLength(upload.Length, actualLength);
                ValidateLength(actualLength);

                upload.Content.Position = 0;
                var contentType = await DetectContentTypeAsync(upload.Content, extension, ct);
                ValidateDeclaredContentType(upload.ContentType, contentType);
                upload.Content.Position = 0;
                await ScanAsync(upload.Content, safeFileName, contentType, ct);

                return new ValidatedMediaUpload(
                    upload.Content,
                    upload.FileName,
                    safeFileName,
                    contentType,
                    extension,
                    actualLength,
                    OwnsStream: false);
            }
            finally
            {
                upload.Content.Position = originalPosition;
            }
        }

        private async Task<ValidatedMediaUpload> ValidateNonSeekableAsync(
            MediaUpload upload,
            string safeFileName,
            string extension,
            CancellationToken ct)
        {
            if (upload.Length is { } declaredLength)
            {
                ValidateLength(declaredLength);
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"bb-media-{Guid.NewGuid():N}.tmp");
            var temp = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            try
            {
                var actualLength = await CopyToBoundedStreamAsync(upload.Content, temp, upload.Length, ct);
                ValidateLength(actualLength);

                temp.Position = 0;
                var contentType = await DetectContentTypeAsync(temp, extension, ct);
                ValidateDeclaredContentType(upload.ContentType, contentType);
                temp.Position = 0;
                await ScanAsync(temp, safeFileName, contentType, ct);
                temp.Position = 0;

                return new ValidatedMediaUpload(
                    temp,
                    upload.FileName,
                    safeFileName,
                    contentType,
                    extension,
                    actualLength,
                    OwnsStream: true);
            }
            catch
            {
                await temp.DisposeAsync();
                throw;
            }
        }

        private async Task<long> CopyToBoundedStreamAsync(
            Stream input,
            Stream output,
            long? declaredLength,
            CancellationToken ct)
        {
            var buffer = new byte[81920];
            long total = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > _options.MaxFileSizeBytes)
                {
                    throw Invalid(
                        ExternalServiceErrorCodes.Media.FileTooLarge,
                        "File exceeds the configured maximum size.");
                }

                if (declaredLength.HasValue && total > declaredLength.Value)
                {
                    throw Invalid(
                        ExternalServiceErrorCodes.Media.InvalidFile,
                        "Declared file length is smaller than the uploaded content.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }

            return total;
        }

        private async Task<string> DetectContentTypeAsync(Stream stream, string extension, CancellationToken ct)
        {
            if (!KnownTypes.TryGetValue(extension, out var signature))
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.UnsupportedType,
                    "File type is not supported.");
            }

            var originalPosition = stream.Position;
            stream.Position = 0;
            var buffer = new byte[32];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            stream.Position = originalPosition;

            var valid = extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                ? read >= 12 && buffer.AsSpan(4, 4).SequenceEqual(new byte[] { 0x66, 0x74, 0x79, 0x70 })
                : signature.Signatures.Any(candidate =>
                    read >= candidate.Length &&
                    buffer.AsSpan(0, candidate.Length).SequenceEqual(candidate));

            if (!valid)
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.SignatureMismatch,
                    "File signature does not match the extension.");
            }

            if (!_options.AllowedMimeTypes.Contains(signature.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.UnsupportedType,
                    "Detected file MIME type is not allowed.");
            }

            return signature.ContentType;
        }

        private async Task ScanAsync(Stream stream, string fileName, string contentType, CancellationToken ct)
        {
            if (_scanners.Length == 0)
            {
                return;
            }

            var originalPosition = stream.Position;
            try
            {
                foreach (var scanner in _scanners)
                {
                    stream.Position = 0;
                    var result = await scanner.ScanAsync(stream, fileName, contentType, ct);
                    if (!result.IsSafe)
                    {
                        throw Invalid(
                            ExternalServiceErrorCodes.Media.MalwareDetected,
                            "Uploaded file failed malware scanning.");
                    }
                }
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        private void ValidateLength(long length)
        {
            if (length <= 0)
            {
                throw Invalid(ExternalServiceErrorCodes.Media.InvalidFile, "File is empty.");
            }

            if (length > _options.MaxFileSizeBytes)
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.FileTooLarge,
                    "File exceeds the configured maximum size.");
            }
        }

        private static void ValidateDeclaredLength(long? declaredLength, long actualLength)
        {
            if (declaredLength.HasValue && actualLength > declaredLength.Value)
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.InvalidFile,
                    "Declared file length is smaller than the uploaded content.");
            }
        }

        private static void ValidateDeclaredContentType(string? declaredContentType, string detectedContentType)
        {
            if (!string.IsNullOrWhiteSpace(declaredContentType) &&
                !string.Equals(declaredContentType.Trim(), detectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.SignatureMismatch,
                    "Declared MIME type does not match detected content.");
            }
        }

        private static string ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                Path.IsPathRooted(fileName) ||
                fileName.Contains(':', StringComparison.Ordinal) ||
                fileName.Contains('/', StringComparison.Ordinal) ||
                fileName.Contains('\\', StringComparison.Ordinal))
            {
                throw Invalid(ExternalServiceErrorCodes.Media.InvalidFile, "File name is invalid.");
            }

            var safeName = Path.GetFileName(fileName);
            if (!string.Equals(safeName, fileName, StringComparison.Ordinal) ||
                safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw Invalid(ExternalServiceErrorCodes.Media.InvalidFile, "File name is invalid.");
            }

            return safeName;
        }

        private void ValidateExtension(string extension)
        {
            if (ActiveContentExtensions.Contains(extension))
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.UnsupportedType,
                    "Active content uploads are not allowed.");
            }

            if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw Invalid(
                    ExternalServiceErrorCodes.Media.UnsupportedType,
                    "File extension is not allowed.");
            }
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw Invalid(ExternalServiceErrorCodes.Media.InvalidFile, "File extension is required.");
            }

            return extension.Trim().ToLowerInvariant();
        }

        private static MediaServiceException Invalid(string code, string message)
            => new(Error.Validation(code, message, source: "Media"));

        private sealed record MediaSignature(string ContentType, IReadOnlyList<byte[]> Signatures);
    }
}
