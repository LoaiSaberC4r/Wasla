using System.Diagnostics.CodeAnalysis;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "DispatchProxy returns boxed ValueTasks to the production caller, which awaits each value once.")]
public sealed class ChromiumPdfRendererTests
{
    [Fact]
    public async Task Concurrent_first_requests_share_one_download_and_cancelled_waiter_does_not_release_it()
    {
        using var coordinator = new BrowserDownloadCoordinator();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        async Task Download() { Interlocked.Increment(ref count); started.TrySetResult(); await finish.Task; }
        using var cancel = new CancellationTokenSource();
        var first = coordinator.EnsureAsync(Download, cancel.Token);
        await started.Task;
        var others = Enumerable.Range(0, 30).Select(_ => coordinator.EnsureAsync(Download, TestContext.Current.CancellationToken)).ToArray();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(coordinator.BrowserDownloaded); Assert.Equal(1, count);
        finish.SetResult(); await Task.WhenAll(others);
        await coordinator.EnsureAsync(Download, TestContext.Current.CancellationToken);
        Assert.True(coordinator.BrowserDownloaded); Assert.Equal(1, count);
    }

    [Fact]
    public async Task Failed_download_is_retryable_and_precancelled_requests_do_not_download()
    {
        using var coordinator = new BrowserDownloadCoordinator();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var calls = 0;
        Task Download() { ++calls; return Task.CompletedTask; }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.EnsureAsync(Download, cancel.Token));
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<IOException>(() => coordinator.EnsureAsync(() => Task.FromException(new IOException()), TestContext.Current.CancellationToken));
        Assert.False(coordinator.BrowserDownloaded);
        await coordinator.EnsureAsync(Download, TestContext.Current.CancellationToken);
        Assert.True(coordinator.BrowserDownloaded); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("HtmlRender", "HtmlRenderFailed")]
    [InlineData("BrowserDownload", "BrowserDownloadFailed")]
    [InlineData("BrowserLaunch", "BrowserLaunchFailed")]
    [InlineData("Render", "RenderUnavailable")]
    public async Task Failures_are_classified_and_browser_resources_are_disposed(string stage, string expected)
    {
        var runtime = new Runtime { FailureStage = stage };
        using var pdf = PdfTestSupport.Chromium(runtime);
        var result = await pdf.RenderAsync(_ => stage == "HtmlRender" ? Task.FromException<string>(new InvalidOperationException("private medical data")) : Task.FromResult("<p>trusted</p>"), new(), TestContext.Current.CancellationToken);
        Assert.Null(result.Content); Assert.Equal(expected, result.FailureCode);
        if (stage == "HtmlRender") Assert.DoesNotContain("download", runtime.Events);
        if (stage == "Render") { Assert.Contains("page-disposed", runtime.Events); Assert.Contains("browser-disposed", runtime.Events); }
    }

    [Fact]
    public async Task Lifecycle_matches_reference_and_preserves_options_and_disposal()
    {
        var runtime = new Runtime(); using var pdf = PdfTestSupport.Chromium(runtime);
        var settings = new PdfOptions { Format = PaperFormat.A4, Landscape = true, PrintBackground = true, DisplayHeaderFooter = true,
            HeaderTemplate = "header", FooterTemplate = "pageNumber totalPages", MarginOptions = new() { Bottom = "16mm" } };
        var result = await pdf.RenderAsync(_ => { runtime.Events.Add("html"); return Task.FromResult("<html lang='ar' dir='rtl'>عربي English</html>"); }, settings, TestContext.Current.CancellationToken);
        Assert.Null(result.FailureCode); Assert.True(result.Content!.AsSpan().StartsWith("%PDF"u8));
        Assert.Equal(["html", "download", "launch", "page", "scripts-disabled", "offline", "interception", "content", "fonts", "print", "pdf", "fonts-disposed", "page-disposed", "browser-disposed"], runtime.Events);
        Assert.Null(runtime.Launch!.ExecutablePath); Assert.True(runtime.Launch.Headless); Assert.Equal(1_000_000, runtime.Launch.Timeout);
        Assert.Equal(["--disable-web-security", "--allow-file-access", "--allow-file-access-from-files", "--no-sandbox", "--disable-setuid-sandbox"], runtime.Launch.Args);
        Assert.Equal([WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0], runtime.Content!.WaitUntil);
        Assert.Equal(1_000_000, runtime.Content.Timeout); Assert.Same(settings, runtime.Pdf);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Invalid_PDFs_and_prescription_byte_limits_fail_closed(bool large)
    {
        var runtime = new Runtime { Bytes = large ? new byte[new ExportLimits().PdfMaximumBytes + 1] : "not PDF"u8.ToArray() };
        if (large) "%PDF"u8.CopyTo(runtime.Bytes);
        using var pdf = PdfTestSupport.Chromium(runtime);
        var result = await pdf.RenderAsync(_ => Task.FromResult("trusted"), new(), TestContext.Current.CancellationToken);
        if (!large) { Assert.Equal("InvalidPdf", result.FailureCode); Assert.Null(result.Content); }
        else
        {
            // A valid oversized PDF reaches the existing caller-specific export limits.
            Assert.Null(result.FailureCode);
            var prescription = new PrescriptionPdfRenderer(pdf);
            var rejected = await prescription.RenderAsync(DoctorPatientPdfTests.Document(Wasla.Domain.Medications.PrescriptionVersionStatus.Finalized), TestContext.Current.CancellationToken);
            Assert.Equal("PrescriptionPdf.DocumentTooLarge", rejected.Errors.First().Code);
            var reports = PdfTestSupport.Report(new PdfTestSupport.FixedPdf(new(runtime.Bytes, null)));
            Assert.True((await reports.RenderPdfAsync(new(Phase18ReportExportTests.Model(), false, false, "QA", "QA"), TestContext.Current.CancellationToken)).IsSuccess);
            Assert.Equal("Report.ExportTooLarge", reports.Health.FailureCode);
            Assert.False(reports.Health.LastProbeSucceeded);
        }
        Assert.Contains("browser-disposed", runtime.Events);
    }

    [Fact]
    public async Task Caller_cancellation_disposes_page_browser_and_releases_shared_slot()
    {
        using var cancel = new CancellationTokenSource();
        var runtime = new Runtime { OnContent = () => cancel.Cancel() };
        using var pdf = PdfTestSupport.Chromium(runtime, new Phase18Options { MaximumConcurrentPdfRenders = 1 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pdf.RenderAsync(_ => Task.FromResult("trusted"), new(), cancel.Token));
        Assert.Contains("page-disposed", runtime.Events); Assert.Contains("browser-disposed", runtime.Events);
        runtime.OnContent = null;
        Assert.Null((await pdf.RenderAsync(_ => Task.FromResult("trusted"), new(), TestContext.Current.CancellationToken)).FailureCode);
    }

    [Fact]
    public async Task Timeout_is_controlled_and_a_browser_acquired_after_cancellation_is_disposed()
    {
        var arrival = new TaskCompletionSource<IBrowser>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new Runtime { PendingBrowser = arrival.Task };
        using var pdf = PdfTestSupport.Chromium(runtime, new Phase18Options { PdfTimeoutSeconds = 1, MaximumConcurrentPdfRenders = 1 });
        var result = await pdf.RenderAsync(_ => Task.FromResult("trusted"), new(), TestContext.Current.CancellationToken);
        Assert.Equal("RenderTimeout", result.FailureCode);
        using var waiting = new CancellationTokenSource();
        var queued = pdf.RenderAsync(_ => Task.FromResult("trusted"), new(), waiting.Token);
        Assert.Equal(1, runtime.Events.Count(e => e == "launch"));
        waiting.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        arrival.SetResult(PdfInterfaceStub.Create<IBrowser>((method, _) => method.Name == "DisposeAsync" ? Dispose() : throw new NotSupportedException(method.Name)));
        ValueTask Dispose() { disposed.SetResult(); return ValueTask.CompletedTask; }
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Excel_does_not_require_templates_or_browser_and_health_only_becomes_ready_after_render()
    {
        var runtime = new Runtime(); using var pdf = PdfTestSupport.Chromium(runtime);
        var missing = PdfTestSupport.Report(pdf, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(missing.RenderExcel(new(Phase18ReportExportTests.Model(), true, false, "QA", "QA"))));
        Assert.Equal(7, workbook.Worksheets.Count); Assert.Empty(runtime.Events); Assert.Equal("NotMeasured", missing.Health.Status);
        var failure = await missing.RenderPdfAsync(new(Phase18ReportExportTests.Model(), true, false, "QA", "QA"), TestContext.Current.CancellationToken);
        Assert.Equal("Report.HtmlRenderFailed", failure.Errors.First().Code); Assert.Empty(runtime.Events);
        var renderer = PdfTestSupport.Report(pdf);
        Assert.Equal("NotMeasured", renderer.Health.Status);
        Assert.True((await renderer.RenderPdfAsync(new(Phase18ReportExportTests.Model(), false, true, "QA", "QA"), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal("Ready", renderer.Health.Status); Assert.True(renderer.Health.LastProbeSucceeded); Assert.True(renderer.Health.BrowserDownloaded);
        Assert.False(renderer.Health.ExecutableConfigured);
    }

    private sealed class Runtime : IChromiumBrowserRuntime
    {
        public string? FailureStage { get; init; }
        public List<string> Events { get; } = [];
        public LaunchOptions? Launch { get; private set; }
        public SetContentOptions? Content { get; private set; }
        public PdfOptions? Pdf { get; private set; }
        public byte[] Bytes { get; init; } = "%PDF-test"u8.ToArray();
        public Action? OnContent { get; set; }
        public Task<IBrowser>? PendingBrowser { get; init; }
        public bool BrowserDownloaded { get; private set; }
        public Task EnsureBrowserDownloadedAsync(CancellationToken ct)
        {
            Events.Add("download"); if (FailureStage == "BrowserDownload") throw new IOException();
            BrowserDownloaded = true; return Task.CompletedTask;
        }
        public Task<IBrowser> LaunchAsync(LaunchOptions options)
        {
            Events.Add("launch"); Launch = options;
            if (FailureStage == "BrowserLaunch") throw new PuppeteerException("synthetic launch failure");
            return PendingBrowser ?? Task.FromResult(PdfInterfaceStub.Create<IBrowser>((method, args) => method.Name switch
            {
                "NewPageAsync" => NewPage(),
                "DisposeAsync" => Dispose("browser-disposed"),
                _ => throw new NotSupportedException(method.Name)
            }));
        }
        private ValueTask Dispose(string name) { Events.Add(name); return ValueTask.CompletedTask; }
        private Task<IPage> NewPage()
        {
            Events.Add("page");
            return Task.FromResult(PdfInterfaceStub.Create<IPage>((method, args) =>
            {
                switch (method.Name)
                {
                    case "SetJavaScriptEnabledAsync": Assert.False((bool)args![0]!); Events.Add("scripts-disabled"); return Task.CompletedTask;
                    case "SetOfflineModeAsync": Assert.True((bool)args![0]!); Events.Add("offline"); return Task.CompletedTask;
                    case "SetRequestInterceptionAsync": Assert.True((bool)args![0]!); Events.Add("interception"); return Task.CompletedTask;
                    case "add_Request": return null;
                    case "SetContentAsync":
                        Events.Add("content"); Content = (SetContentOptions)args![1]!; OnContent?.Invoke();
                        return FailureStage == "Render" ? Task.FromException(new PuppeteerException("synthetic render failure")) : Task.CompletedTask;
                    case "EvaluateExpressionHandleAsync":
                        Assert.Equal("document.fonts.ready", args![0]); Events.Add("fonts");
                        return Task.FromResult(PdfInterfaceStub.Create<IJSHandle>((m, _) => m.Name == "DisposeAsync" ? Dispose("fonts-disposed") : throw new NotSupportedException(m.Name)));
                    case "EmulateMediaTypeAsync": Events.Add("print"); return Task.CompletedTask;
                    case "PdfDataAsync": Events.Add("pdf"); Pdf = (PdfOptions)args![0]!; return Task.FromResult(Bytes);
                    case "DisposeAsync": return Dispose("page-disposed");
                    default: throw new NotSupportedException(method.Name);
                }
            }));
        }
    }
}
