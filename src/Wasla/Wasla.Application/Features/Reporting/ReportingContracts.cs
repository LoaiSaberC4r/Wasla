using BuildingBlock.Domain.Results;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Reporting;

public enum ReportActor { Doctor = 1, Reception = 2, SuperAdmin = 3, Patient = 4 }
public enum ReportDataStatus { Complete = 1, Partial = 2, NotAvailable = 3, NotMeasured = 4, NotInstrumented = 5, Stale = 6 }
public enum ReportGrouping { Day = 1, Week = 2, Month = 3, Practice = 4, Segment = 5, VisitType = 6, Source = 7, Status = 8 }

public sealed record ReportFilter(DateOnly? FromDate = null, DateOnly? ToDate = null,
    Guid? PracticeId = null, Guid? DoctorId = null, Guid? SegmentId = null, Guid? VisitTypeId = null,
    ReservationBookingSource? BookingSource = null, ReservationStatus? ReservationStatus = null,
    TicketStatus? TicketStatus = null, DateTime? AsOfUtc = null, string? ReportTimeZoneId = null,
    ReportGrouping Grouping = ReportGrouping.Day, bool ComparePreviousPeriod = false,
    int PageNumber = 1, int PageSize = 20, string SortBy = "Date", string SortDirection = "desc");

public sealed record ReportingWindow(DateOnly FromDate, DateOnly ToDate, DateTime AsOfUtc,
    DateTime GeneratedAtUtc, string ReportTimeZoneId);
public sealed record ReportScope(ReportActor Actor, Guid ActorId, Guid? DoctorId, Guid? PatientId,
    IReadOnlyList<Guid> PracticeIds, IReadOnlyList<string> Permissions);
public sealed record ReportMetric(string MetricCode, string NameAr, string NameEn, decimal? Value,
    string Unit, decimal? Numerator, decimal? Denominator, long EligibleSampleCount,
    long ExcludedSampleCount, ReportDataStatus DataStatus, string? WarningCode = null);
public sealed record ReportSeriesPoint(string Key, string NameAr, string? NameEn, IReadOnlyList<ReportMetric> Metrics);
public sealed record ReportAction(string Code, bool Allowed, string? Route = null, string? UnavailableReasonCode = null);
public sealed record ReportRecord(Guid Id, string ResourceType, Guid? PracticeId, DateOnly BusinessDate,
    DateTime RecordedAtUtc, string Reference, string Status, Guid? PatientId = null,
    decimal? Amount = null, string? DetailRoute = null, IReadOnlyList<ReportAction>? AvailableActions = null);
public sealed record ReportPage<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize,
    ReportDataStatus DataStatus = ReportDataStatus.Complete, IReadOnlyList<string>? Warnings = null);
public sealed record ReportModel(string ReportCode, string TitleAr, string TitleEn, string Description,
    ReportFilter AppliedFilters, ReportScope Scope, ReportingWindow Window, DateTime DataFreshnessUtc,
    ReportDataStatus DataStatus, IReadOnlyList<ReportMetric> Metrics, IReadOnlyList<ReportSeriesPoint> Series,
    IReadOnlyList<ReportSeriesPoint> Breakdowns, IReadOnlyList<ReportAction> AvailableActions,
    IReadOnlyList<string> Warnings, long EligibleSampleCount, long ExcludedSampleCount,
    IReadOnlyList<ReportRecord> Records, long DetailRowCount = 0);
public sealed record ExportLimits(int MaximumCalendarDays = 366, int ExcelMaximumRows = 25_000,
    int PdfMaximumRows = 5_000, int ExcelMaximumBytes = 20 * 1024 * 1024, int PdfMaximumBytes = 15 * 1024 * 1024);
public sealed record ReportCatalogEntry(string ReportCode, string TitleAr, string TitleEn, string Description,
    IReadOnlyList<string> AllowedFilters, IReadOnlyList<ReportGrouping> AvailableGroupings,
    string Scope, bool CanPreview, bool CanDrillDown, bool CanExportPdf, bool CanExportExcel,
    ExportLimits ExportLimits, IReadOnlyList<string> RequiredPermissions, ReportDataStatus DataStatus);
public sealed record ReportOption(string Code, string NameAr, string? NameEn, Guid? Id = null);
public sealed record ReportFilterOptions(IReadOnlyList<ReportOption> Practices,
    IReadOnlyList<ReportOption> Segments, IReadOnlyList<ReportOption> VisitTypes,
    IReadOnlyList<ReportOption> BookingSources, IReadOnlyList<ReportOption> ReservationStatuses,
    IReadOnlyList<ReportOption> TicketStatuses, IReadOnlyList<ReportGrouping> Groupings,
    string DefaultReportTimeZoneId, ExportLimits ExportLimits);
public sealed record KpiDefinition(string MetricCode, string NameAr, string NameEn, string Purpose,
    string Sources, string ActorScope, string DateBasis, string EligibleStates, string Numerator,
    string? Denominator, string Exclusions, string TimeZoneBasis, string Completeness,
    string AsOfBehavior, string Example, string Unit);

public interface IReportingAccessGuard
{
    Task<Result<ReportScope>> AuthorizeAsync(ReportActor actor, string permission, Guid? practiceId,
        Guid? doctorId, CancellationToken cancellationToken);
}
public interface IReportingQueryService
{
    Task<Result<ReportModel>> GetAsync(ReportActor actor, string reportCode, ReportFilter filter, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<ReportCatalogEntry>>> CatalogAsync(ReportActor actor, CancellationToken cancellationToken);
    Task<Result<ReportFilterOptions>> FilterOptionsAsync(ReportActor actor, CancellationToken cancellationToken);
    Task<Result<ReportPage<ReportRecord>>> DrilldownAsync(ReportActor actor, string reportCode,
        string metricCode, ReportFilter filter, CancellationToken cancellationToken);
    Task<Result<ReportModel>> BuildExportAsync(ReportActor actor, string reportCode, ReportFilter filter,
        string format, CancellationToken cancellationToken);
}
public interface IKpiDefinitionRegistry { IReadOnlyList<KpiDefinition> Definitions { get; } KpiDefinition FindDefinition(string code); }
public interface IReportingTimeScope { Result<ReportingWindow> Resolve(ReportFilter filter); }

public sealed class Phase18Options
{
    public string DefaultReportTimeZoneId { get; set; } = "Africa/Cairo";
    public int ObligationGenerationIntervalSeconds { get; set; } = 300;
    public bool ObligationGenerationEnabled { get; set; } = true;
    public string? ChromiumExecutablePath { get; set; }
    public int PdfTimeoutSeconds { get; set; } = 45;
    public int MaximumConcurrentPdfRenders { get; set; } = 2;
}
