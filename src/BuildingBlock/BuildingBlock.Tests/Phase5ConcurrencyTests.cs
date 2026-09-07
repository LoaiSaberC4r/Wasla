using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Infrastructure.Media;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections;

namespace BuildingBlock.Tests;

public sealed class Phase5ConcurrencyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bb-phase5-concurrency-{Guid.NewGuid():N}");

    [Fact]
    public async Task Keyed_semaphore_serializes_same_key_and_cleans_up_after_contention()
    {
        var semaphore = new KeyedSemaphore();
        var start = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inside = 0;
        var maxInside = 0;

        var tasks = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                using (await semaphore.WaitAsync("shared", CancellationToken.None))
                {
                    var current = Interlocked.Increment(ref inside);
                    maxInside = Math.Max(maxInside, current);
                    await Task.Delay(2);
                    Interlocked.Decrement(ref inside);
                }
            }))
            .ToArray();

        start.SetResult(null);
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxInside);
        Assert.Equal(0, GetLockCount(semaphore));
    }

    [Fact]
    public async Task Keyed_semaphore_removes_cancelled_waiters_without_leaking_entries()
    {
        var semaphore = new KeyedSemaphore();
        using var held = await semaphore.WaitAsync("cancelled", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            semaphore.WaitAsync("cancelled", cancellation.Token));

        Assert.Equal(1, GetLockCount(semaphore));
        held.Dispose();
        Assert.Equal(0, GetLockCount(semaphore));
    }

    [Fact]
    public async Task File_system_media_storage_saves_distinct_files_concurrently()
    {
        var service = CreateMediaService();

        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(index => service.SaveAsync(
                new MediaUpload(new MemoryStream(Png()), $"sample-{index}.png", "image/png"),
                new MediaStorageRequest("parallel", $"sample-{index}.png"))));

        Assert.Equal(32, results.Select(result => result.Key).Distinct(StringComparer.Ordinal).Count());
        foreach (var stored in results)
        {
            Assert.True(File.Exists(Path.Combine(_root, stored.Key.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private MediaService CreateMediaService()
    {
        var options = Options.Create(new MediaStorageOptions
        {
            RootPath = _root,
            AllowedExtensions = new[] { ".png" },
            AllowedMimeTypes = new[] { "image/png" }
        });

        return new MediaService(
            new MediaUploadValidator(options, Array.Empty<IMalwareScanner>()),
            new FileSystemMediaStorage(options, NullLogger<FileSystemMediaStorage>.Instance),
            NullLogger<MediaService>.Instance);
    }

    private static int GetLockCount(KeyedSemaphore semaphore)
    {
        var field = typeof(KeyedSemaphore).GetField(
            "_locks",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Lock dictionary field was not found.");

        return ((IDictionary)field.GetValue(semaphore)!).Count;
    }

    private static byte[] Png()
        => new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D
        };
}
