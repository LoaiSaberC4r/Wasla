using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class MediaService : IMediaService
    {
        private static readonly Action<ILogger, string, string, long, Exception?> MediaFileSaved =
            LoggerMessage.Define<string, string, long>(
                LogLevel.Information,
                new EventId(2100, nameof(MediaFileSaved)),
                "Media file saved. ContentType={ContentType} Extension={Extension} Bytes={Length}");

        private static readonly Action<ILogger, string, Exception?> MediaRollbackFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(2101, nameof(MediaRollbackFailed)),
                "Failed to roll back saved media item during batch save. ContentType={ContentType}");

        private readonly IMediaUploadValidator _validator;
        private readonly IMediaStorage _storage;
        private readonly ILogger<MediaService> _logger;

        public MediaService(
            IMediaUploadValidator validator,
            IMediaStorage storage,
            ILogger<MediaService> logger)
        {
            _validator = validator;
            _storage = storage;
            _logger = logger;
        }

        public async Task<StoredMedia> SaveAsync(
            MediaUpload upload,
            MediaStorageRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await using var validated = await _validator.ValidateAsync(upload, ct);
                var stored = await _storage.SaveAsync(validated, request, ct);
                BuildingBlockDiagnostics.RecordMediaFileSaved(stored.Length);
                MediaFileSaved(_logger, stored.ContentType, stored.Extension, stored.Length, null);
                return stored;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                BuildingBlockDiagnostics.RecordMediaFailure("save", exception.GetType().Name);
                throw;
            }
        }

        public async Task<IReadOnlyList<StoredMedia>> SaveAsync(
            IEnumerable<MediaUpload> uploads,
            MediaStorageRequest request,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(uploads);

            var saved = new List<StoredMedia>();
            try
            {
                foreach (var upload in uploads)
                {
                    saved.Add(await SaveAsync(upload, request, ct));
                }

                return saved;
            }
            catch
            {
                foreach (var stored in saved)
                {
                    try
                    {
                        await _storage.DeleteAsync(stored.Key, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        MediaRollbackFailed(_logger, stored.ContentType, exception);
                    }
                }

                throw;
            }
        }

        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            try
            {
                await _storage.DeleteAsync(key, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                BuildingBlockDiagnostics.RecordMediaFailure("delete", exception.GetType().Name);
                throw;
            }
        }

        public async Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(keys);

            foreach (var key in keys)
            {
                await DeleteAsync(key, ct);
            }
        }
    }
}
