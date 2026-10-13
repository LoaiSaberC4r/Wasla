using System.Data;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService(WaslaDbContext db, IReportingAccessGuard guard,
    IReportingTimeScope time, IKpiDefinitionRegistry registry) : IReportingQueryService
{
    private readonly OperationalKpiCalculator calculator = new(registry);
    public Task<Result<ReportModel>> GetAsync(ReportActor actor, string code, ReportFilter filter, CancellationToken ct)
        => BuildAsync(actor, code, filter, false, null, ct);
    public Task<Result<ReportModel>> BuildExportAsync(ReportActor actor, string code, ReportFilter filter, string format, CancellationToken ct)
        => BuildAsync(actor, code, filter, true, format, ct);

    private async Task<Result<ReportModel>> BuildAsync(ReportActor actor, string code, ReportFilter filter,
        bool export, string? format, CancellationToken ct)
    {
        var definition = ReportDefinitionCatalog.Definitions.SingleOrDefault(d => d.Code == code && d.Actors.Contains(actor));
        if (definition is null) return Result<ReportModel>.Fail(ReportingErrors.NotFound);
        if (export && format is not ("pdf" or "xlsx")) return Result<ReportModel>.Fail(ReportingErrors.InvalidFilter);
        var access = await guard.AuthorizeAsync(actor, export ? ReportDefinitionCatalog.ExportPermission(actor, definition.Financial)
            : ReportDefinitionCatalog.ViewPermission(actor), filter.PracticeId, filter.DoctorId, ct);
        if (access.IsFailure) return Result<ReportModel>.Fail(access.Errors);
        var view = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ViewPermission(actor), filter.PracticeId, filter.DoctorId, ct);
        if (view.IsFailure) return Result<ReportModel>.Fail(view.Errors);
        var scope = view.Value;
        if (definition.Financial && !FinancialPermissions(scope, code)) return Result<ReportModel>.Fail(ReportingErrors.Denied);
        var resolved = time.Resolve(filter);
        if (resolved.IsFailure) return Result<ReportModel>.Fail(resolved.Errors);
        var window = resolved.Value;
        if (!await ValidDimensionFiltersAsync(scope, filter, ct)) return Result<ReportModel>.Fail(ReportingErrors.NotFound);
        await using var transaction = db.Database.IsSqlServer()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await OperationalSourcesAsync(scope, filter, window, ct);
        var metrics = calculator.Calculate(source.Reservations, source.Tickets, window).ToList();
        var warnings = new List<string>();
        var clinicalAllowed = actor == ReportActor.SuperAdmin || actor == ReportActor.Doctor && scope.Permissions.Contains(PermissionNames.MedicalEncountersViewOwn);
        if (actor == ReportActor.Doctor && code is "overview" or "patients" or "practice-comparison")
            metrics.AddRange(await PatientMetricsAsync(scope, source.Tickets, window, clinicalAllowed, ct));
        if (actor == ReportActor.SuperAdmin) metrics.AddRange(await PlatformMetricsAsync(scope, code, window, ct));
        if (actor == ReportActor.Reception)
        {
            var receptionUsers = await db.Receptions.AsNoTracking().Where(r => db.ReceptionPracticeAssignments.Any(a => a.ReceptionId == r.Id && a.IsActive && scope.PracticeIds.Contains(a.DoctorPracticeId)))
                .Select(r => r.ApplicationUserId).ToArrayAsync(ct);
            var count = source.Reservations.Sum(r => r.Events.Count(e => e.ActorId != null && receptionUsers.Contains(e.ActorId.Value) &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), window))) +
                source.Tickets.Sum(t => t.Events.Count(e => e.ActorId != null && receptionUsers.Contains(e.ActorId.Value) &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), window)));
            metrics.Add(calculator.Count("ReceptionActivity", count));
        }
        var financeAllowed = actor == ReportActor.Doctor && FinancialPermissions(scope, "operating-finance");
        if (definition.Financial || code is "overview" or "practice-comparison" && financeAllowed)
            metrics.AddRange(await FinancialMetricsAsync(scope, filter, window, definition.Financial ? code : "operating-finance", ct));
        var records = await RecordsAsync(scope, code, source, filter, window, ct);
        if (export && records.Count > (format == "pdf" ? 5_000 : 25_000))
            return Result<ReportModel>.Fail(ExportTooLarge());
        var series = BuildSeries(source, filter.Grouping, window);
        var breakdowns = await BuildBreakdownsAsync(scope, source, window, ct);
        var uniquePatients = source.Tickets.Select(t => t.PatientId).Concat(source.Reservations.Select(r => r.PatientId)).Distinct().Count();
        if (actor == ReportActor.SuperAdmin && code is "overview" or "operations" or "adoption" && uniquePatients is > 0 and < 5)
        {
            warnings.Add("Report.SmallGroupAndComplementarySuppression");
            metrics = metrics.Select(m => IsPatientActivity(m.MetricCode) ? m with { Value = null, Numerator = null, Denominator = null,
                EligibleSampleCount = 0, ExcludedSampleCount = 0, DataStatus = ReportDataStatus.NotAvailable, WarningCode = "Report.SmallGroupSuppressed" } : m).ToList();
            series = []; breakdowns = [];
        }
        if (source.Reservations.Select(r => r.TimeZoneId).Concat(source.Tickets.Select(t => t.TimeZoneId)).Distinct().Skip(1).Any()) warnings.Add("Report.MixedPracticeTimeZones");
        warnings.AddRange(metrics.Where(m => m.WarningCode != null).Select(m => m.WarningCode!).Distinct());
        var normalized = filter with { FromDate = window.FromDate, ToDate = window.ToDate, AsOfUtc = window.AsOfUtc, ReportTimeZoneId = window.ReportTimeZoneId };
        var quality = metrics.Any(m => m.DataStatus != ReportDataStatus.Complete) ? ReportDataStatus.Partial : ReportDataStatus.Complete;
        var sorted = OrderRecords(records, filter).ToArray();
        var visible = export ? sorted : sorted.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        if (transaction != null) await transaction.CommitAsync(ct);
        return Result<ReportModel>.Ok(new(code, definition.TitleAr, definition.TitleEn, definition.TitleEn, normalized, scope,
            window, window.AsOfUtc, quality, metrics, series, breakdowns,
            [new("Drilldown", definition.Drilldown), new("ExportPdf", scope.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, definition.Financial))),
             new("ExportExcel", scope.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, definition.Financial)))],
            warnings.Distinct().ToArray(), source.Reservations.Count + source.Tickets.Count,
            metrics.Sum(m => m.ExcludedSampleCount), visible, sorted.LongLength));
    }
    public async Task<Result<IReadOnlyList<ReportCatalogEntry>>> CatalogAsync(ReportActor actor, CancellationToken ct)
    {
        var scope = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ViewPermission(actor), null, null, ct);
        if (scope.IsFailure) return Result<IReadOnlyList<ReportCatalogEntry>>.Fail(scope.Errors);
        return Result<IReadOnlyList<ReportCatalogEntry>>.Ok(ReportDefinitionCatalog.Definitions.Where(d => d.Actors.Contains(actor) &&
            (!d.Financial || FinancialPermissions(scope.Value, d.Code))).Select(d => new ReportCatalogEntry(d.Code, d.TitleAr, d.TitleEn,
                d.TitleEn, ["fromDate", "toDate", "practiceId", "doctorId", "segmentId", "visitTypeId", "bookingSource", "reservationStatus", "ticketStatus", "asOfUtc", "reportTimeZoneId", "grouping", "comparePreviousPeriod"],
                Enum.GetValues<ReportGrouping>(), actor == ReportActor.SuperAdmin ? "Authorized platform aggregates" : actor == ReportActor.Doctor ? "Owned Practices" : "Actively assigned and delegated Practices",
                true, d.Drilldown, scope.Value.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, d.Financial)),
                scope.Value.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, d.Financial)), new(),
                [ReportDefinitionCatalog.ViewPermission(actor), ReportDefinitionCatalog.ExportPermission(actor, d.Financial)], ReportDataStatus.Partial)).ToArray());
    }
    public async Task<Result<ReportFilterOptions>> FilterOptionsAsync(ReportActor actor, CancellationToken ct)
    {
        var scope = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ViewPermission(actor), null, null, ct);
        if (scope.IsFailure) return Result<ReportFilterOptions>.Fail(scope.Errors);
        var ids = scope.Value.PracticeIds;
        var practices = await db.DoctorPractices.AsNoTracking().Where(p => ids.Contains(p.Id)).OrderBy(p => p.NameAr)
            .Select(p => new ReportOption(p.Id.ToString(), p.NameAr, p.NameEn, p.Id)).ToArrayAsync(ct);
        var segments = await db.DoctorPracticeSegments.AsNoTracking().Where(s => ids.Contains(s.DoctorPracticeId))
            .Select(s => new ReportOption(s.Id.ToString(), s.NameAr, s.NameEn, s.Id)).ToArrayAsync(ct);
        var types = await db.DoctorPracticeVisitTypes.AsNoTracking().Where(t => ids.Contains(t.DoctorPracticeId))
            .Select(t => new ReportOption(t.Type.ToString(), t.NameAr, t.NameEn, t.Id)).ToArrayAsync(ct);
        var resolved = time.Resolve(new());
        return Result<ReportFilterOptions>.Ok(new(practices, segments, types,
            [new("Patient", "المريض", "Patient"), new("FamilyMember", "أحد أفراد الأسرة", "Family member"), new("Reception", "الاستقبال", "Reception")],
            [new("Active", "نشط", "Active"), new("Cancelled", "ملغى", "Cancelled"), new("NoShow", "لم يحضر", "No-show"), new("Expired", "منتهي", "Expired"), new("ConvertedToTicket", "تحول لتذكرة", "Converted to ticket")],
            [new("Waiting", "انتظار", "Waiting"), new("Called", "تم النداء", "Called"), new("InProgress", "الخدمة جارية", "In progress"), new("NoShow", "لم يحضر", "No-show"), new("Completed", "مكتمل تشغيليًا", "Operationally completed"), new("Cancelled", "ملغى", "Cancelled")],
            Enum.GetValues<ReportGrouping>(), resolved.Value.ReportTimeZoneId, new()));
    }
    public async Task<Result<ReportPage<ReportRecord>>> DrilldownAsync(ReportActor actor, string code, string metric,
        ReportFilter filter, CancellationToken ct)
    {
        if (!registry.Definitions.Any(d => d.MetricCode == metric)) return Result<ReportPage<ReportRecord>>.Fail(ReportingErrors.InvalidFilter);
        var definition = ReportDefinitionCatalog.Definitions.SingleOrDefault(d => d.Code == code && d.Actors.Contains(actor));
        if (definition is null || !definition.Drilldown) return Result<ReportPage<ReportRecord>>.Fail(ReportingErrors.NotFound);
        var result = await GetAsync(actor, code, filter, ct);
        if (result.IsFailure) return Result<ReportPage<ReportRecord>>.Fail(result.Errors);
        return Result<ReportPage<ReportRecord>>.Ok(new(result.Value.Records, result.Value.DetailRowCount, filter.PageNumber, filter.PageSize,
            result.Value.DataStatus, result.Value.Warnings));
    }
    private async Task<bool> ValidDimensionFiltersAsync(ReportScope scope, ReportFilter f, CancellationToken ct)
        => (f.SegmentId == null || await db.DoctorPracticeSegments.AnyAsync(s => s.Id == f.SegmentId && scope.PracticeIds.Contains(s.DoctorPracticeId), ct)) &&
           (f.VisitTypeId == null || await db.DoctorPracticeVisitTypes.AnyAsync(t => t.Id == f.VisitTypeId && scope.PracticeIds.Contains(t.DoctorPracticeId), ct));
    private static bool FinancialPermissions(ReportScope s, string code) => code == "salary"
        ? s.Permissions.Contains(Phase18PermissionNames.SalaryObligationsView)
        : code is "expenses" or "recurring-expenses" ? s.Permissions.Contains(Phase18PermissionNames.ExpensesView)
        : s.Permissions.Contains(Phase18PermissionNames.ExpensesView) && s.Permissions.Contains(Phase18PermissionNames.SalaryObligationsView) && s.Permissions.Contains(PermissionNames.DoctorRevenueViewOwn);
    private static bool IsPatientActivity(string metric) => metric is not ("RegisteredDoctors" or "PendingDoctors" or "ApprovedDoctors" or "RejectedDoctors" or
        "SuspendedDoctors" or "ActivePractices" or "ReceptionAccounts" or "ApprovalWaitMinutes" or "DoctorAcquisition" or "DoctorRetention");
    private static IEnumerable<ReportRecord> OrderRecords(IEnumerable<ReportRecord> rows, ReportFilter f) => f.SortBy switch
    {
        "Status" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Status).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Status).ThenBy(r => r.Id),
        "Reference" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Reference).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Reference).ThenBy(r => r.Id),
        "Amount" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Amount).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Amount).ThenBy(r => r.Id),
        _ => f.SortDirection == "asc" ? rows.OrderBy(r => r.BusinessDate).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.BusinessDate).ThenBy(r => r.Id)
    };
    internal static Error ExportTooLarge() => Error.Domain("Report.ExportTooLarge", Wasla.Domain.Resources.ErrorMessage.GetString("Phase18ExportTooLarge"),
        "Maximum 366 calendar days; PDF 5000 rows/15 MiB; XLSX 25000 rows/20 MiB. Narrow the filters.");
}
