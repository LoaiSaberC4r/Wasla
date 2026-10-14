using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Media;
using Wasla.Domain.Reporting;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

internal sealed class ReportExportService(WaslaDbContext db, IReportingQueryService reports, IReportDocumentRenderer renderer,
    IReportingAccessGuard guard, IPrivateMediaReader media, IDateTimeProvider clock) : IReportExportService
{
    public async Task<Result<ReportExportFile>> ExportAsync(ReportActor actor, string code, ReportFilter filter, string format,
        string language, bool landscape, CancellationToken ct)
    {
        if (language is not ("ar" or "en") || format is not ("pdf" or "xlsx")) return Result<ReportExportFile>.Fail(ReportingErrors.InvalidFilter);
        var queried = await reports.BuildExportAsync(actor, code, filter, format, ct);
        if (queried.IsFailure) return Result<ReportExportFile>.Fail(queried.Errors);
        var report = queried.Value;
        var scope = report.Scope;
        var names = actor != ReportActor.SuperAdmin && scope.PracticeIds.Count <= 20 ?
            await db.DoctorPractices.AsNoTracking().Where(p => scope.PracticeIds.Contains(p.Id)).OrderBy(p => p.Id)
                .Select(p => language == "ar" ? p.NameAr : p.NameEn ?? p.NameAr).ToArrayAsync(ct) : [];
        var by = actor switch
        {
            ReportActor.Doctor => await db.Doctors.AsNoTracking().Where(d => d.ApplicationUserId == scope.ActorId).Select(d => language == "ar" ? d.NameAr : d.NameEn ?? d.NameAr).SingleAsync(ct),
            ReportActor.Reception => await db.Receptions.AsNoTracking().Where(r => r.ApplicationUserId == scope.ActorId).Select(r => language == "ar" ? r.NameAr : r.NameEn ?? r.NameAr).SingleAsync(ct),
            _ => await db.SuperAdmins.AsNoTracking().Where(a => a.ApplicationUserId == scope.ActorId).Select(a => language == "ar" ? a.NameAr : a.NameEn ?? a.NameAr).SingleAsync(ct)
        };
        var logo = scope.PracticeIds.Count == 1 ? await LogoAsync(scope.PracticeIds[0], ct) : null;
        var scopeLabel = actor == ReportActor.SuperAdmin ? language == "ar" ? "تجميعات المنصة المصرح بها" : "Authorized platform aggregates"
            : scope.PracticeIds.Count > 20 ? $"{scope.PracticeIds.Count} {(language == "ar" ? "عيادة مصرح بها" : "authorized practices")}" : string.Join(" / ", names);
        var p = new ReportPresentation(report, language == "ar", landscape, by, scopeLabel, logo);
        byte[] bytes;
        if (format == "pdf")
        {
            var rendered = await renderer.RenderPdfAsync(p, ct);
            if (rendered.IsFailure) { await AuditAsync(report, format, language, "Failed", rendered.Errors.First().Code, null, ct); return Result<ReportExportFile>.Fail(rendered.Errors); }
            bytes = rendered.Value;
        }
        else bytes = renderer.RenderExcel(p);
        if (bytes.Length > (format == "pdf" ? new ExportLimits().PdfMaximumBytes : new ExportLimits().ExcelMaximumBytes))
        {
            await AuditAsync(report, format, language, "Rejected", "Report.ExportTooLarge", bytes.Length, ct);
            return Result<ReportExportFile>.Fail(ReportingQueryService.ExportTooLarge());
        }
        // Long-running renders recheck current authorization before releasing bytes.
        var definition = ReportDefinitionCatalog.Definitions.Single(d => d.Code == code);
        var current = await guard.AuthorizeAsync(actor, ReportDefinitionCatalog.ExportPermission(actor, definition.Financial), filter.PracticeId, filter.DoctorId, ct);
        var view = await guard.AuthorizeAsync(actor, code == "overview" ? ReportDefinitionCatalog.DashboardPermission(actor) : ReportDefinitionCatalog.ViewPermission(actor),
            filter.PracticeId, filter.DoctorId, ct);
        if (current.IsFailure || view.IsFailure || scope.PracticeIds.Any(id => !current.Value.PracticeIds.Contains(id) || !view.Value.PracticeIds.Contains(id)) ||
            !scope.Permissions.Order().SequenceEqual(view.Value.Permissions.Order()))
        {
            await AuditAsync(report, format, language, "DeniedBeforeRelease", "Report.AuthorizationChanged", null, ct);
            return Result<ReportExportFile>.Fail(ReportingErrors.Denied);
        }
        await AuditAsync(report, format, language, "Completed", null, bytes.Length, ct);
        return Result<ReportExportFile>.Ok(new($"wasla-{actor.ToString().ToLowerInvariant()}-{code}-{report.Window.FromDate:yyyyMMdd}-{report.Window.ToDate:yyyyMMdd}-{language}.{format}",
            format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes));
    }
    private async Task AuditAsync(ReportModel report, string format, string language, string outcome, string? failure, int? bytes, CancellationToken ct)
    {
        db.Set<ReportExportAudit>().Add(new(Guid.NewGuid(), report.Scope.ActorId, report.Scope.Actor.ToString(), report.ReportCode,
            format, language, JsonSerializer.Serialize(report.AppliedFilters), outcome, failure, bytes, clock.UtcNow,
            JsonSerializer.Serialize(new { report.Scope.DoctorId, report.Scope.PracticeIds, report.Scope.Permissions, report.Window, report.DataFreshnessUtc })));
        await db.SaveChangesAsync(ct);
    }
    private async Task<string?> LogoAsync(Guid practiceId, CancellationToken ct)
    {
        var key = await db.DoctorPracticeBrandings.AsNoTracking().Where(b => b.DoctorPracticeId == practiceId).Select(b => b.LogoMediaKey).SingleOrDefaultAsync(ct);
        if (key == null) return null;
        var logo = await media.OpenAsync(key, ct); if (logo == null) return null;
        await using var content = logo.Content;
        if (logo.ContentType is not ("image/png" or "image/jpeg")) return null;
        using var bounded = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await content.ReadAsync(buffer, ct)) > 0)
        { if (bounded.Length + read > 1024 * 1024) return null; bounded.Write(buffer, 0, read); }
        var bytes = bounded.ToArray();
        if (!ReportLogoPolicy.IsBoundedImage(bytes, logo.ContentType)) return null;
        return "data:" + logo.ContentType + ";base64," + Convert.ToBase64String(bytes);
    }
}
