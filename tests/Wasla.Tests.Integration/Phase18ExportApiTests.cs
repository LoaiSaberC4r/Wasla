using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PuppeteerSharp;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18ExportApiTests
{
    [Theory] [InlineData("pdf")] [InlineData("xlsx")]
    public async Task Oversized_exports_keep_422_and_rejected_audit_with_original_byte_count(string format)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var doctor = await app.ClientAsync("doctor");
        await using var host = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportDocumentRenderer>(); services.AddSingleton<IReportDocumentRenderer, OversizedRenderer>();
        }));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync("/api/v1/doctors/me/reports/queue/export?format=" + format, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var audit = await db.Set<ReportExportAudit>().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Rejected", audit.Outcome); Assert.Equal("Report.ExportTooLarge", audit.FailureCode);
            Assert.Equal((format == "pdf" ? new ExportLimits().PdfMaximumBytes : new ExportLimits().ExcelMaximumBytes) + 1, audit.ByteCount);
        });
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Exports_recheck_scope_columns_and_export_permission_and_preserve_audit_metadata(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var doctor = await app.ClientAsync("doctor");
        var preview = await GetAsync(doctor, "/api/v1/doctors/me/dashboard/overview");
        Assert.Contains(preview.GetProperty("metrics").EnumerateArray(), m => m.GetProperty("metricCode").GetString() == "NetCollected");
        await app.WithDbAsync(async db =>
        {
            await db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Doctor && p.PermissionId == SystemPermissionIds.For(Phase18PermissionNames.DoctorExportFinancial))
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });
        using var export = await doctor.GetAsync("/api/v1/doctors/me/reports/overview/export?format=xlsx&language=ar", TestContext.Current.CancellationToken);
        Assert.True(export.StatusCode == HttpStatusCode.OK, await export.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType!.MediaType);
        Assert.Contains("wasla-doctor-overview", export.Content.Headers.ContentDisposition!.FileNameStar);
        using (var book = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))))
        {
            var codes = book.Worksheet("01 Summary").Column(9).CellsUsed().Select(c => c.GetString()).ToArray();
            Assert.DoesNotContain("NetCollected", codes); Assert.DoesNotContain("PaidOperatingExpenses", codes);
        }
        using var deniedFinancial = await doctor.GetAsync("/api/v1/doctors/me/reports/operating-finance/export?format=xlsx", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedFinancial.StatusCode);
        await using var failedBrowserHost = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChromiumBrowserRuntime>(); services.AddSingleton<IChromiumBrowserRuntime, FailedBrowser>();
        }));
        using var pdfClient = failedBrowserHost.CreateClient(); pdfClient.DefaultRequestHeaders.Authorization = doctor.DefaultRequestHeaders.Authorization;
        using var pdf = await pdfClient.GetAsync("/api/v1/doctors/me/reports/queue/export?format=pdf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pdf.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var audits = await db.Set<ReportExportAudit>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Contains(audits, a => a.Outcome == "Completed" && a.Format == "xlsx");
            Assert.Contains(audits, a => a.Outcome == "Failed" && a.FailureCode == "Report.BrowserDownloadFailed");
            Assert.DoesNotContain(audits, a => a.AppliedFiltersJson.Contains("PatientId", StringComparison.Ordinal));
        });
        using var reception = await app.ClientAsync("reception");
        await app.WithDbAsync(async db =>
        {
            await db.ReceptionPracticeAssignmentPermissions.Where(p => p.AssignmentId == app.ReceptionAssignmentId &&
                p.PermissionId == SystemPermissionIds.For(Phase18PermissionNames.ReceptionExport)).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });
        using var deniedScope = await reception.GetAsync("/api/v1/reception/reports/queue/export?format=xlsx", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedScope.StatusCode);
        using var unsupported = await doctor.GetAsync("/api/v1/doctors/me/reports/queue/export?format=csv", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        using var tooWide = await doctor.GetAsync("/api/v1/doctors/me/reports/queue/export?format=xlsx&fromDate=2025-01-01&toDate=2026-01-02", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooWide.StatusCode);
    }
    private sealed class FailedBrowser : IChromiumBrowserRuntime
    {
        public bool BrowserDownloaded => false;
        public Task EnsureBrowserDownloadedAsync(CancellationToken ct) => throw new IOException("Synthetic download failure");
        public Task<IBrowser> LaunchAsync(LaunchOptions options) => throw new InvalidOperationException("Download must succeed first");
    }
    private sealed class OversizedRenderer : IReportDocumentRenderer
    {
        public ExportRuntimeHealth Health => throw new NotSupportedException();
        public Task ProbeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<string> RenderHtmlAsync(ReportPresentation p, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<byte[]>> RenderPdfAsync(ReportPresentation p, CancellationToken ct)
            => Task.FromResult(Result<byte[]>.Ok(new byte[new ExportLimits().PdfMaximumBytes + 1]));
        public byte[] RenderExcel(ReportPresentation p) => new byte[new ExportLimits().ExcelMaximumBytes + 1];
    }
}
