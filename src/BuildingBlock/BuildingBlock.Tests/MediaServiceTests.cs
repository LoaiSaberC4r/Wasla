using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Infrastructure.Media;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Tests;

public sealed class MediaServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bb-media-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_async_validates_and_saves_non_seekable_stream()
    {
        var options = Options.Create(new MediaStorageOptions { RootPath = _root });
        var validator = new MediaUploadValidator(options, Array.Empty<IMalwareScanner>());
        var storage = new FileSystemMediaStorage(
            options,
            NullLogger<FileSystemMediaStorage>.Instance);
        var service = new MediaService(
            validator,
            storage,
            NullLogger<MediaService>.Instance);
        var content = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D
        };

        var stored = await service.SaveAsync(
            new MediaUpload(new NonSeekableStream(content), "sample.png", "image/png"),
            new MediaStorageRequest("uploads"),
            CancellationToken.None);

        var saved = await File.ReadAllBytesAsync(Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar)), TestContext.Current.CancellationToken);
        Assert.Equal(content, saved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
