using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

public sealed class Phase18ReportExportTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Shared_model_renders_encoded_Razor_and_typed_safe_Excel_and_paginated_Pdf(bool arabic)
    {
        var executable = Environment.GetEnvironmentVariable("WASLA_CHROMIUM_PATH");
        using var renderer = new ReportDocumentRenderer(Options.Create(new Phase18Options { ChromiumExecutablePath = executable }));
        var report = Model(); var p = new ReportPresentation(report, arabic, false, "Doctor sample", "Practice sample");
        var html = await renderer.RenderHtmlAsync(p, TestContext.Current.CancellationToken);
        Assert.Contains("table-header-group", html); Assert.Contains("Content-Security-Policy", html);
        Assert.Contains(arabic ? "dir=\"rtl\"" : "dir=\"ltr\"", html);
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        var excel = renderer.RenderExcel(p);
        using (var book = new XLWorkbook(new MemoryStream(excel)))
        {
            Assert.Equal(7, book.Worksheets.Count); Assert.Equal(arabic, book.Worksheet(1).RightToLeft);
            Assert.Equal(XLColor.FromHtml("#143C50"), book.Worksheet(1).Cell(14, 1).Style.Fill.BackgroundColor);
            var details = book.Worksheet("05 Details"); Assert.Equal("0000123", details.Cell(2, 2).GetString());
            Assert.Equal(XLDataType.Text, details.Cell(3, 2).DataType); Assert.Equal("=2+2", details.Cell(3, 2).GetString()); Assert.False(details.Cell(3, 2).HasFormula);
            Assert.Equal(XLDataType.DateTime, details.Cell(2, 3).DataType); Assert.Equal(12.34m, details.Cell(2, 6).GetValue<decimal>());
            Assert.True(details.AutoFilter.IsEnabled); Assert.Equal(1, details.SheetView.SplitRow);
            Assert.Equal(600, details.LastRowUsed()!.RowNumber() - 1);
            Assert.Single(book.Worksheet("06 Charts").Pictures);
            Assert.Equal(0.5m, book.Worksheet("07 Comparisons").Cell(2, 7).GetValue<decimal>());
        }
        var output = Environment.GetEnvironmentVariable("WASLA_EXPORT_QA_DIRECTORY");
        if (output != null) { Directory.CreateDirectory(output); await File.WriteAllBytesAsync(Path.Combine(output, arabic ? "report-ar.xlsx" : "report-en.xlsx"), excel, TestContext.Current.CancellationToken); }
        if (executable == null) return;
        var pdf = await renderer.RenderPdfAsync(p, TestContext.Current.CancellationToken);
        Assert.True(pdf.IsSuccess, pdf.IsFailure ? string.Join(",", pdf.Errors.Select(e => e.Code)) : "");
        Assert.True(pdf.Value.AsSpan().StartsWith("%PDF"u8)); Assert.True(pdf.Value.Length > 10_000);
        if (output != null) await File.WriteAllBytesAsync(Path.Combine(output, arabic ? "report-ar.pdf" : "report-en.pdf"), pdf.Value, TestContext.Current.CancellationToken);
    }
    [Fact]
    public void Excel_handles_the_full_row_limit_and_empty_reports_with_no_silent_truncation()
    {
        using var renderer = new ReportDocumentRenderer(Options.Create(new Phase18Options()));
        var model = Model();
        var rows = Enumerable.Range(0, 25_000).Select(i => model.Records[i % model.Records.Count] with { Reference = "ROW-" + i }).ToArray();
        using var large = new XLWorkbook(new MemoryStream(renderer.RenderExcel(new(model with { Records = rows, DetailRowCount = rows.Length }, false, true, "QA", "QA"))));
        Assert.Equal("ROW-24999", large.Worksheet("05 Details").Cell(25_001, 2).GetString());
        using var empty = new XLWorkbook(new MemoryStream(renderer.RenderExcel(new(model with { Records = [], DetailRowCount = 0, Metrics = [], Series = [], PreviousPeriodComparisons = [] }, true, false, "QA", "QA"))));
        Assert.Equal(1, empty.Worksheet("05 Details").LastRowUsed()!.RowNumber());
        Assert.Empty(empty.Worksheet("06 Charts").Pictures);
    }
    [Fact]
    public void Logos_reject_unsupported_formats_truncated_headers_and_excessive_pixel_dimensions()
    {
        byte[] png = new byte[33]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0); "IHDR"u8.CopyTo(png.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 100);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), 100);
        Assert.True(ReportLogoPolicy.IsBoundedImage(png, "image/png"));
        Assert.False(ReportLogoPolicy.IsBoundedImage(png, "image/svg+xml"));
        Assert.False(ReportLogoPolicy.IsBoundedImage(png.AsSpan(0, 20), "image/png"));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 30_000);
        Assert.False(ReportLogoPolicy.IsBoundedImage(png, "image/png"));
        Assert.False(ReportLogoPolicy.IsBoundedImage(new byte[] { 255, 216, 255 }, "image/jpeg"));
    }
    [Fact]
    public async Task Missing_browser_returns_controlled_unavailable_and_real_health_result()
    {
        using var renderer = new ReportDocumentRenderer(Options.Create(new Phase18Options()));
        Assert.Equal("NotMeasured", renderer.Health.Status);
        var result = await renderer.RenderPdfAsync(new(Model(), true, false, "Doctor", "Practice"), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure); Assert.Equal("Report.ChromiumNotConfigured", result.Errors.First().Code);
        Assert.Equal("Unavailable", renderer.Health.Status);
    }
    private static ReportModel Model()
    {
        var now = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc); var date = new DateOnly(2026, 10, 4);
        var scope = new ReportScope(ReportActor.Doctor, Guid.NewGuid(), Guid.NewGuid(), null, [Guid.NewGuid()], []);
        ReportMetric[] metrics = [new("ReservationToTicketConversion", "التحول إلى تذكرة", "Conversion", 50, "percent", 10, 20, 20, 0, ReportDataStatus.Complete),
            new("GrossCollected", "المتحصلات", "Gross collected", 12.34m, "EGP", 12.34m, null, 1, 0, ReportDataStatus.Complete)];
        var rows = Enumerable.Range(0, 600).Select(i => new ReportRecord(Guid.NewGuid(), "Reservation", scope.PracticeIds[0], date,
            now, i == 0 ? "0000123" : i == 1 ? "=2+2" : i == 2 ? "<script>alert(1)</script>" : "REF-" + i, "Active", Amount: 12.34m)).ToArray();
        return new("reservations", "تقرير الحجوزات", "Reservation report", "Operational summary", new(date, date), scope, new(date, date, now, now, "Africa/Cairo"),
            now, ReportDataStatus.Complete, metrics, [new("2026-10-04", "2026-10-04", "2026-10-04", metrics)], [], [],
            ["Report.TestSyntheticData"], 600, 0, rows, 600,
            [new("ReservationToTicketConversion", date.AddDays(-1), date.AddDays(-1), 50, 100m / 3, 50m / 3, 50, ReportDataStatus.Complete)]);
    }
}
