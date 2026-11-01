using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

internal static class PdfTestSupport
{
    internal static ReportDocumentRenderer Report(IChromiumPdfRenderer? pdf = null, string? root = null)
        => new(Options.Create(new Phase18Options()), new TestEnvironment { ContentRootPath = root ?? AppContext.BaseDirectory }, pdf ?? new UnavailablePdf());
    internal static ChromiumPdfRenderer Chromium(IChromiumBrowserRuntime runtime, Phase18Options? options = null)
        => new(Options.Create(options ?? new()), runtime, NullLogger<ChromiumPdfRenderer>.Instance);
    internal sealed class UnavailablePdf : IChromiumPdfRenderer
    {
        public bool BrowserDownloaded => false;
        public Task<PdfRenderResult> RenderAsync(Func<CancellationToken, Task<string>> html, PdfOptions pdf, CancellationToken ct)
            => Task.FromResult(new PdfRenderResult(null, "BrowserLaunchFailed"));
    }
    internal sealed class FixedPdf(PdfRenderResult result) : IChromiumPdfRenderer
    {
        public bool BrowserDownloaded => true;
        public Task<PdfRenderResult> RenderAsync(Func<CancellationToken, Task<string>> html, PdfOptions pdf, CancellationToken ct)
            => Task.FromResult(result);
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Wasla.Tests.Unit";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public class PdfInterfaceStub : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeMethod { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeMethod(targetMethod!, args);
    internal static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = Create<T, PdfInterfaceStub>();
        ((PdfInterfaceStub)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }
}
