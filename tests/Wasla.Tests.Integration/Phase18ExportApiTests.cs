using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18ExportApiTests
{
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
        using var pdf = await doctor.GetAsync("/api/v1/doctors/me/reports/queue/export?format=pdf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pdf.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var audits = await db.Set<ReportExportAudit>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Contains(audits, a => a.Outcome == "Completed" && a.Format == "xlsx");
            Assert.Contains(audits, a => a.Outcome == "Failed" && a.FailureCode == "Report.ChromiumNotConfigured");
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
}
