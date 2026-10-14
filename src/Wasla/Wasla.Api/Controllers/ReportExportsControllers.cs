using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Doctor)]
[Route("api/v{version:apiVersion}/doctors/me/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DoctorReportExportsController(IReportExportService exports) : ControllerBase
{
    [HttpGet("{reportCode}/export"), EnableRateLimiting("reports-export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export(string reportCode, [FromQuery] ReportFilter filter, [FromQuery] string format = "pdf",
        [FromQuery] string language = "ar", [FromQuery] bool landscape = false, CancellationToken ct = default)
    {
        var result = await exports.ExportAsync(ReportActor.Doctor, reportCode, filter, format, language, landscape, ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Reception)]
[Route("api/v{version:apiVersion}/reception/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceptionReportExportsController(IReportExportService exports) : ControllerBase
{
    [HttpGet("{reportCode}/export"), EnableRateLimiting("reports-export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export(string reportCode, [FromQuery] ReportFilter filter, [FromQuery] string format = "pdf",
        [FromQuery] string language = "ar", [FromQuery] bool landscape = false, CancellationToken ct = default)
    {
        var result = await exports.ExportAsync(ReportActor.Reception, reportCode, filter, format, language, landscape, ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.SuperAdmin)]
[Route("api/v{version:apiVersion}/admin/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SuperAdminReportExportsController(IReportExportService exports) : ControllerBase
{
    [HttpGet("{reportCode}/export"), EnableRateLimiting("reports-export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export(string reportCode, [FromQuery] ReportFilter filter, [FromQuery] string format = "pdf",
        [FromQuery] string language = "ar", [FromQuery] bool landscape = false, CancellationToken ct = default)
    {
        var result = await exports.ExportAsync(ReportActor.SuperAdmin, reportCode, filter, format, language, landscape, ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.SuperAdmin)]
[Route("api/v{version:apiVersion}/admin/operations/exports/health")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExportRuntimeHealthController(IReportDocumentRenderer renderer, IReportingAccessGuard guard) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ExportRuntimeHealth>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var access = await guard.AuthorizeAsync(ReportActor.SuperAdmin, Phase18PermissionNames.JobsView, null, null, ct);
        return access.IsFailure ? access.Errors.ToActionProblem(ct) : Ok(renderer.Health);
    }
}
