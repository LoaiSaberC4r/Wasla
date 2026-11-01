using Microsoft.Extensions.Logging;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

internal static partial class PdfRuntimeLog
{
    [LoggerMessage(1810, LogLevel.Information, "Ensuring Puppeteer browser cache at {CacheDirectory}. The worker identity needs write/execute access and HTTPS access to the browser download source.")]
    internal static partial void Provisioning(ILogger logger, string cacheDirectory);
    [LoggerMessage(1811, LogLevel.Information, "Puppeteer browser provisioning completed. Rendering readiness is not yet verified.")]
    internal static partial void Provisioned(ILogger logger);
    [LoggerMessage(1812, LogLevel.Error, "Puppeteer browser provisioning failed ({ExceptionType}). Check cache permissions, free disk space, outbound HTTPS/proxy and antivirus policy.")]
    internal static partial void DownloadFailed(ILogger logger, string exceptionType);
    [LoggerMessage(1813, LogLevel.Warning, "PDF request timed out at {Stage}. Initial browser provisioning may still be completing; retry after checking runtime diagnostics.")]
    internal static partial void Timeout(ILogger logger, string stage);
    [LoggerMessage(1814, LogLevel.Error, "PDF generation failed at {Stage} ({ExceptionType}). Check deployed Razor templates/compilation references for HTML failures; browser execution policy, OS dependencies and fonts for launch/render failures. Document content is omitted.")]
    internal static partial void Failed(ILogger logger, string stage, string exceptionType);
    [LoggerMessage(1815, LogLevel.Debug, "PDF request interception ended ({ExceptionType}); the bounded page may have closed.")]
    internal static partial void InterceptionEnded(ILogger logger, string exceptionType);
    [LoggerMessage(1816, LogLevel.Warning, "Cancelled PDF resource acquisition/cleanup ended ({ExceptionType}).")]
    internal static partial void CleanupEnded(ILogger logger, string exceptionType);
}
