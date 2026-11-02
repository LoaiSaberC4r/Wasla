using ClosedXML.Excel;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

public sealed class Phase18ReportExportTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Shared_model_renders_encoded_Razor_and_typed_safe_Excel_and_paginated_Pdf(bool arabic)
    {
        var renderer = PdfTestSupport.Report();
        var report = Model(); var p = new ReportPresentation(report, arabic, false, "Doctor sample", "Practice sample");
        var html = await renderer.RenderHtmlAsync(p, TestContext.Current.CancellationToken);
        Assert.Contains("table-header-group", html); Assert.Contains("Content-Security-Policy", html);
        Assert.Contains(arabic ? "dir=\"rtl\"" : "dir=\"ltr\"", html);
        Assert.Contains("size:A4 portrait", html);
        Assert.Contains("size:A4 landscape", await renderer.RenderHtmlAsync(p with { Landscape = true }, TestContext.Current.CancellationToken));
        var escapedHtml = await renderer.RenderHtmlAsync(p with { ScopeLabel = "<script>alert(1)</script> & synthetic" }, TestContext.Current.CancellationToken);
        Assert.Contains("&lt;script&gt;", escapedHtml); Assert.Contains("&amp; synthetic", escapedHtml);
        Assert.DoesNotContain("<script>", escapedHtml); Assert.DoesNotContain("<script>", html);
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
    }
    [Fact]
    public void Excel_handles_the_full_row_limit_and_empty_reports_with_no_silent_truncation()
    {
        var renderer = PdfTestSupport.Report();
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
    public async Task Launch_failure_returns_controlled_unavailable_and_real_health_result()
    {
        var renderer = PdfTestSupport.Report();
        Assert.Equal("NotMeasured", renderer.Health.Status);
        Assert.Equal("AutomaticDownload", renderer.Health.BrowserProvisioning);
        Assert.Null(renderer.Health.LastProbeSucceeded);
        var result = await renderer.RenderPdfAsync(new(Model(), true, false, "Doctor", "Practice"), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure); Assert.Equal("Report.BrowserLaunchFailed", result.Errors.First().Code);
        Assert.Equal("Unavailable", renderer.Health.Status);
    }
    [Theory]
    [InlineData(ReportActor.Doctor, "overview", "AppointmentsScheduled CheckIns ReservationToTicketConversion CancellationRateScheduledCohort ReservationNoShowRate", "Series")]
    [InlineData(ReportActor.Doctor, "reservations", "BookingsCreated AppointmentsScheduled ReservationToTicketConversion CancellationRateScheduledCohort ReservationNoShowRate", "Series")]
    [InlineData(ReportActor.Doctor, "queue", "CheckIns TicketsCompleted TicketsNoShow WaitToFirstCall WaitToServiceStart", "Series")]
    [InlineData(ReportActor.Doctor, "patients", "UniqueDoctorPatients NewToDoctorPatients ReturningDoctorPatients PatientRetention90d ClinicalWorkflowCompleted", "Metrics")]
    [InlineData(ReportActor.Doctor, "practice-comparison", "AppointmentsScheduled CheckIns WaitToFirstCall ReservationToTicketConversion NetCollected", "Practices")]
    [InlineData(ReportActor.Doctor, "operating-finance", "GrossCollected Refunds NetCollected PaidOperatingExpenses NetOperatingCashResult", "Series")]
    [InlineData(ReportActor.Doctor, "expenses", "PaidOperatingExpenses OutstandingDueInPeriod AllOutstandingAsOf OverdueAsOf", "Series")]
    [InlineData(ReportActor.Doctor, "salary", "PaidOperatingExpenses OutstandingDueInPeriod AllOutstandingAsOf OverdueAsOf", "Series")]
    [InlineData(ReportActor.Doctor, "recurring-expenses", "PaidOperatingExpenses OutstandingDueInPeriod AllOutstandingAsOf OverdueAsOf", "Series")]
    [InlineData(ReportActor.Reception, "overview", "AppointmentsScheduled CheckIns TicketsCompleted WaitToFirstCall WaitToServiceStart", "Series")]
    [InlineData(ReportActor.Reception, "reservations", "BookingsCreated AppointmentsScheduled ReservationToTicketConversion CancellationRateScheduledCohort ReservationNoShowRate", "BookingWeekday:")]
    [InlineData(ReportActor.Reception, "queue", "CheckIns TicketsCompleted TicketsNoShow WaitToFirstCall WaitToServiceStart", "Series")]
    [InlineData(ReportActor.Reception, "activity", "ReceptionActivity", "Metrics")]
    [InlineData(ReportActor.SuperAdmin, "overview", "RegisteredDoctors PendingDoctors ApprovedDoctors ActivePractices CurrentBookableDoctors", "Approval:")]
    [InlineData(ReportActor.SuperAdmin, "doctor-lifecycle", "RegisteredDoctors PendingDoctors ApprovedDoctors RejectedDoctors SuspendedDoctors", "Approval:")]
    [InlineData(ReportActor.SuperAdmin, "adoption", "DoctorAcquisition PatientAcquisition DoctorRetention PatientRetention FeatureBookingDoctors", "DoctorRegistrationCohort:")]
    [InlineData(ReportActor.SuperAdmin, "operations", "ActivePractices CurrentBookableDoctors PublicEligibleDoctors CheckIns TicketsCompleted", "Series")]
    [InlineData(ReportActor.SuperAdmin, "geography", "PracticeCoverage SpecializationCoverage", "Governorate:")]
    [InlineData(ReportActor.SuperAdmin, "security-metadata", "ActiveUserAccounts InactiveUserAccounts RoleAssignments PermissionGrants AssignedPracticePermissionGrants", "Role:")]
    public void PdfPrint_Actor_code_mapping_is_explicit_and_ordered(ReportActor actor, string code, string headlines, string primary)
    {
        var spec = Assert.IsType<PdfPrintSpec>(PdfPrintPresentation.Find(actor, code));
        Assert.Equal(headlines.Split(' '), spec.Headlines); Assert.InRange(spec.Headlines.Length, 1, 5);
        Assert.Equal(primary, spec.Sources[0].Source); Assert.NotEmpty(spec.QuestionAr); Assert.NotEmpty(spec.QuestionEn);
        Assert.Equal(19, ReportDefinitionCatalog.Definitions.Sum(d => d.Actors.Count));
        foreach (var definition in ReportDefinitionCatalog.Definitions)
            foreach (var candidate in Enum.GetValues<ReportActor>())
                Assert.Equal(definition.Actors.Contains(candidate), PdfPrintPresentation.Find(candidate, definition.Code) != null);
        Assert.Null(PdfPrintPresentation.Find(actor, "UNKNOWN"));
    }

    [Theory]
    [InlineData(ReportDataStatus.Complete, 0, true)]
    [InlineData(ReportDataStatus.Partial, 12, true)]
    [InlineData(ReportDataStatus.NotMeasured, 99, false)]
    [InlineData(ReportDataStatus.NotInstrumented, 99, false)]
    [InlineData(ReportDataStatus.NotAvailable, 99, false)]
    [InlineData(ReportDataStatus.Stale, 99, false)]
    public void PdfPrint_Headlines_select_source_codes_and_distinguish_unavailable_from_zero(ReportDataStatus status, int value, bool measured)
    {
        var selected = PrintMetric("BookingsCreated", value) with { DataStatus = status };
        var r = Model() with { Metrics = [PrintMetric("UnrelatedPercent", 98, "percent"), selected], Series = [], Breakdowns = [] };
        var print = PdfPrintPresentation.Build(new(r, false, false, "QA", "Synthetic practice"));
        Assert.Same(selected, Assert.Single(print.Cards)); Assert.Equal(measured, PdfPrintPresentation.Measured(selected));
        Assert.Equal(measured ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—", PdfPrintPresentation.Value(selected));
        Assert.All(print.Sections, s => { Assert.Null(s.ChartDataUrl); Assert.Empty(s.Points); });
        Assert.False(PdfPrintPresentation.Measured(selected with { Value = null }));
        Assert.False(PdfPrintPresentation.Measured(PrintMetric("ReservationToTicketConversion", 0, "percent") with { Denominator = 0 }));
        Assert.NotEqual(PdfPrintPresentation.State(ReportDataStatus.NotMeasured, false), PdfPrintPresentation.State(ReportDataStatus.NotInstrumented, false));
    }

    [Fact]
    public async Task PdfPrint_Charts_preserve_cohorts_units_signed_flows_and_authorized_practices()
    {
        var model = Model() with { Metrics = [PrintMetric("CheckIns", 77)], Records = [], Series = [], PreviousPeriodComparisons = [] };
        var cohort = new ReportSeriesPoint("BookingCreationCohortFunnel", "مجموعة الإنشاء", "Creation cohort",
            [PrintMetric("TicketsCompleted", 42), PrintMetric("CheckIns", 63), PrintMetric("BookingsCreated", 100)]);
        var p = new ReportPresentation(model with { Breakdowns = [cohort] }, false, false, "QA", "QA");
        var funnel = PdfPrintPresentation.Build(p).Sections.Single(s => s.Source == cohort.Key);
        Assert.Equal(new decimal?[] { 100, 63, 42 }, funnel.Points.Select(x => x.Metric.Value)); Assert.NotNull(funnel.ChartDataUrl);
        var queue = PdfPrintPresentation.Build(p with { Report = model with { ReportCode = "queue", Metrics = [PrintMetric("CheckIns", 77), PrintMetric("WaitToFirstCall", 8, "minutes"), PrintMetric("WaitToServiceStart", 12, "minutes"), PrintMetric("ServiceDuration", 5, "minutes")] } });
        var durations = queue.Sections.Single(s => s.Source == "Metrics");
        Assert.All(durations.Points, x => Assert.Equal("minutes", x.Metric.Unit)); Assert.NotNull(durations.ChartDataUrl);
        var flows = new[] { new ReportSeriesPoint("2026-10-01", "أكتوبر", "October", [PrintMetric("NetCollected", -25, "EGP"), PrintMetric("PaidOperatingExpenses", 10, "EGP"), PrintMetric("AllOutstandingAsOf", 900, "EGP")]),
            new ReportSeriesPoint("2026-10-02", "أكتوبر", "October", [PrintMetric("NetCollected", 50, "EGP"), PrintMetric("PaidOperatingExpenses", 0, "EGP"), PrintMetric("AllOutstandingAsOf", 800, "EGP")]) };
        var finance = PdfPrintPresentation.Build(p with { Report = model with { ReportCode = "operating-finance", Series = flows, Metrics = [PrintMetric("AllOutstandingAsOf", 800, "EGP")] } });
        var flow = finance.Sections[0]; Assert.DoesNotContain(flow.Points, x => x.Metric.MetricCode == "AllOutstandingAsOf");
        var svg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Assert.IsType<string>(flow.ChartDataUrl).Split(',')[1]));
        Assert.Contains("-25", svg); Assert.Contains("2026-10-01", svg); Assert.Contains("stroke-dasharray", svg);
        var missing = flows[1] with { Metrics = [PrintMetric("NetCollected", 50, "EGP")] };
        Assert.NotNull(PdfPrintPresentation.Build(p with { Report = model with { ReportCode = "operating-finance", Series = [flows[0], missing] } }).Sections[0].ChartDataUrl);
        Assert.NotNull(PdfPrintPresentation.Build(p with { Report = model with { ReportCode = "operating-finance", Series = [missing, missing with { Key = "2026-10-03" }] } }).Sections[0].ChartDataUrl);
        var practice = new ReportSeriesPoint(model.Scope.PracticeIds[0].ToString(), "عيادة نموذجية", "Synthetic practice", [PrintMetric("CheckIns", 7)]);
        var comparison = PdfPrintPresentation.Build(p with { Report = model with { ReportCode = "practice-comparison", Breakdowns = [cohort, practice, practice with { Key = Guid.NewGuid().ToString(), NameEn = "UNAUTHORIZED" }] } });
        Assert.Equal("Synthetic practice", Assert.Single(comparison.Sections[0].Points).Group);
        Assert.Null(PdfPrintPresentation.Build(p with { Report = model with { Scope = model.Scope with { Actor = ReportActor.Reception }, ReportCode = "activity", Series = flows } }).Sections[0].ChartDataUrl);
        var ids = Enumerable.Range(1, 4).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
        var practices = ids.Select((id, i) => practice with { Key = id.ToString(), NameEn = "Practice " + i,
            Metrics = [PrintMetric("CheckIns", i), PrintMetric("WaitToFirstCall", i + 5, "minutes"), PrintMetric("ReservationToTicketConversion", 50, "percent") with { Numerator = 1, Denominator = 2 }] }).ToArray();
        var html = await PdfTestSupport.Report().RenderHtmlAsync(p with { Report = model with { ReportCode = "practice-comparison", Scope = model.Scope with { PracticeIds = ids }, Breakdowns = practices } }, TestContext.Current.CancellationToken);
        Assert.Contains("Showing 2 of 4", html); Assert.DoesNotContain("Practice 2", html); Assert.DoesNotContain(ids[0].ToString(), html); Assert.Contains("1 / 2", html);
        if (Environment.GetEnvironmentVariable("WASLA_EXPORT_QA_DIRECTORY") is { } output)
        {
            Directory.CreateDirectory(output);
            var sample = model with { ReportCode = "operating-finance", TitleEn = "Operating financial statement", Window = model.Window with { FromDate = new(2026, 10, 1), ToDate = new(2026, 10, 2) },
                AppliedFilters = model.AppliedFilters with { FromDate = new(2026, 10, 1), ToDate = new(2026, 10, 2) }, Series = flows,
                Metrics = [PrintMetric("GrossCollected", 100, "EGP") with { NameEn = "Gross collected" }, PrintMetric("Refunds", 75, "EGP"), PrintMetric("NetCollected", 25, "EGP") with { NameEn = "Net collected" }, PrintMetric("PaidOperatingExpenses", 10, "EGP") with { NameEn = "Paid operating expenses" }, PrintMetric("NetOperatingCashResult", 15, "EGP") with { NameEn = "Net operating cash result" }] };
            await File.WriteAllTextAsync(Path.Combine(output, "executive-finance-en.html"), await PdfTestSupport.Report().RenderHtmlAsync(p with { Report = sample }, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        }
    }

    [Theory] [InlineData(ReportActor.Doctor)] [InlineData(ReportActor.SuperAdmin)]
    public async Task PdfPrint_Excluded_finance_and_suppressed_families_do_not_reappear(ReportActor actor)
    {
        var model = Model();
        var hidden = new ReportSeriesPoint("DoctorRegistrationCohort:secret", "PRIVATE-HIDDEN", "PRIVATE-HIDDEN", [PrintMetric("DoctorAcquisition", 987654) with { DataStatus = ReportDataStatus.NotAvailable }]);
        var complement = new ReportSeriesPoint("DoctorRegistrationCohort:other", "PRIVATE-COMPLEMENT", "PRIVATE-COMPLEMENT", [PrintMetric("DoctorAcquisition", 876543)]);
        var r = model with { Scope = model.Scope with { Actor = actor }, ReportCode = actor == ReportActor.Doctor ? "overview" : "adoption",
            Metrics = [PrintMetric("AppointmentsScheduled", 3), PrintMetric("PageViewAdoption", 765432) with { DataStatus = ReportDataStatus.NotInstrumented }],
            Records = [], Series = [], Breakdowns = actor == ReportActor.SuperAdmin ? [hidden, complement] : [], PreviousPeriodComparisons = [] };
        var p = new ReportPresentation(r, false, false, "QA", "Authorized synthetic scope");
        var print = PdfPrintPresentation.Build(p);
        var html = await PdfTestSupport.Report().RenderHtmlAsync(p, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("NetCollected", html); Assert.DoesNotContain("EGP", html); Assert.DoesNotContain("PRIVATE-", html);
        Assert.DoesNotContain("765,432", html); Assert.DoesNotContain("987,654", html); Assert.DoesNotContain("876,543", html);
        Assert.All(print.Sections, s => Assert.DoesNotContain(s.Points, x => x.Group.StartsWith("PRIVATE-", StringComparison.Ordinal)));
        if (actor == ReportActor.SuperAdmin) { Assert.Empty(PdfPrintPresentation.VisibleGroups(r, r.Breakdowns)); Assert.Contains(print.QualityNotes, n => n.Contains("privacy-suppressed")); }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PdfPrint_Html_is_bilingual_encoded_and_explains_the_executive_scope(bool arabic)
    {
        var model = Model(); var unsafeText = "<script>alert(1)</script> & synthetic";
        var metric = PrintMetric("BookingsCreated", 7) with { NameAr = unsafeText, NameEn = unsafeText, Numerator = 7, Denominator = 9, EligibleSampleCount = 9, ExcludedSampleCount = 2 };
        var r = model with { Metrics = [metric, PrintMetric("AppointmentsScheduled", 0)],
            Series = [new("2026-10-04", "2026-10-04", "2026-10-04", [metric])],
            Breakdowns = [new("BookingWeekday:Sunday", unsafeText, unsafeText, [metric])],
            Records = [model.Records[0] with { Reference = "PRIVATE-ROW-REFERENCE", Amount = null }], DetailRowCount = 1,
            AppliedFilters = model.AppliedFilters with { PracticeId = model.Scope.PracticeIds[0], ComparePreviousPeriod = true }, EligibleSampleCount = 9, ExcludedSampleCount = 2,
            PreviousPeriodComparisons = [new("BookingsCreated", model.Window.FromDate, model.Window.ToDate, 7, 6, 1, 16, ReportDataStatus.Partial),
                new("AppointmentsScheduled", model.Window.FromDate, model.Window.ToDate, 0, 1, -1, -100, ReportDataStatus.Complete),
                new("ExcludedComparison", model.Window.FromDate, model.Window.ToDate, 999, 1, 998, 99800, ReportDataStatus.Complete)],
            Warnings = ["Report.Unknown<script>", "Report.MixedPracticeTimeZones"] };
        var renderer = PdfTestSupport.Report(); var p = new ReportPresentation(r, arabic, false, unsafeText, unsafeText);
        var html = await renderer.RenderHtmlAsync(p, TestContext.Current.CancellationToken);
        Assert.Contains(arabic ? "lang=\"ar\" dir=\"rtl\"" : "lang=\"en\" dir=\"ltr\"", html);
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains(arabic ? "فترة التقرير" : "Report period", decoded); Assert.Contains(arabic ? "لقطة البيانات" : "As-of snapshot", decoded);
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("src=\"http", html);
        Assert.Contains("script-src 'none'", html); Assert.Contains("font-size:10.5pt", html); Assert.Contains("table-header-group", html);
        Assert.Contains("size:A4 portrait", html); Assert.Contains("grid-template-columns:repeat(2,minmax(0,1fr))", html);
        var landscape = await renderer.RenderHtmlAsync(p with { Landscape = true }, TestContext.Current.CancellationToken);
        Assert.Contains("size:A4 landscape", landscape);
        Assert.Contains(PdfPrintPresentation.ExecutiveLabel(arabic), decoded);
        Assert.Contains(arabic ? "يحذف هذا الملخص التنفيذي بيانات الصفوف عمدًا" : "intentionally omits row-level data", decoded);
        Assert.Contains(arabic ? "قد تختلف المناطق الزمنية للعيادات" : "Practice time zones may differ", decoded);
        Assert.Contains("7 / 9", html); Assert.Contains("9 / 2", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Count(html, "data-comparison=\"headline\""));
        Assert.DoesNotContain("ExcludedComparison", html); Assert.DoesNotContain("PRIVATE-ROW-REFERENCE", html);
        Assert.DoesNotContain(model.Scope.PracticeIds[0].ToString(), html); Assert.DoesNotContain("Report.Unknown", html);
        Assert.DoesNotContain("authorized-appendix", html); Assert.DoesNotContain("data-appendix-metric", html);
        Assert.True(html.IndexOf("data-card=", StringComparison.Ordinal) < html.IndexOf("id=\"data-quality\"", StringComparison.Ordinal));
    }

    [Theory] [InlineData(30)] [InlineData(366)]
    public async Task PdfPrint_Dense_sources_and_records_do_not_expand_the_executive_document(int days)
    {
        var model = Model(); var from = model.Window.ToDate.AddDays(1 - days);
        var metrics = new[] { PrintMetric("BookingsCreated", 30) with { NameAr = "الحجوزات المنشأة" }, PrintMetric("AppointmentsScheduled", 20) with { NameAr = "المواعيد المجدولة" }, PrintMetric("ReservationToTicketConversion", 50, "percent") with { NameAr = "تحويل الحجز إلى تذكرة", Numerator = 5, Denominator = 10 }, PrintMetric("CancellationRateScheduledCohort", 10, "percent") with { NameAr = "نسبة إلغاء المواعيد" }, PrintMetric("ReservationNoShowRate", 5, "percent") with { NameAr = "نسبة عدم الحضور" }, PrintMetric("UNSELECTED-METRIC", 1234) };
        var series = Enumerable.Range(0, days).Select(i => new ReportSeriesPoint(from.AddDays(i).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "", "", metrics)).ToArray();
        var categories = Enumerable.Range(0, 20).Select(i => new ReportSeriesPoint("CancellationReason:" + i, "سبب " + i, "Reason " + i, [PrintMetric("CancellationsByEvent", i)])).ToArray();
        var hours = Enumerable.Range(0, 24).Select(i => new ReportSeriesPoint("BookingHour:" + i, "HOUR-" + i, "HOUR-" + i, [PrintMetric("BookingsCreated", i)])).ToArray();
        var r = model with { Metrics = metrics, Series = series, Breakdowns = hours.Concat(categories).ToArray(), Warnings = [], PreviousPeriodComparisons = [],
            AppliedFilters = model.AppliedFilters with { FromDate = from }, Window = model.Window with { FromDate = from } };
        var p = new ReportPresentation(r, true, false, "QA", "عيادة تجريبية"); var renderer = PdfTestSupport.Report();
        var print = PdfPrintPresentation.Build(p); var html = await renderer.RenderHtmlAsync(p, TestContext.Current.CancellationToken);
        Assert.Equal(5, print.Cards.Count); Assert.Equal(3, print.Sections.Count); Assert.NotNull(print.Sections[0].ChartDataUrl);
        Assert.Contains("data-card=", html); Assert.Contains("data:image/svg+xml;base64,", System.Net.WebUtility.HtmlDecode(html)); Assert.Contains("grid-template-columns:repeat(3,minmax(0,1fr))", html);
        Assert.Contains("grid-template-columns:repeat(4,minmax(0,1fr))", await renderer.RenderHtmlAsync(p with { Landscape = true }, TestContext.Current.CancellationToken));
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Count(html, "data-summary-row=")); Assert.Equal(8, System.Text.RegularExpressions.Regex.Count(html, "<tr data-"));
        foreach (var excluded in new[] { "authorized-appendix", "data-appendix-metric", "data-record=", "All report metrics", "Source series", "Source breakdowns", "Authorized details", "UNSELECTED-METRIC", "REF-599", "HOUR-" }) Assert.DoesNotContain(excluded, html);
        var distribution = print.Sections[2]; Assert.Equal(20, distribution.Points.Count); Assert.Equal(8, PdfPrintPresentation.VisibleSummaryRows(distribution, 100).Count);
        Assert.Contains(PdfPrintPresentation.SummaryNote(8, 20, true), System.Net.WebUtility.HtmlDecode(html));
        var weekday = new ReportSeriesPoint("BookingWeekday:Sunday", "الأحد", "Sunday", [PrintMetric("BookingsCreated", 3)]);
        var preferred = PdfPrintPresentation.Build(p with { Report = r with { Breakdowns = r.Breakdowns.Append(weekday).ToArray() } }).Sections[2];
        Assert.Equal("Sunday", PdfPrintPresentation.Build(p with { IsArabic = false, Report = r with { Breakdowns = r.Breakdowns.Append(weekday).ToArray() } }).Sections[2].Points[0].Group);
        Assert.Single(preferred.Points);
        var unavailable = r with { Series = series.Select(g => g with { Metrics = g.Metrics.Select(m => m with { Value = null, DataStatus = ReportDataStatus.NotMeasured }).ToArray() }).ToArray() };
        var fallback = await renderer.RenderHtmlAsync(p with { Report = unavailable }, TestContext.Current.CancellationToken);
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Count(fallback, "data-summary-row=")); Assert.DoesNotContain("data:image/svg+xml;base64,", System.Net.WebUtility.HtmlDecode(fallback));
        Assert.Contains("لا تتوفر قياسات لهذا التحليل", System.Net.WebUtility.HtmlDecode(fallback));
        if (days == 30 && Environment.GetEnvironmentVariable("WASLA_EXPORT_QA_DIRECTORY") is { } output)
        { Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, "executive-reservations-ar.html"), html, TestContext.Current.CancellationToken); }
    }

    [Theory]
    [InlineData(ReportGrouping.Day, false)] [InlineData(ReportGrouping.Day, true)]
    [InlineData(ReportGrouping.Week, false)] [InlineData(ReportGrouping.Week, true)]
    [InlineData(ReportGrouping.Month, false)] [InlineData(ReportGrouping.Month, true)]
    public void PdfPrint_Partial_trends_keep_dated_positions_and_break_at_missing_observations(ReportGrouping grouping, bool omitGroup)
    {
        var model = Model();
        var series = Enumerable.Range(1, 5).Where(i => !omitGroup || i != 3).Select(i =>
        {
            var key = grouping == ReportGrouping.Day ? $"2026-01-0{i}" : grouping == ReportGrouping.Week ? $"2026-W0{i}" : $"2026-0{i}";
            return new ReportSeriesPoint(key, key, key, [PrintMetric("NetCollected", i == 3 ? null : i * -10, "EGP") with { DataStatus = i == 3 ? ReportDataStatus.NotMeasured : ReportDataStatus.Complete, NameEn = "Cash <script> & amount" }, PrintMetric("PaidOperatingExpenses", i, "EGP")]);
        }).ToArray();
        var p = new ReportPresentation(model with { ReportCode = "operating-finance", AppliedFilters = model.AppliedFilters with { Grouping = grouping }, Series = series, Breakdowns = [] }, false, false, "QA", "QA");
        var section = PdfPrintPresentation.Build(p).Sections[0]; Assert.False(section.Table);
        var svg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Assert.IsType<string>(section.ChartDataUrl).Split(',')[1]));
        var xml = System.Xml.Linq.XDocument.Parse(svg); System.Xml.Linq.XNamespace ns = "http://www.w3.org/2000/svg";
        var net = xml.Descendants(ns + "g").Single(g => (string?)g.Attribute("data-series") == "NetCollected");
        var paths = net.Elements(ns + "polyline").Select(e => (string)e.Attribute("points")!).ToArray();
        Assert.Equal(2, paths.Length); Assert.All(paths, path => Assert.Equal(2, path.Split(' ').Length));
        Assert.StartsWith("65,", paths[0]); Assert.Contains("231.25,", paths[0]); Assert.StartsWith("563.75,", paths[1]); Assert.Contains("730,", paths[1]);
        Assert.DoesNotContain("397.5,", string.Join(" ", paths)); Assert.Equal(4, net.Elements(ns + "circle").Count());
        Assert.Contains("-50", svg); Assert.DoesNotContain("<script>", svg); Assert.Contains("&lt;script&gt;", svg);
        Assert.Contains(grouping.ToString(), section.Caption); Assert.Contains("break across missing periods", section.Caption);
    }

    private static ReportMetric PrintMetric(string code, decimal? value, string unit = "count")
        => new(code, "مقياس " + code, code, value, unit, null, null, 1, 0, ReportDataStatus.Complete);

    internal static ReportModel Model()
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
