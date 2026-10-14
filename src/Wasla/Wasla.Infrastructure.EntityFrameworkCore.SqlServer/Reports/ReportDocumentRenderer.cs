using System.Globalization;
using System.Net;
using BuildingBlock.Domain.Results;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using RazorLight;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public sealed class ReportDocumentRenderer : IReportDocumentRenderer, IDisposable
{
    private readonly Phase18Options _options;
    private readonly RazorLightEngine _razor;
    private readonly SemaphoreSlim _renders;
    private DateTime? _probeAt;
    private bool? _probeSucceeded;
    private string? _failureCode;
    public ReportDocumentRenderer(IOptions<Phase18Options> options)
    {
        _options = options.Value;
        _renders = new(_options.MaximumConcurrentPdfRenders, _options.MaximumConcurrentPdfRenders);
        _razor = new RazorLightEngineBuilder().UseEmbeddedResourcesProject(typeof(ReportDocumentRenderer).Assembly,
                "Wasla.Infrastructure.EntityFrameworkCore.SqlServer")
            .SetOperatingAssembly(typeof(ReportDocumentRenderer).Assembly).UseMemoryCachingProvider().Build();
    }
    public ExportRuntimeHealth Health => new(!_probeSucceeded.HasValue ? "NotMeasured" : _probeSucceeded.Value ? "Ready" : "Unavailable",
        !string.IsNullOrWhiteSpace(_options.ChromiumExecutablePath), _probeAt, _probeSucceeded, _failureCode,
        _options.MaximumConcurrentPdfRenders, _options.PdfTimeoutSeconds);
    public async Task<string> RenderHtmlAsync(ReportPresentation p, CancellationToken ct)
        => await _razor.CompileRenderAsync("Reports.Templates.Report", p).WaitAsync(ct);
    public async Task<Result<byte[]>> RenderPdfAsync(ReportPresentation p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ChromiumExecutablePath) || !Path.IsPathFullyQualified(_options.ChromiumExecutablePath) ||
            !File.Exists(_options.ChromiumExecutablePath)) return Unavailable("Report.ChromiumNotConfigured");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(_options.PdfTimeoutSeconds));
        var entered = false;
        try
        {
            await _renders.WaitAsync(deadline.Token); entered = true;
            var html = await RenderHtmlAsync(p, deadline.Token);
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = _options.ChromiumExecutablePath, Headless = true, Timeout = _options.PdfTimeoutSeconds * 1000,
                Args = ["--disable-background-networking", "--disable-extensions", "--disable-sync", "--no-first-run"]
            });
            await using var page = await browser.NewPageAsync();
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetOfflineModeAsync(true);
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) =>
            {
                try
                {
                    if (e.Request.Url.StartsWith("data:", StringComparison.Ordinal) || e.Request.Url == "about:blank")
                        await e.Request.ContinueAsync();
                    else await e.Request.AbortAsync();
                }
                catch (PuppeteerException) { /* A closed bounded render has no requests to continue. */ }
            };
            await page.SetContentAsync(html, new SetContentOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = _options.PdfTimeoutSeconds * 1000 }).WaitAsync(deadline.Token);
            await page.EmulateMediaTypeAsync(MediaType.Print).WaitAsync(deadline.Token);
            var bytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4, Landscape = p.Landscape, PrintBackground = true, DisplayHeaderFooter = true,
                HeaderTemplate = "<span></span>",
                FooterTemplate = "<div style='width:100%;text-align:center;font:9px sans-serif;color:#64748b'>WASLA · <span class='pageNumber'></span> / <span class='totalPages'></span></div>",
                MarginOptions = new MarginOptions { Top = "12mm", Bottom = "16mm", Left = "12mm", Right = "12mm" }
            }).WaitAsync(deadline.Token);
            _probeAt = DateTime.UtcNow; _probeSucceeded = true; _failureCode = null;
            return Result<byte[]>.Ok(bytes);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable("Report.RenderTimeout"); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Unavailable("Report.RenderUnavailable"); }
        finally { if (entered) _renders.Release(); }
    }
    public async Task ProbeAsync(CancellationToken ct)
    {
        var window = new ReportingWindow(new(2026, 1, 1), new(2026, 1, 1), DateTime.UtcNow, DateTime.UtcNow, "Africa/Cairo");
        var scope = new ReportScope(ReportActor.SuperAdmin, Guid.Empty, null, null, [], []);
        var report = new ReportModel("runtime-probe", "اختبار التصدير", "Export runtime probe", "", new(), scope, window, window.GeneratedAtUtc,
            ReportDataStatus.Complete, [], [], [], [], [], 0, 0, []);
        var result = await RenderPdfAsync(new(report, true, false, "WASLA", "Runtime probe"), ct);
        _probeAt = DateTime.UtcNow; _probeSucceeded = result.IsSuccess && result.Value.AsSpan().StartsWith("%PDF"u8);
        _failureCode = result.IsFailure ? result.Errors.First().Code : _probeSucceeded == false ? "Report.InvalidPdf" : null;
    }
    public byte[] RenderExcel(ReportPresentation p)
    {
        using var book = new XLWorkbook(); book.Properties.Author = p.GeneratedBy; book.Properties.Title = p.IsArabic ? p.Report.TitleAr : p.Report.TitleEn;
        var summary = Sheet(book, "01 Summary", p.IsArabic);
        Text(summary.Cell(1, 1), p.IsArabic ? p.Report.TitleAr : p.Report.TitleEn); summary.Range(1, 1, 1, 10).Merge();
        summary.Cell(1, 1).Style.Font.FontSize = 19; summary.Cell(1, 1).Style.Font.Bold = true; summary.Row(1).Height = 36;
        var metadata = new (string Label, string Value)[] {
            (T(p, "Generated by", "أنشأه"), p.GeneratedBy), (T(p, "Scope", "النطاق"), p.ScopeLabel),
            (T(p, "From date", "من تاريخ"), p.Report.Window.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            (T(p, "To date", "إلى تاريخ"), p.Report.Window.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            (T(p, "Time zone", "المنطقة الزمنية"), p.Report.Window.ReportTimeZoneId),
            (T(p, "As of UTC", "كما في UTC"), p.Report.Window.AsOfUtc.ToString("O", CultureInfo.InvariantCulture)),
            (T(p, "Generated UTC", "وقت الإنشاء UTC"), p.Report.Window.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            (T(p, "Data status", "حالة البيانات"), ReportPresentationText.Status(p.Report.DataStatus.ToString(), p.IsArabic)),
            (T(p, "Detail rows", "صفوف التفاصيل"), p.Report.DetailRowCount.ToString(CultureInfo.InvariantCulture)) };
        for (var i = 0; i < metadata.Length; ++i) { Text(summary.Cell(i + 3, 1), metadata[i].Label); Text(summary.Cell(i + 3, 2), metadata[i].Value); }
        summary.Cell(5, 2).Value = p.Report.Window.FromDate.ToDateTime(TimeOnly.MinValue); summary.Cell(5, 2).Style.NumberFormat.Format = "yyyy-mm-dd";
        summary.Cell(6, 2).Value = p.Report.Window.ToDate.ToDateTime(TimeOnly.MinValue); summary.Cell(6, 2).Style.NumberFormat.Format = "yyyy-mm-dd";
        summary.Cell(8, 2).Value = p.Report.Window.AsOfUtc; summary.Cell(8, 2).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
        summary.Cell(9, 2).Value = p.Report.Window.GeneratedAtUtc; summary.Cell(9, 2).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
        summary.Column(2).Width = 32;
        MetricTable(summary, p, p.Report.Metrics.Select(m => ("", m)), 14);
        var warnings = Sheet(book, "02 Filters and Quality", p.IsArabic); Header(warnings, 1, [T(p, "Filter / warning", "الفلتر أو التنبيه"), T(p, "Value", "القيمة")]);
        var filterRow = 2;
        foreach (var f in ReportPresentationText.Filters(p)) { Text(warnings.Cell(filterRow, 1), f.Label); Text(warnings.Cell(filterRow++, 2), f.Value); }
        foreach (var warning in p.Report.Warnings) { Text(warnings.Cell(filterRow, 1), T(p, "Data quality warning", "تنبيه جودة البيانات")); Text(warnings.Cell(filterRow++, 2), warning); }
        warnings.Column(1).Width = 36; warnings.Column(2).Width = 70; warnings.Range(1, 1, filterRow - 1, 2).SetAutoFilter();
        var trends = Sheet(book, "03 Trends", p.IsArabic);
        MetricTable(trends, p, p.Report.Series.SelectMany(s => s.Metrics.Select(m => (p.IsArabic ? s.NameAr : s.NameEn ?? s.Key, m))), 1);
        var groups = Sheet(book, "04 Breakdowns", p.IsArabic);
        MetricTable(groups, p, p.Report.Breakdowns.SelectMany(s => s.Metrics.Select(m => (p.IsArabic ? s.NameAr : s.NameEn ?? s.Key, m))), 1);
        var details = Sheet(book, "05 Details", p.IsArabic);
        Header(details, 1, [T(p, "Resource", "المورد"), T(p, "Reference", "المرجع"), T(p, "Business date", "تاريخ العمل"),
            T(p, "Recorded UTC", "وقت التسجيل UTC"), T(p, "Status", "الحالة"), T(p, "Amount EGP", "القيمة جنيه"), T(p, "Practice ID", "معرف العيادة")]);
        var row = 2;
        foreach (var r in p.Report.Records)
        {
            Text(details.Cell(row, 1), ReportPresentationText.Resource(r.ResourceType, p.IsArabic)); Text(details.Cell(row, 2), r.Reference);
            details.Cell(row, 3).Value = r.BusinessDate.ToDateTime(TimeOnly.MinValue); details.Cell(row, 3).Style.NumberFormat.Format = "yyyy-mm-dd";
            details.Cell(row, 4).Value = r.RecordedAtUtc; details.Cell(row, 4).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
            Text(details.Cell(row, 5), ReportPresentationText.Status(r.Status, p.IsArabic)); if (r.Amount is { } amount) details.Cell(row, 6).Value = amount;
            details.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00;[Red](#,##0.00);-";
            Text(details.Cell(row, 7), r.PracticeId?.ToString() ?? ""); ++row;
        }
        details.Range(1, 1, Math.Max(1, row - 1), 7).SetAutoFilter(); details.Columns(1, 7).Width = 24; details.Column(2).Width = 40; details.Column(7).Width = 40;
        details.Rows(2, Math.Max(2, row - 1)).Height = 28;
        var graphics = Sheet(book, "06 Charts", p.IsArabic);
        var values = ChartMetrics(p.Report);
        if (values.Length > 0)
        {
            Text(graphics.Cell(1, 1), T(p, "Selected percentage KPIs", "مؤشرات النسب المختارة"));
            using var png = new MemoryStream(ReportChartPngRenderer.Render(values.Select(m => m.Value!.Value).ToArray()));
            graphics.AddPicture(png).MoveTo(graphics.Cell(3, 4));
            for (var i = 0; i < values.Length; ++i) { Text(graphics.Cell(i * 2 + 3, 1), p.IsArabic ? values[i].NameAr : values[i].NameEn); graphics.Cell(i * 2 + 3, 2).Value = values[i].Value!.Value / 100; graphics.Cell(i * 2 + 3, 2).Style.NumberFormat.Format = "0.0%"; }
        }
        else Text(graphics.Cell(1, 1), T(p, "No measured percentage data", "لا توجد نسب مقاسة"));
        var comparisons = Sheet(book, "07 Comparisons", p.IsArabic);
        Header(comparisons, 1, [T(p, "Metric", "المؤشر"), T(p, "Previous from", "بداية الفترة السابقة"), T(p, "Previous to", "نهاية الفترة السابقة"),
            T(p, "Current", "الحالي"), T(p, "Previous", "السابق"), T(p, "Absolute change", "التغير المطلق"), T(p, "Relative change", "التغير النسبي"),
            T(p, "Data status", "حالة البيانات"), T(p, "Warning", "التنبيه")]);
        var comparisonRow = 2;
        foreach (var c in p.Report.PreviousPeriodComparisons ?? [])
        {
            var m = p.Report.Metrics.Single(x => x.MetricCode == c.MetricCode);
            Text(comparisons.Cell(comparisonRow, 1), p.IsArabic ? m.NameAr : m.NameEn);
            comparisons.Cell(comparisonRow, 2).Value = c.PreviousFromDate.ToDateTime(TimeOnly.MinValue);
            comparisons.Cell(comparisonRow, 3).Value = c.PreviousToDate.ToDateTime(TimeOnly.MinValue);
            comparisons.Range(comparisonRow, 2, comparisonRow, 3).Style.NumberFormat.Format = "yyyy-mm-dd";
            if (c.CurrentValue is { } current) comparisons.Cell(comparisonRow, 4).Value = m.Unit == "percent" ? current / 100 : current;
            if (c.PreviousValue is { } previous) comparisons.Cell(comparisonRow, 5).Value = m.Unit == "percent" ? previous / 100 : previous;
            if (c.AbsoluteChange is { } change) comparisons.Cell(comparisonRow, 6).Value = m.Unit == "percent" ? change / 100 : change;
            comparisons.Range(comparisonRow, 4, comparisonRow, 6).Style.NumberFormat.Format = m.Unit == "percent" ? "0.0%" : m.Unit == "EGP" ? "#,##0.00" : "#,##0.##";
            if (c.RelativeChangePercent is { } relative) comparisons.Cell(comparisonRow, 7).Value = relative / 100;
            comparisons.Cell(comparisonRow, 7).Style.NumberFormat.Format = "0.0%";
            Text(comparisons.Cell(comparisonRow, 8), ReportPresentationText.Status(c.DataStatus.ToString(), p.IsArabic));
            Text(comparisons.Cell(comparisonRow++, 9), c.WarningCode ?? "");
        }
        if (comparisonRow > 2) comparisons.Range(1, 1, comparisonRow - 1, 9).SetAutoFilter();
        else Text(comparisons.Cell(2, 1), T(p, "Previous-period comparison was not requested", "لم تُطلب المقارنة بالفترة السابقة"));
        using var stream = new MemoryStream(); book.SaveAs(stream); return stream.ToArray();
    }
    public static ReportMetric[] ChartMetrics(ReportModel report) => report.Metrics.Where(m => m.Unit == "percent" && m.Value != null).Take(6).ToArray();
    public static string ChartDataUrl(ReportModel report) => "data:image/png;base64," + Convert.ToBase64String(ReportChartPngRenderer.Render(ChartMetrics(report).Select(m => m.Value!.Value).ToArray()));
    public static string DecimalText(decimal? value) => value?.ToString("#,##0.##", CultureInfo.InvariantCulture) ?? "-";
    private static string T(ReportPresentation p, string en, string ar) => p.IsArabic ? ar : en;
    private static IXLWorksheet Sheet(XLWorkbook b, string name, bool rtl)
    {
        var s = b.Worksheets.Add(name); s.RightToLeft = rtl; s.ShowGridLines = false; s.Style.Font.FontName = "Tahoma"; s.Style.Font.FontSize = 11;
        s.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top; s.Style.Alignment.WrapText = true;
        s.Columns(1, 10).Width = 25; s.Column(1).Width = 38; s.SheetView.FreezeRows(1);
        s.PageSetup.PageOrientation = XLPageOrientation.Landscape; s.PageSetup.FitToPages(1, 0); s.PageSetup.SetRowsToRepeatAtTop(1, 1);
        return s;
    }
    private static void Header(IXLWorksheet s, int row, string[] labels)
    {
        for (var i = 0; i < labels.Length; ++i) Text(s.Cell(row, i + 1), labels[i]);
        var range = s.Range(row, 1, row, labels.Length); range.Style.Fill.BackgroundColor = XLColor.FromHtml("#143C50");
        range.Style.Font.FontColor = XLColor.White; range.Style.Font.Bold = true; s.Row(row).Height = 32;
    }
    private static void MetricTable(IXLWorksheet s, ReportPresentation p, IEnumerable<(string Group, ReportMetric Metric)> metrics, int headerRow)
    {
        Header(s, headerRow, [T(p, "Group / metric", "المجموعة أو المؤشر"), T(p, "Value", "القيمة"), T(p, "Unit", "الوحدة"), T(p, "Numerator", "البسط"),
            T(p, "Denominator", "المقام"), T(p, "Eligible", "المؤهل"), T(p, "Excluded", "المستبعد"), T(p, "Data status", "حالة البيانات"),
            T(p, "Metric code", "رمز المؤشر"), T(p, "Warning", "التنبيه")]);
        var row = headerRow + 1;
        foreach (var (group, m) in metrics)
        {
            Text(s.Cell(row, 1), (group == "" ? "" : group + " / ") + (p.IsArabic ? m.NameAr : m.NameEn));
            if (m.Value is { } value) s.Cell(row, 2).Value = m.Unit == "percent" ? value / 100 : value;
            s.Cell(row, 2).Style.NumberFormat.Format = m.Unit == "percent" ? "0.0%" : m.Unit == "EGP" ? "#,##0.00;[Red](#,##0.00);-" : "#,##0.##";
            Text(s.Cell(row, 3), ReportPresentationText.Unit(m.Unit, p.IsArabic)); if (m.Numerator is { } n) s.Cell(row, 4).Value = n; if (m.Denominator is { } d) s.Cell(row, 5).Value = d;
            s.Cell(row, 6).Value = m.EligibleSampleCount; s.Cell(row, 7).Value = m.ExcludedSampleCount;
            Text(s.Cell(row, 8), ReportPresentationText.Status(m.DataStatus.ToString(), p.IsArabic)); Text(s.Cell(row, 9), m.MetricCode); Text(s.Cell(row, 10), m.WarningCode ?? "");
            s.Row(row).Height = 36; ++row;
        }
        s.Range(headerRow, 1, Math.Max(headerRow, row - 1), 10).SetAutoFilter();
        s.SheetView.FreezeRows(headerRow); s.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }
    private static void Text(IXLCell cell, string value)
    {
        cell.Value = WebUtility.HtmlDecode(value); cell.Style.NumberFormat.Format = "@"; cell.Style.IncludeQuotePrefix = true;
    }
    private Result<byte[]> Unavailable(string code)
    {
        _probeAt = DateTime.UtcNow; _probeSucceeded = false; _failureCode = code;
        return Result<byte[]>.Fail(BuildingBlock.Domain.Results.Error.Infra(code,
            Wasla.Domain.Resources.ErrorMessage.GetString("Phase18ExportUnavailable")));
    }
    public void Dispose() => _renders.Dispose();
}
