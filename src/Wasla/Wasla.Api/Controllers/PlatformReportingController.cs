using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = SystemRoleNames.SuperAdmin)]
public sealed class PlatformReportingController(IReportingQueryService reports, IPlatformOperationsService operations) : ControllerBase
{
    [HttpGet("dashboard/overview")]
    [Permission(Phase18PermissionNames.PlatformDashboard)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "overview", filter, ct)).ToIActionResult(ct);
    [HttpGet("dashboard/filter-options")]
    [ProducesResponseType<ReportFilterOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterOptions([FromQuery] ReportOptionsFilter filter, CancellationToken ct)
        => (await reports.FilterOptionsAsync(ReportActor.SuperAdmin, ct, filter)).ToIActionResult(ct);
    [HttpGet("reports/catalog")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<IReadOnlyList<ReportCatalogEntry>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Catalog(CancellationToken ct)
        => (await reports.CatalogAsync(ReportActor.SuperAdmin, ct)).ToIActionResult(ct);
    [HttpGet("reports/doctor-lifecycle")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DoctorLifecycle([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "doctor-lifecycle", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/adoption")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Adoption([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "adoption", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/operations")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Operations([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "operations", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/geography")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Geography([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "geography", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/security-metadata")]
    [Permission(Phase18PermissionNames.SecurityMetadataView)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SecurityMetadata([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.SuperAdmin, "security-metadata", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/{reportCode}/drilldown")]
    [Permission(Phase18PermissionNames.PlatformReports)]
    [ProducesResponseType<ReportPage<ReportRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Drilldown(string reportCode, [FromQuery] string metricCode, [FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.DrilldownAsync(ReportActor.SuperAdmin, reportCode, metricCode, filter, ct)).ToIActionResult(ct);
    [HttpGet("data-quality/summary")]
    [Permission(Phase18PermissionNames.DataQualityView)]
    [ProducesResponseType<PlatformDataQualityResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DataQuality(CancellationToken ct)
        => (await operations.DataQualityAsync(ct)).ToIActionResult(ct);
    [HttpGet("operations/jobs/health")]
    [Permission(Phase18PermissionNames.JobsView)]
    [ProducesResponseType<PlatformJobHealthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Jobs(CancellationToken ct)
        => (await operations.JobsAsync(ct)).ToIActionResult(ct);
    [HttpGet("attention")]
    [Permission(Phase18PermissionNames.PlatformDashboard)]
    [ProducesResponseType<PlatformAttentionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Attention([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await operations.AttentionAsync(pageNumber, pageSize, ct)).ToIActionResult(ct);
}
