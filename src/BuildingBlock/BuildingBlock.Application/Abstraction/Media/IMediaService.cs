namespace BuildingBlock.Application.Abstraction.Media
{
    public sealed record MediaUpload(
        Stream Content,
        string FileName,
        string? ContentType = null,
        long? Length = null);

    public sealed record MediaStorageRequest(
        string FolderName,
        string? FileName = null);

    public sealed record StoredMedia(
        string Key,
        string FileName,
        string ContentType,
        string Extension,
        long Length);

    public sealed record ValidatedMediaUpload(
        Stream Content,
        string OriginalFileName,
        string SafeFileName,
        string ContentType,
        string Extension,
        long Length,
        bool OwnsStream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
            => OwnsStream ? Content.DisposeAsync() : ValueTask.CompletedTask;
    }

    public sealed record MalwareScanResult(bool IsSafe, string? Reason = null)
    {
        public static MalwareScanResult Safe { get; } = new(true);

        public static MalwareScanResult Unsafe(string? reason = null) => new(false, reason);
    }

    public interface IMalwareScanner
    {
        Task<MalwareScanResult> ScanAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken ct = default);
    }

    public interface IMediaUploadValidator
    {
        Task<ValidatedMediaUpload> ValidateAsync(MediaUpload upload, CancellationToken ct = default);
    }

    public interface IMediaStorage
    {
        Task<StoredMedia> SaveAsync(
            ValidatedMediaUpload upload,
            MediaStorageRequest request,
            CancellationToken ct = default);

        Task DeleteAsync(string key, CancellationToken ct = default);
    }

    public interface IMediaService
    {
        Task<StoredMedia> SaveAsync(
            MediaUpload upload,
            MediaStorageRequest request,
            CancellationToken ct = default);

        Task<IReadOnlyList<StoredMedia>> SaveAsync(
            IEnumerable<MediaUpload> uploads,
            MediaStorageRequest request,
            CancellationToken ct = default);

        Task DeleteAsync(string key, CancellationToken ct = default);

        Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default);
    }
}
