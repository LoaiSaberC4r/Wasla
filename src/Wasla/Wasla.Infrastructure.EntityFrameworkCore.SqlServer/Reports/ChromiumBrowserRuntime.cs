using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public interface IChromiumBrowserRuntime
{
    bool BrowserDownloaded { get; }
    Task EnsureBrowserDownloadedAsync(CancellationToken ct);
    Task<IBrowser> LaunchAsync(LaunchOptions options);
}

public sealed class ChromiumBrowserRuntime(ILogger<ChromiumBrowserRuntime> logger) : IChromiumBrowserRuntime
{
    // One coordinator across all renderers and DI containers in this worker process.
    private static readonly BrowserDownloadCoordinator Downloads = new();
    public bool BrowserDownloaded => Downloads.BrowserDownloaded;
    public Task EnsureBrowserDownloadedAsync(CancellationToken ct) => Downloads.EnsureAsync(DownloadAsync, ct);
    public Task<IBrowser> LaunchAsync(LaunchOptions options) => Puppeteer.LaunchAsync(options);

    private async Task DownloadAsync()
    {
        var fetcher = new BrowserFetcher();
        PdfRuntimeLog.Provisioning(logger, fetcher.CacheDir);
        try
        {
            await fetcher.DownloadAsync();
            PdfRuntimeLog.Provisioned(logger);
        }
        catch (Exception ex)
        {
            // Exception messages from browser/HTML tooling can contain document data or local paths.
            PdfRuntimeLog.DownloadFailed(logger, ex.GetType().Name);
            throw;
        }
    }
}

internal sealed class BrowserDownloadCoordinator : IDisposable
{
    private readonly SemaphoreSlim _browserDownloadLock = new(1, 1);
    private volatile bool _browserDownloaded;
    private Task? _download;
    public bool BrowserDownloaded => _browserDownloaded;

    public async Task EnsureAsync(Func<Task> download, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_browserDownloaded) return;
        Task pending;
        await _browserDownloadLock.WaitAsync(ct);
        try
        {
            if (_browserDownloaded) return;
            if (_download is null || _download.IsFaulted || _download.IsCanceled)
            {
                _download = DownloadAsync(download);
                // A request can stop waiting before the non-cancellable library download finishes.
                _ = _download.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            pending = _download;
        }
        finally { _browserDownloadLock.Release(); }
        await pending.WaitAsync(ct);
    }

    private async Task DownloadAsync(Func<Task> download)
    {
        await download();
        _browserDownloaded = true;
    }

    public void Dispose() => _browserDownloadLock.Dispose();
}
