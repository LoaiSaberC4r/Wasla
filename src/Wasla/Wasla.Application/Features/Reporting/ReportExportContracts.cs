using BuildingBlock.Domain.Results;

namespace Wasla.Application.Features.Reporting;

public sealed record ReportExportFile(string FileName, string ContentType, byte[] Content);
public sealed record ReportPresentation(ReportModel Report, bool IsArabic, bool Landscape, string GeneratedBy,
    string ScopeLabel, string? LogoDataUrl = null);
public sealed record ExportRuntimeHealth(string Status, bool ExecutableConfigured, DateTime? LastProbeAtUtc, bool? LastProbeSucceeded,
    string? FailureCode, int MaximumConcurrentRenders, int TimeoutSeconds);
public interface IReportDocumentRenderer
{
    Task<string> RenderHtmlAsync(ReportPresentation p, CancellationToken ct);
    Task<Result<byte[]>> RenderPdfAsync(ReportPresentation p, CancellationToken ct);
    byte[] RenderExcel(ReportPresentation p);
    ExportRuntimeHealth Health { get; }
    Task ProbeAsync(CancellationToken ct);
}
public interface IReportExportService
{
    Task<Result<ReportExportFile>> ExportAsync(ReportActor actor, string reportCode, ReportFilter filter, string format,
        string language, bool landscape, CancellationToken ct);
}
