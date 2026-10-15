using System.Data;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class ReportingQueryService(WaslaDbContext db, IReportingAccessGuard guard,
    IReportingTimeScope time, IKpiDefinitionRegistry registry) : IReportingQueryService, IInsightEvidenceReader
{
    private readonly OperationalKpiCalculator calculator = new(registry);
    public Task<Result<ReportModel>> GetAsync(ReportActor actor, string code, ReportFilter filter, CancellationToken ct)
        => BuildAsync(actor, code, filter, false, null, null, ct);
    public Task<Result<ReportModel>> BuildExportAsync(ReportActor actor, string code, ReportFilter filter, string format, CancellationToken ct)
        => BuildAsync(actor, code, filter, true, format, null, ct);
    public Task<Result<ReportModel>> GetPracticeWindowAsync(Guid practiceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => BuildAsync(ReportActor.Doctor, "queue", new(fromDate, toDate, PracticeId: practiceId), false, null, Phase18PermissionNames.InsightsView, cancellationToken);

    private async Task<Result<ReportModel>> BuildAsync(ReportActor actor, string code, ReportFilter filter,
        bool export, string? format, string? accessPermission, CancellationToken ct)
    {
        var definition = ReportDefinitionCatalog.Definitions.SingleOrDefault(d => d.Code == code && d.Actors.Contains(actor));
        if (definition is null) return Result<ReportModel>.Fail(ReportingErrors.NotFound);
        if (!definition.Financial && (filter.ExpenseCategory != null || filter.ReceptionId != null || filter.PlanId != null))
            return Result<ReportModel>.Fail(ReportingErrors.InvalidFilter);
        if (definition.Financial && (filter.SegmentId != null || filter.VisitTypeId != null || filter.BookingSource != null || filter.ReservationStatus != null || filter.TicketStatus != null))
            return Result<ReportModel>.Fail(ReportingErrors.InvalidFilter);
        if (export && format is not ("pdf" or "xlsx")) return Result<ReportModel>.Fail(ReportingErrors.InvalidFilter);
        var requiredView = accessPermission ?? (code == "overview" ? ReportDefinitionCatalog.DashboardPermission(actor) : ReportDefinitionCatalog.ViewPermission(actor));
        var access = await guard.AuthorizeAsync(actor, export ? ReportDefinitionCatalog.ExportPermission(actor, definition.Financial)
            : requiredView, filter.PracticeId, filter.DoctorId, ct);
        if (access.IsFailure) return Result<ReportModel>.Fail(access.Errors);
        var view = await guard.AuthorizeAsync(actor, requiredView, filter.PracticeId, filter.DoctorId, ct);
        if (view.IsFailure) return Result<ReportModel>.Fail(view.Errors);
        var scope = view.Value;
        if (export && scope.PracticeIds.Any(p => !access.Value.PracticeIds.Contains(p)))
            return Result<ReportModel>.Fail(ReportingErrors.Denied);
        if (definition.Financial && !FinancialPermissions(scope, code)) return Result<ReportModel>.Fail(ReportingErrors.Denied);
        if (code == "security-metadata" && !scope.Permissions.Contains(Phase18PermissionNames.SecurityMetadataView))
            return Result<ReportModel>.Fail(ReportingErrors.Denied);
        var resolved = time.Resolve(filter);
        if (resolved.IsFailure)
        {
            if (export && filter.FromDate is { } from)
            {
                var bounded = time.Resolve(filter with { FromDate = null });
                if (bounded.IsSuccess && bounded.Value.ToDate.DayNumber - from.DayNumber >= 366)
                    return Result<ReportModel>.Fail(ExportTooLarge());
            }
            return Result<ReportModel>.Fail(resolved.Errors);
        }
        var window = resolved.Value;
        if (!await ValidDimensionFiltersAsync(scope, filter, ct)) return Result<ReportModel>.Fail(ReportingErrors.NotFound);
        await using var transaction = db.Database.IsSqlServer() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = definition.Financial ? new OperationalSources([], []) : await OperationalSourcesAsync(scope, filter, window, ct);
        var metrics = definition.Financial ? new List<ReportMetric>() : calculator.Calculate(source.Reservations, source.Tickets, window).ToList();
        var policySnapshots = definition.Financial ? [] : await CapacitySnapshotsAsync(scope, window, ct);
        if (!definition.Financial)
        {
            var capacity = CapacityMetrics(policySnapshots, scope, source, window, filter).ToDictionary(m => m.MetricCode);
            metrics = metrics.Select(m => capacity.GetValueOrDefault(m.MetricCode) ?? m).ToList();
        }
        var warnings = new List<string>();
        if (source.ExcludedSourceCount > 0) warnings.Add("Report.SourceHistoryIncomplete");
        if (filter.ExpenseCategory != null || filter.ReceptionId != null || filter.PlanId != null) warnings.Add("Report.ExpenseFiltersApplyOutgoingCosts");
        var clinicalAllowed = actor == ReportActor.SuperAdmin || actor == ReportActor.Doctor && scope.Permissions.Contains(PermissionNames.MedicalEncountersViewOwn);
        if (actor == ReportActor.Doctor && code is "overview" or "patients" or "practice-comparison")
            metrics.AddRange(await PatientMetricsAsync(scope, source.Tickets, window, clinicalAllowed, ct));
        if (actor == ReportActor.SuperAdmin) metrics.AddRange(await PlatformMetricsAsync(scope, code, window, ct));
        if (actor == ReportActor.Reception || actor == ReportActor.Doctor && code == "overview") metrics.AddRange(await HandoverMetricsAsync(scope, filter, window, ct));
        if (actor == ReportActor.Reception)
        {
            var receptionUsers = await db.Receptions.AsNoTracking().Where(r => db.DoctorPractices.Any(p => p.DoctorId == r.OwnerDoctorId && scope.PracticeIds.Contains(p.Id)))
                .Select(r => r.ApplicationUserId).ToArrayAsync(ct);
            var count = source.Reservations.Sum(r => r.Events.Count(e => e.ActorId != null && receptionUsers.Contains(e.ActorId.Value) &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, r.TimeZoneId), window))) +
                source.Tickets.Sum(t => t.Events.Count(e => e.ActorId != null && receptionUsers.Contains(e.ActorId.Value) &&
                OperationalKpiCalculator.InWindow(OperationalKpiCalculator.LocalDate(e.RecordedAtUtc, t.TimeZoneId), window)));
            metrics.Add(calculator.Count("ReceptionActivity", count));
        }
        var financeAllowed = actor == ReportActor.Doctor && FinancialPermissions(scope, "operating-finance") &&
            (!export || scope.Permissions.Contains(Phase18PermissionNames.DoctorExportFinancial));
        if (export && actor == ReportActor.Doctor && code is "overview" or "practice-comparison" && !scope.Permissions.Contains(Phase18PermissionNames.DoctorExportFinancial))
            warnings.Add("Report.FinancialColumnsExcludedForExport");
        FinancialSources? financial = null;
        if (definition.Financial || (code is "overview" or "practice-comparison") && financeAllowed)
        {
            financial = await FinancialSourcesAsync(scope, definition.Financial ? code : "operating-finance", window, ct, filter);
            metrics.AddRange(FinancialMetrics(financial, window, definition.Financial ? code : "operating-finance"));
        }
        var records = definition.Financial ? FinancialRecords(financial!, scope, window) : code == "activity"
            ? await OperationalDrilldownAsync(scope, "ReceptionActivity", source, filter, window, ct) : await RecordsAsync(scope, code, source, filter, window, ct);
        if (export && records.Count > (format == "pdf" ? 5_000 : 25_000))
            return Result<ReportModel>.Fail(ExportTooLarge());
        var series = definition.Financial ? FinancialSeries(financial!, window, code, filter.Grouping) : BuildSeries(source, filter.Grouping, window);
        var breakdowns = await BuildBreakdownsAsync(scope, source, window, filter.Grouping, financial, definition.Financial, code, policySnapshots, filter, ct);
        if (actor == ReportActor.SuperAdmin)
        {
            // Any small operational slice suppresses the whole slice family to prevent subtraction attacks.
            var smallSlice = source.Reservations.GroupBy(r => (r.PracticeId, r.ScheduledBusinessDate))
                .Any(g => g.Select(r => r.PatientId).Distinct().Count() < 5) || source.Tickets.GroupBy(t => (t.PracticeId, t.BusinessDate))
                .Any(g => g.Select(t => t.PatientId).Distinct().Count() < 5);
            if (smallSlice) { series = []; breakdowns = []; warnings.Add("Report.SmallGroupAndComplementarySuppression"); }
            breakdowns = breakdowns.Concat(await PlatformBreakdownsAsync(scope, code, window, ct)).ToArray();
        }
        var uniquePatients = source.Tickets.Select(t => t.PatientId).Concat(source.Reservations.Select(r => r.PatientId)).Distinct().Count();
        var patientCohortSizes = new[] { uniquePatients,
            source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.CreatedBusinessDate, window)).Select(r => r.PatientId).Distinct().Count(),
            source.Reservations.Where(r => OperationalKpiCalculator.InWindow(r.ScheduledBusinessDate, window) && r.ScheduledStartUtc <= window.AsOfUtc).Select(r => r.PatientId).Distinct().Count(),
            source.Tickets.Where(t => OperationalKpiCalculator.InWindow(t.BusinessDate, window)).Select(t => t.PatientId).Distinct().Count() };
        var privacySuppressed = actor == ReportActor.SuperAdmin && (patientCohortSizes.Any(size => size is > 0 and < 5) ||
            metrics.Any(m => IsPatientActivity(m.MetricCode) && (m.WarningCode == "Report.SmallGroupSuppressed" ||
                m.EligibleSampleCount is > 0 and < 5 || m.Unit == "count" && m.Value is > 0 and < 5 ||
                m.Unit == "percent" && m.Numerator is > 0 and < 5)));
        if (privacySuppressed)
        {
            warnings.Add("Report.SmallGroupAndComplementarySuppression");
            metrics = metrics.Select(m => IsPatientActivity(m.MetricCode) ? m with { Value = null, Numerator = null, Denominator = null,
                EligibleSampleCount = 0, ExcludedSampleCount = 0, DataStatus = ReportDataStatus.NotAvailable, WarningCode = "Report.SmallGroupSuppressed" } : m).ToList();
            series = []; breakdowns = [];
        }
        if (source.Reservations.Select(r => r.TimeZoneId).Concat(source.Tickets.Select(t => t.TimeZoneId)).Distinct().Skip(1).Any()) warnings.Add("Report.MixedPracticeTimeZones");
        warnings.AddRange(metrics.Where(m => m.WarningCode != null).Select(m => m.WarningCode!).Distinct());
        var normalized = filter with { FromDate = window.FromDate, ToDate = window.ToDate, AsOfUtc = window.AsOfUtc, ReportTimeZoneId = window.ReportTimeZoneId };
        var quality = source.ExcludedSourceCount > 0 || metrics.Any(m => m.DataStatus != ReportDataStatus.Complete) ? ReportDataStatus.Partial : ReportDataStatus.Complete;
        var sorted = OrderRecords(await ArchiveLinksAsync(records, scope, ct), filter).ToArray();
        metrics = metrics.Select(m =>
        {
            var allowed = definition.Drilldown && SupportsDrilldown(scope, m.MetricCode) && m.Value != null;
            return m with { AvailableActions = [new("Drilldown", allowed,
                allowed ? DrilldownRoute(actor, code, m.MetricCode, normalized) : null,
                allowed ? null : "Report.NoPermittedIndividualDetail")] };
        }).ToList();
        var visible = export ? sorted : sorted.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        List<ReportComparison> comparisons = [];
        if (filter.ComparePreviousPeriod)
        {
            var length = window.ToDate.DayNumber - window.FromDate.DayNumber + 1;
            if (window.FromDate.DayNumber <= length) return Result<ReportModel>.Fail(ReportingErrors.InvalidFilter);
            var previousFilter = filter with { FromDate = window.FromDate.AddDays(-length), ToDate = window.FromDate.AddDays(-1),
                AsOfUtc = window.AsOfUtc, ReportTimeZoneId = window.ReportTimeZoneId, ComparePreviousPeriod = false };
            var previous = await BuildAsync(actor, code, previousFilter, false, null, accessPermission, ct);
            if (previous.IsFailure) return Result<ReportModel>.Fail(previous.Errors);
            foreach (var current in metrics)
            {
                var earlier = previous.Value.Metrics.SingleOrDefault(m => m.MetricCode == current.MetricCode);
                var comparable = current.Value != null && earlier?.Value != null;
                comparisons.Add(new(current.MetricCode, previousFilter.FromDate!.Value, previousFilter.ToDate!.Value,
                    current.Value, earlier?.Value, comparable ? current.Value - earlier!.Value : null,
                    comparable && earlier!.Value != 0 ? (current.Value - earlier.Value) / Math.Abs(earlier.Value!.Value) * 100 : null,
                    comparable ? current.DataStatus == ReportDataStatus.Complete && earlier!.DataStatus == ReportDataStatus.Complete ? ReportDataStatus.Complete : ReportDataStatus.Partial : ReportDataStatus.NotAvailable,
                    !comparable ? "Report.PreviousPeriodNotComparable" : earlier!.Value == 0 ? "Report.PreviousPeriodZeroBaseline" : null));
            }
        }
        if (transaction != null) await transaction.CommitAsync(ct);
        return Result<ReportModel>.Ok(new(code, definition.TitleAr, definition.TitleEn, definition.TitleEn, normalized, scope,
            window, window.GeneratedAtUtc, quality, metrics, series, breakdowns,
            [new("Drilldown", definition.Drilldown), new("ExportPdf", scope.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, definition.Financial))),
             new("ExportExcel", scope.Permissions.Contains(ReportDefinitionCatalog.ExportPermission(actor, definition.Financial)))],
            warnings.Distinct().ToArray(), privacySuppressed ? 0 : source.Reservations.Count + source.Tickets.Count,
            privacySuppressed ? 0 : source.ExcludedSourceCount, visible, sorted.LongLength, comparisons));
    }
    private static string DrilldownRoute(ReportActor actor, string code, string metricCode, ReportFilter filter)
    {
        var fields = typeof(ReportFilter).GetProperties().Where(p => p.Name is not ("PageNumber" or "ComparePreviousPeriod"))
            .Select(p => (p.Name, Value: p.GetValue(filter))).Where(p => p.Value != null)
            .Select(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..] + "=" + Uri.EscapeDataString(p.Value switch
            {
                DateOnly date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                DateTime date => date.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                IFormattable value => value.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => p.Value!.ToString()!
            }));
        return $"/api/v1/{(actor == ReportActor.Doctor ? "doctors/me" : actor == ReportActor.Reception ? "reception" : "admin")}/reports/{code}/drilldown?metricCode={Uri.EscapeDataString(metricCode)}&" + string.Join('&', fields);
    }
    public async Task<Result<IReadOnlyList<ReportCatalogEntry>>> CatalogAsync(ReportActor actor, CancellationToken ct)
    {
        var scope = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ViewPermission(actor), null, null, ct);
        if (scope.IsFailure) return Result<IReadOnlyList<ReportCatalogEntry>>.Fail(scope.Errors);
        var operationalExport = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ExportPermission(actor, false), null, null, ct);
        var financialExport = actor == ReportActor.Doctor ? await guard.AuthorizeAsync(actor, Phase18PermissionNames.DoctorExportFinancial, null, null, ct) : operationalExport;
        var dashboard = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.DashboardPermission(actor), null, null, ct);
        List<ReportCatalogEntry> entries = [];
        foreach (var d in ReportDefinitionCatalog.Definitions.Where(d => d.Actors.Contains(actor) && (!d.Financial || FinancialPermissions(scope.Value, d.Code)) &&
            (d.Code != "security-metadata" || scope.Value.Permissions.Contains(Phase18PermissionNames.SecurityMetadataView))))
        {
            var preview = d.Code == "overview" ? dashboard : scope;
            if (preview.IsFailure) continue;
            var exportScope = d.Financial ? financialExport : operationalExport;
            var canExport = exportScope.IsSuccess && preview.Value.PracticeIds.All(p => exportScope.Value.PracticeIds.Contains(p));
            var routeBase = $"/api/v1/{(actor == ReportActor.Doctor ? "doctors/me" : actor == ReportActor.Reception ? "reception" : "admin")}";
            entries.Add(new(d.Code, d.TitleAr, d.TitleEn, d.TitleEn,
                d.Financial ? ["fromDate", "toDate", "practiceId", "expenseCategory", "receptionId", "planId", "asOfUtc", "reportTimeZoneId", "grouping", "comparePreviousPeriod"] :
                    ["fromDate", "toDate", "practiceId", "segmentId", "visitTypeId", "bookingSource", "reservationStatus", "ticketStatus", "asOfUtc", "reportTimeZoneId", "grouping", "comparePreviousPeriod"],
                d.Financial ? [ReportGrouping.Day, ReportGrouping.Week, ReportGrouping.Month, ReportGrouping.Practice] : Enum.GetValues<ReportGrouping>(),
                actor == ReportActor.SuperAdmin ? "Authorized platform aggregates" : actor == ReportActor.Doctor ? "Owned Practices" : "Actively assigned and delegated Practices",
                true, d.Drilldown, canExport, canExport, new(),
                [d.Code == "overview" ? ReportDefinitionCatalog.DashboardPermission(actor) : ReportDefinitionCatalog.ViewPermission(actor),
                    .. d.Financial ? RequiredFinancialPermissions(d.Code) : Array.Empty<string>(),
                    .. d.Code == "security-metadata" ? new[] { Phase18PermissionNames.SecurityMetadataView } : Array.Empty<string>()], ReportDataStatus.Partial,
                [ReportDefinitionCatalog.ExportPermission(actor, d.Financial)],
                PreviewRoute: routeBase + (d.Code == "overview" ? "/dashboard/overview" : "/reports/" + d.Code),
                DrilldownRoute: d.Drilldown ? routeBase + "/reports/" + d.Code + "/drilldown" : null,
                ExportRoute: canExport ? routeBase + "/reports/" + d.Code + "/export" : null));
        }
        return Result<IReadOnlyList<ReportCatalogEntry>>.Ok(entries);
    }
    public async Task<Result<ReportFilterOptions>> FilterOptionsAsync(ReportActor actor, CancellationToken ct, ReportOptionsFilter? filter = null)
    {
        var f = filter ?? new();
        if (f.PageNumber is < 1 or > 1_000_000 || f.PageSize is < 1 or > 100 || f.Search?.Length > 100) return Result<ReportFilterOptions>.Fail(ReportingErrors.InvalidFilter);
        var scope = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.DashboardPermission(actor), f.PracticeId, null, ct);
        if (scope.IsFailure) scope = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ViewPermission(actor), f.PracticeId, null, ct);
        if (scope.IsFailure) return Result<ReportFilterOptions>.Fail(scope.Errors);
        var ids = scope.Value.PracticeIds;
        var pq = db.DoctorPractices.AsNoTracking().Where(p => ids.Contains(p.Id) && (f.Search == null || p.NameAr.Contains(f.Search) || p.NameEn != null && p.NameEn.Contains(f.Search)));
        var sq = db.DoctorPracticeSegments.AsNoTracking().Where(s => ids.Contains(s.DoctorPracticeId) && (f.Search == null || s.NameAr.Contains(f.Search) || s.NameEn != null && s.NameEn.Contains(f.Search)));
        var vq = db.DoctorPracticeVisitTypes.AsNoTracking().Where(t => ids.Contains(t.DoctorPracticeId) && (f.Search == null || t.NameAr.Contains(f.Search) || t.NameEn != null && t.NameEn.Contains(f.Search)));
        var offset = (f.PageNumber - 1) * f.PageSize;
        var pages = new ReportOptionsPagination(f.PageNumber, f.PageSize, await pq.LongCountAsync(ct), await sq.LongCountAsync(ct), await vq.LongCountAsync(ct));
        var practices = await pq.OrderBy(p => p.NameAr).ThenBy(p => p.Id).Skip(offset).Take(f.PageSize).Select(p => new ReportOption(p.Id.ToString(), p.NameAr, p.NameEn, p.Id)).ToArrayAsync(ct);
        var segments = await sq.OrderBy(s => s.NameAr).ThenBy(s => s.Id).Skip(offset).Take(f.PageSize).Select(s => new ReportOption(s.Id.ToString(), s.NameAr, s.NameEn, s.Id)).ToArrayAsync(ct);
        var types = await vq.OrderBy(t => t.NameAr).ThenBy(t => t.Id).Skip(offset).Take(f.PageSize).Select(t => new ReportOption(t.Type.ToString(), t.NameAr, t.NameEn, t.Id)).ToArrayAsync(ct);
        var resolved = time.Resolve(new());
        return Result<ReportFilterOptions>.Ok(new(practices, segments, types,
            [new("Patient", "المريض", "Patient"), new("FamilyMember", "أحد أفراد الأسرة", "Family member"), new("Reception", "الاستقبال", "Reception")],
            [new("Active", "نشط", "Active"), new("Cancelled", "ملغى", "Cancelled"), new("NoShow", "لم يحضر", "No-show"), new("Expired", "منتهي", "Expired"), new("ConvertedToTicket", "تحول لتذكرة", "Converted to ticket")],
            [new("Waiting", "انتظار", "Waiting"), new("Called", "تم النداء", "Called"), new("InProgress", "الخدمة جارية", "In progress"), new("NoShow", "لم يحضر", "No-show"), new("Completed", "مكتمل تشغيليًا", "Operationally completed"), new("Cancelled", "ملغى", "Cancelled")],
            Enum.GetValues<ReportGrouping>(), resolved.Value.ReportTimeZoneId, new(), pages));
    }
    public async Task<Result<ReportPage<ReportRecord>>> DrilldownAsync(ReportActor actor, string code, string metric,
        ReportFilter filter, CancellationToken ct)
    {
        if (!registry.Definitions.Any(d => d.MetricCode == metric)) return Result<ReportPage<ReportRecord>>.Fail(ReportingErrors.InvalidFilter);
        var definition = ReportDefinitionCatalog.Definitions.SingleOrDefault(d => d.Code == code && d.Actors.Contains(actor));
        if (definition is null || !definition.Drilldown) return Result<ReportPage<ReportRecord>>.Fail(ReportingErrors.NotFound);
        var result = await GetAsync(actor, code, filter, ct);
        if (result.IsFailure) return Result<ReportPage<ReportRecord>>.Fail(result.Errors);
        var report = result.Value;
        if (!report.Metrics.Any(m => m.MetricCode == metric && m.Value != null) || !SupportsDrilldown(report.Scope, metric))
            return Result<ReportPage<ReportRecord>>.Fail(ReportingErrors.InvalidFilter);
        await using var transaction = db.Database.IsSqlServer() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var rows = FinancialDrilldownMetrics.Contains(metric) ? FinancialDrilldown(await FinancialSourcesAsync(report.Scope,
                definition.Financial ? code : "operating-finance", report.Window, ct, report.AppliedFilters), report.Scope, report.Window, metric)
            : actor == ReportActor.SuperAdmin ? await PlatformDrilldownAsync(report.Scope, code, report.Window, metric, ct)
            : await OperationalDrilldownAsync(report.Scope, metric, await OperationalSourcesAsync(report.Scope, report.AppliedFilters, report.Window, ct), report.AppliedFilters, report.Window, ct);
        var ordered = OrderRecords(await ArchiveLinksAsync(rows, report.Scope, ct), filter).ToArray();
        if (transaction != null) await transaction.CommitAsync(ct);
        var selected = report.Metrics.Single(m => m.MetricCode == metric);
        return Result<ReportPage<ReportRecord>>.Ok(new(ordered.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArray(),
            ordered.LongLength, filter.PageNumber, filter.PageSize, selected.DataStatus, selected.WarningCode == null ? [] : [selected.WarningCode]));
    }
    private async Task<bool> ValidDimensionFiltersAsync(ReportScope scope, ReportFilter f, CancellationToken ct)
        => (f.SegmentId == null || await db.DoctorPracticeSegments.AnyAsync(s => s.Id == f.SegmentId && scope.PracticeIds.Contains(s.DoctorPracticeId), ct)) &&
           (f.VisitTypeId == null || await db.DoctorPracticeVisitTypes.AnyAsync(t => t.Id == f.VisitTypeId && scope.PracticeIds.Contains(t.DoctorPracticeId), ct)) &&
           (f.ReceptionId == null || scope.Actor == ReportActor.Doctor && await db.Receptions.AnyAsync(r => r.Id == f.ReceptionId && r.OwnerDoctorId == scope.DoctorId, ct)) &&
           (f.PlanId == null || scope.Actor == ReportActor.Doctor && await db.OperatingFinancialPlans.AnyAsync(p => p.Id == f.PlanId && p.DoctorId == scope.DoctorId, ct));
    private static string[] RequiredFinancialPermissions(string code) => code == "salary" ? [Phase18PermissionNames.SalaryObligationsView]
        : code is "expenses" or "recurring-expenses" ? [Phase18PermissionNames.ExpensesView]
        : [Phase18PermissionNames.ExpensesView, Phase18PermissionNames.SalaryObligationsView, PermissionNames.DoctorRevenueViewOwn];
    private static bool FinancialPermissions(ReportScope s, string code) => RequiredFinancialPermissions(code).All(s.Permissions.Contains);
    private static bool IsPatientActivity(string metric) => OperationalDrilldownMetrics.Contains(metric) || metric is "RegisteredPatients" or "PatientAcquisition" or
        "PlatformActivePatient30d" or "PatientRetention" or "ClinicalWorkflowCompleted" or "BookedUtilization" or "ActualServiceUtilization" or "SegmentQuotaUtilization" or "UnusedCapacityMinutes";
    private static IEnumerable<ReportRecord> OrderRecords(IEnumerable<ReportRecord> rows, ReportFilter f) => (f.SortBy switch
    {
        "Status" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Status).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Status).ThenBy(r => r.Id),
        "Reference" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Reference).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Reference).ThenBy(r => r.Id),
        "Amount" => f.SortDirection == "asc" ? rows.OrderBy(r => r.Amount).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.Amount).ThenBy(r => r.Id),
        _ => f.SortDirection == "asc" ? rows.OrderBy(r => r.BusinessDate).ThenBy(r => r.Id) : rows.OrderByDescending(r => r.BusinessDate).ThenBy(r => r.Id)
    }).Select(r => r with { RecordedAtUtc = DateTime.SpecifyKind(r.RecordedAtUtc, DateTimeKind.Utc) });
    internal static Error ExportTooLarge() => Error.Domain("Report.ExportTooLarge", Wasla.Domain.Resources.ErrorMessage.GetString("Phase18ExportTooLarge"),
        "Maximum 366 calendar days; PDF 5000 rows/15 MiB; XLSX 25000 rows/20 MiB. Narrow the filters.");
}
