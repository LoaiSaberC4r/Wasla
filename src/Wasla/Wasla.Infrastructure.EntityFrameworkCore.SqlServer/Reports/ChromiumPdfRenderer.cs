using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public sealed record PdfRenderResult(byte[]? Content, string? FailureCode);

public interface IChromiumPdfRenderer
{
    bool BrowserDownloaded { get; }
    Task<PdfRenderResult> RenderAsync(Func<CancellationToken, Task<string>> html, PdfOptions pdf, CancellationToken ct);
}

// Only called with trusted server-generated HTML; never expose a client HTML/template endpoint.
public sealed class ChromiumPdfRenderer(IOptions<Phase18Options> options, IChromiumBrowserRuntime runtime,
    ILogger<ChromiumPdfRenderer> logger) : IChromiumPdfRenderer, IDisposable
{
    private readonly SemaphoreSlim _renders = new(options.Value.MaximumConcurrentPdfRenders, options.Value.MaximumConcurrentPdfRenders);
    public bool BrowserDownloaded => runtime.BrowserDownloaded;

    public async Task<PdfRenderResult> RenderAsync(Func<CancellationToken, Task<string>> html, PdfOptions pdf, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.PdfTimeoutSeconds));
        var entered = false;
        List<Task> pendingCleanup = [];
        var stage = "Render";
        try
        {
            await _renders.WaitAsync(deadline.Token); entered = true;
            stage = "HtmlRender";
            var content = await html(deadline.Token).WaitAsync(deadline.Token);
            if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("Empty server template.");
            stage = "BrowserDownload";
            await runtime.EnsureBrowserDownloadedAsync(deadline.Token);
            stage = "BrowserLaunch";
            await using var browser = await AcquireAsync(runtime.LaunchAsync(new LaunchOptions
            {
                Headless = true, Timeout = 1_000_000,
                Args = ["--disable-web-security", "--allow-file-access", "--allow-file-access-from-files", "--no-sandbox", "--disable-setuid-sandbox"]
            }), pendingCleanup, deadline.Token);
            stage = "Render";
            await using var page = await AcquireAsync(browser.NewPageAsync(), pendingCleanup, deadline.Token);
            await page.SetJavaScriptEnabledAsync(false).WaitAsync(deadline.Token);
            await page.SetOfflineModeAsync(true).WaitAsync(deadline.Token);
            await page.SetRequestInterceptionAsync(true).WaitAsync(deadline.Token);
            page.Request += BlockExternalRequest;
            await page.SetContentAsync(content, new SetContentOptions
            {
                WaitUntil = [WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0], Timeout = 1_000_000
            }).WaitAsync(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            await using var fonts = await AcquireAsync(page.EvaluateExpressionHandleAsync("document.fonts.ready"), pendingCleanup, deadline.Token);
            await page.EmulateMediaTypeAsync(MediaType.Print).WaitAsync(deadline.Token);
            var bytes = await page.PdfDataAsync(pdf).WaitAsync(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            return bytes.AsSpan().StartsWith("%PDF"u8) ? new(bytes, null) : new(null, "InvalidPdf");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            PdfRuntimeLog.Timeout(logger, stage);
            return new(null, "RenderTimeout");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PdfRuntimeLog.Failed(logger, stage, ex.GetType().Name);
            return new(null, stage == "Render" ? "RenderUnavailable" : stage + "Failed");
        }
        finally
        {
            if (entered)
            {
                if (pendingCleanup.Count == 0) _renders.Release();
                else _ = ReleaseAfterCleanupAsync(pendingCleanup);
            }
        }
    }

    private async void BlockExternalRequest(object? sender, RequestEventArgs e)
    {
        try
        {
            if (e.Request.Url.StartsWith("data:", StringComparison.Ordinal) || e.Request.Url == "about:blank")
                await e.Request.ContinueAsync();
            else await e.Request.AbortAsync();
        }
        catch (Exception ex)
        {
            PdfRuntimeLog.InterceptionEnded(logger, ex.GetType().Name);
        }
    }

    private async Task<T> AcquireAsync<T>(Task<T> acquisition, List<Task> pendingCleanup, CancellationToken ct) where T : IAsyncDisposable
    {
        try { return await acquisition.WaitAsync(ct); }
        catch (OperationCanceledException)
        {
            // Launch/NewPage do not accept cancellation. Dispose a resource even if it arrives late.
            pendingCleanup.Add(DisposeLateAsync(acquisition));
            throw;
        }
    }

    private async Task ReleaseAfterCleanupAsync(List<Task> cleanup)
    {
        try
        {
            // A cancelled in-flight launch still occupies a slot until it finishes and is disposed.
            await Task.WhenAll(cleanup);
            _renders.Release();
        }
        catch (Exception ex) { PdfRuntimeLog.CleanupEnded(logger, ex.GetType().Name); }
    }

    private async Task DisposeLateAsync<T>(Task<T> acquisition) where T : IAsyncDisposable
    {
        try { await using var resource = await acquisition; }
        catch (Exception ex) { PdfRuntimeLog.CleanupEnded(logger, ex.GetType().Name); }
    }

    public void Dispose() => _renders.Dispose();
}
