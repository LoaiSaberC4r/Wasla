using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Medications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Integration;

public sealed class ExportBrowserSmokeTests
{
    [Fact, Trait("Category", "BrowserIntegration")]
    public async Task Automatic_download_renders_bilingual_reports_and_every_issued_prescription_status()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WASLA_RUN_BROWSER_TESTS") == "1",
            "Set WASLA_RUN_BROWSER_TESTS=1 to exercise automatic browser download and launch.");
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Phase18__ChromiumExecutablePath")));
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WASLA_CHROMIUM_PATH")));
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH")));
        var ct = TestContext.Current.CancellationToken;
        var settings = Options.Create(new Phase18Options { PdfTimeoutSeconds = 120 });
        var runtime = new ChromiumBrowserRuntime(NullLogger<ChromiumBrowserRuntime>.Instance);
        using var chromium = new ChromiumPdfRenderer(settings, runtime, NullLogger<ChromiumPdfRenderer>.Instance);
        var root = Environment.GetEnvironmentVariable("WASLA_PUBLISHED_API_DIRECTORY") ?? AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(root, "Views", "Templates", "Report.cshtml")));
        var reports = new ReportDocumentRenderer(settings, new SmokeEnvironment { ContentRootPath = root }, chromium);
        var prescriptions = new PrescriptionPdfRenderer(chromium);
        var now = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc); var date = DateOnly.FromDateTime(now);
        var scope = new ReportScope(ReportActor.Doctor, Guid.NewGuid(), Guid.NewGuid(), null, [Guid.NewGuid()], []);
        ReportMetric[] metrics = [new("Conversion", "نسبة التحول", "Conversion", 50, "percent", 10, 20, 20, 0, ReportDataStatus.Complete)];
        var rows = Enumerable.Range(0, 600).Select(i => new ReportRecord(Guid.NewGuid(), "Reservation", scope.PracticeIds[0], date, now,
            i == 0 ? "<script>private</script>" : "REF-" + i, "Active", Amount: 12.34m)).ToArray();
        var model = new ReportModel("reservations", "تقرير الحجوزات", "Reservation report", "Smoke test", new(date, date), scope,
            new(date, date, now, now, "Africa/Cairo"), now, ReportDataStatus.Complete, metrics,
            [new("day", "اليوم", "Day", metrics)], [], [], ["Synthetic verification data"], rows.Length, 0, rows, rows.Length);
        Assert.Equal("NotMeasured", reports.Health.Status);
        foreach (var ar in new[] { true, false })
        {
            var p = new ReportPresentation(model, ar, !ar, "د. أحمد / Test doctor", "عيادة وصلة / Practice <script>scope</script>");
            var html = await reports.RenderHtmlAsync(p, ct);
            Assert.Contains(ar ? "dir=\"rtl\"" : "dir=\"ltr\"", html); Assert.Contains("&lt;script&gt;scope&lt;/script&gt;", html);
            Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private", html, StringComparison.Ordinal); // Executive PDF excludes row-level references.
            var result = await reports.RenderPdfAsync(p, ct);
            // A slow first download can outlive the bounded request; subsequent requests join it.
            for (var retry = 0; retry < 3 && result.IsFailure && result.Errors.First().Code == "Report.RenderTimeout" && !runtime.BrowserDownloaded; ++retry)
                result = await reports.RenderPdfAsync(p, ct);
            Assert.True(result.IsSuccess, result.IsFailure ? string.Join(",", result.Errors.Select(e => e.Code)) : "");
            Assert.True(result.Value.AsSpan().StartsWith("%PDF"u8)); Assert.True(result.Value.Length > 10_000);
            AssertA4(result.Value, !ar);
            await SaveAsync(ar ? "report-ar.pdf" : "report-en-landscape.pdf", result.Value, ct);
            var xlsx = reports.RenderExcel(p);
            using var workbook = new XLWorkbook(new MemoryStream(xlsx));
            Assert.Equal(7, workbook.Worksheets.Count); Assert.Equal(ar, workbook.Worksheet(1).RightToLeft);
            Assert.Equal(601, workbook.Worksheet("05 Details").LastRowUsed()!.RowNumber());
            Assert.Equal("<script>private</script>", workbook.Worksheet("05 Details").Cell(2, 2).GetString());
            await SaveAsync(ar ? "report-ar.xlsx" : "report-en.xlsx", xlsx, ct);
        }
        foreach (var status in new[] { PrescriptionVersionStatus.Finalized, PrescriptionVersionStatus.Superseded, PrescriptionVersionStatus.Voided })
        {
            var doc = new PrescriptionPdfDocument(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "د. أحمد / Doctor", "عيادة وصلة / Practice", "مريض تجريبي / Synthetic patient", now,
                new(Guid.NewGuid(), 2, status, null, "private-reason", now, now, status == PrescriptionVersionStatus.Voided ? now : null, "private-reason",
                    [new(Guid.NewGuid(), 1, MedicationSource.DoctorSubmitted, null, Guid.NewGuid(), "دواء تجريبي / Test medicine", "Synthetic",
                        new(Strength: "500 mg", DosageForm: "Tablet", Route: "Oral", Dose: "قرص واحد", FrequencyCode: MedicationFrequency.TwiceDaily,
                            DurationType: MedicationDurationType.Fixed, DurationValue: 5, DurationUnit: MedicationDurationUnit.Days, Instructions: "بعد الأكل / After meals"))]));
            var result = await prescriptions.RenderAsync(doc, ct);
            Assert.True(result.IsSuccess, result.IsFailure ? string.Join(",", result.Errors.Select(e => e.Code)) : "");
            Assert.True(result.Value.AsSpan().StartsWith("%PDF"u8)); Assert.True(result.Value.Length > 10_000);
            AssertA4(result.Value, false);
            await SaveAsync("prescription-" + status + ".pdf", result.Value, ct);
        }
        Assert.Equal("Ready", reports.Health.Status);
        Assert.True(runtime.BrowserDownloaded);
        Assert.True((await reports.RenderPdfAsync(new(model, false, false, "Repeat", "Practice"), ct)).IsSuccess);
    }

    private static void AssertA4(byte[] pdf, bool landscape)
    {
        // Chromium emits uncompressed page dictionaries; verify actual PDF geometry, not only Launch/PdfOptions.
        var box = System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.ASCII.GetString(pdf),
            @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(box.Success, "Expected Chromium PDF page dictionary.");
        var width = double.Parse(box.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var height = double.Parse(box.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(width, landscape ? 840 : 594, landscape ? 844 : 598);
        Assert.InRange(height, landscape ? 594 : 840, landscape ? 598 : 844);
    }

    private static async Task SaveAsync(string name, byte[] bytes, CancellationToken ct)
    {
        var path = Environment.GetEnvironmentVariable("WASLA_EXPORT_QA_DIRECTORY");
        if (path is null) return;
        Directory.CreateDirectory(path); await File.WriteAllBytesAsync(Path.Combine(path, name), bytes, ct);
    }
    private sealed class SmokeEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Wasla.Api";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
