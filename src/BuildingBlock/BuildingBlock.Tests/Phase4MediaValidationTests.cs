using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Media;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Tests;

public sealed class Phase4MediaValidationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bb-phase4-media-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("sample.png", "image/png")]
    [InlineData("sample.jpg", "image/jpeg")]
    [InlineData("sample.pdf", "application/pdf")]
    public async Task Optional_scanner_allows_valid_images_and_PDF_without_registered_scanners(string fileName, string contentType)
    {
        var bytes = Path.GetExtension(fileName) switch { ".png" => Png(), ".jpg" => Jpeg(), _ => "%PDF-1.4\n%%EOF"u8.ToArray() };
        var validator = CreateValidator();
        using var stream = new MemoryStream(bytes);
        await using var validated = await validator.ValidateAsync(new MediaUpload(stream, fileName, contentType), TestContext.Current.CancellationToken);
        Assert.Equal(contentType, validated.ContentType);
        Assert.Equal(bytes.Length, validated.Length);
    }

    [Fact]
    public async Task Optional_scanner_does_not_allow_unsupported_extensions()
    {
        var exception = await Assert.ThrowsAsync<MediaServiceException>(() =>
            CreateValidator().ValidateAsync(new MediaUpload(new MemoryStream(Png()), "sample.txt", "image/png"), TestContext.Current.CancellationToken));
        Assert.Equal(ExternalServiceErrorCodes.Media.UnsupportedType, exception.Error.Code);
    }

    [Fact]
    public async Task Optional_scanner_does_not_allow_oversized_files()
    {
        var validator = new MediaUploadValidator(Options.Create(new MediaStorageOptions
        {
            RootPath = _root, RequireMalwareScanner = false, MaxFileSizeBytes = Png().Length - 1
        }), Array.Empty<IMalwareScanner>());
        var exception = await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(Png()), "sample.png", "image/png"), TestContext.Current.CancellationToken));
        Assert.Equal(ExternalServiceErrorCodes.Media.FileTooLarge, exception.Error.Code);
    }

    [Fact]
    public async Task Empty_stream_is_rejected()
    {
        var validator = CreateValidator();

        var exception = await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(), "empty.png", "image/png"), TestContext.Current.CancellationToken));

        Assert.Equal(ExternalServiceErrorCodes.Media.InvalidFile, exception.Error.Code);
    }

    [Fact]
    public async Task Jpeg_declared_as_png_is_rejected_by_signature()
    {
        var validator = CreateValidator();

        var exception = await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(Jpeg()), "image.png", "image/png"), TestContext.Current.CancellationToken));

        Assert.Equal(ExternalServiceErrorCodes.Media.SignatureMismatch, exception.Error.Code);
    }

    [Fact]
    public async Task Valid_mp4_ftyp_is_accepted_and_short_mp4_is_rejected()
    {
        var validator = CreateValidator();

        await using var valid = await validator.ValidateAsync(
            new MediaUpload(new MemoryStream(Mp4()), "clip.mp4", "video/mp4"), TestContext.Current.CancellationToken);
        var invalid = await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(new byte[] { 0, 0, 0, 1 }), "clip.mp4", "video/mp4"), TestContext.Current.CancellationToken));

        Assert.Equal("video/mp4", valid.ContentType);
        Assert.Equal(ExternalServiceErrorCodes.Media.SignatureMismatch, invalid.Error.Code);
    }

    [Theory]
    [InlineData("../evil.png")]
    [InlineData("folder/evil.png")]
    [InlineData("C:evil.png")]
    [InlineData("script.svg")]
    public async Task Unsafe_filenames_and_active_content_are_rejected(string fileName)
    {
        var validator = CreateValidator();

        await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(Png()), fileName, "image/png"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Seekable_stream_position_is_restored_and_caller_stream_is_not_disposed()
    {
        var validator = CreateValidator();
        var stream = new TrackingMemoryStream(Png());
        stream.Position = 2;

        await using var validated = await validator.ValidateAsync(new MediaUpload(stream, "sample.png", "image/png"), TestContext.Current.CancellationToken);

        Assert.Equal(2, stream.Position);
        Assert.False(stream.Disposed);
    }

    [Fact]
    public async Task Malware_scanner_can_reject_uploads()
    {
        var validator = CreateValidator(new RejectingScanner());

        var exception = await Assert.ThrowsAsync<MediaServiceException>(() =>
            validator.ValidateAsync(new MediaUpload(new MemoryStream(Png()), "sample.png", "image/png"), TestContext.Current.CancellationToken));

        Assert.Equal(ExternalServiceErrorCodes.Media.MalwareDetected, exception.Error.Code);
    }

    [Fact]
    public void Required_malware_scanner_missing_fails_service_creation()
    {
        var options = Options.Create(new MediaStorageOptions
        {
            RootPath = _root,
            RequireMalwareScanner = true
        });

        Assert.Throws<MediaServiceException>(() =>
            new MediaUploadValidator(options, Array.Empty<IMalwareScanner>()));
    }

    [Fact]
    public async Task File_system_storage_rejects_path_traversal_and_existing_key()
    {
        var service = CreateService();

        var stored = await service.SaveAsync(
            new MediaUpload(new MemoryStream(Png()), "sample.png", "image/png"),
            new MediaStorageRequest("uploads", "same.png"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar))));
        await Assert.ThrowsAsync<MediaServiceException>(() =>
            service.SaveAsync(
                new MediaUpload(new MemoryStream(Png()), "sample.png", "image/png"),
                new MediaStorageRequest("uploads", "same.png"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<MediaServiceException>(() => service.DeleteAsync("../outside.png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Batch_save_failure_deletes_current_batch_only()
    {
        var service = CreateService();
        var preExisting = await service.SaveAsync(
            new MediaUpload(new MemoryStream(Png()), "pre.png", "image/png"),
            new MediaStorageRequest("uploads", "pre.png"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<MediaServiceException>(() => service.SaveAsync(
            new[]
            {
                new MediaUpload(new MemoryStream(Png()), "one.png", "image/png"),
                new MediaUpload(new MemoryStream(Jpeg()), "bad.png", "image/png")
            },
            new MediaStorageRequest("batch"), TestContext.Current.CancellationToken));

        Assert.True(File.Exists(Path.Combine(_root, preExisting.Key.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(Path.Combine(_root, "batch")) &&
            Directory.EnumerateFiles(Path.Combine(_root, "batch")).Any(path => !Path.GetFileName(path).StartsWith('.')));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private MediaUploadValidator CreateValidator(params IMalwareScanner[] scanners)
        => new(
            Options.Create(new MediaStorageOptions { RootPath = _root, RequireMalwareScanner = false }),
            scanners);

    private MediaService CreateService()
    {
        var options = Options.Create(new MediaStorageOptions { RootPath = _root });
        return new MediaService(
            new MediaUploadValidator(options, Array.Empty<IMalwareScanner>()),
            new FileSystemMediaStorage(options, NullLogger<FileSystemMediaStorage>.Instance),
            NullLogger<MediaService>.Instance);
    }

    private static byte[] Png()
        => new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D
        };

    private static byte[] Jpeg()
        => new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01 };

    private static byte[] Mp4()
        => new byte[]
        {
            0x00, 0x00, 0x00, 0x18,
            0x66, 0x74, 0x79, 0x70,
            0x69, 0x73, 0x6F, 0x6D
        };

    private sealed class RejectingScanner : IMalwareScanner
    {
        public Task<MalwareScanResult> ScanAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken ct = default)
            => Task.FromResult(MalwareScanResult.Unsafe("test"));
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public TrackingMemoryStream(byte[] buffer)
            : base(buffer)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
