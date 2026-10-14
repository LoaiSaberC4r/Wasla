using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reception")]
[Authorize(Roles = SystemRoleNames.Reception)]
public sealed class ReceptionReportingController(IReportingQueryService reports, IReceptionOperationsService operations) : ControllerBase
{
    [HttpGet("dashboard/overview")]
    [Permission(Phase18PermissionNames.ReceptionDashboard)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Reception, "overview", filter, ct)).ToIActionResult(ct);
    [HttpGet("dashboard/filter-options")]
    [ProducesResponseType<ReportFilterOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterOptions([FromQuery] ReportOptionsFilter filter, CancellationToken ct)
        => (await reports.FilterOptionsAsync(ReportActor.Reception, ct, filter)).ToIActionResult(ct);
    [HttpGet("reports/catalog")]
    [Permission(Phase18PermissionNames.ReceptionReports)]
    [ProducesResponseType<IReadOnlyList<ReportCatalogEntry>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Catalog(CancellationToken ct)
        => (await reports.CatalogAsync(ReportActor.Reception, ct)).ToIActionResult(ct);
    [HttpGet("reports/reservations")]
    [Permission(Phase18PermissionNames.ReceptionReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reservations([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Reception, "reservations", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/queue")]
    [Permission(Phase18PermissionNames.ReceptionReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Reception, "queue", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/activity")]
    [Permission(Phase18PermissionNames.ReceptionReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Activity([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Reception, "activity", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/{reportCode}/drilldown")]
    [Permission(Phase18PermissionNames.ReceptionReports)]
    [ProducesResponseType<ReportPage<ReportRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Drilldown(string reportCode, [FromQuery] string metricCode, [FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.DrilldownAsync(ReportActor.Reception, reportCode, metricCode, filter, ct)).ToIActionResult(ct);
    [HttpGet("attention")]
    [Permission(Phase18PermissionNames.ReceptionDashboard)]
    [ProducesResponseType<ReceptionAttentionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Attention([FromQuery] Guid? practiceId, CancellationToken ct)
        => (await operations.AttentionAsync(practiceId, ct)).ToIActionResult(ct);
    [HttpGet("practices/{practiceId:guid}/worklist")]
    [Permission(Phase18PermissionNames.ReceptionDashboard)]
    [ProducesResponseType<ReportPage<WorklistItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Worklist(Guid practiceId, [FromQuery] WorklistFilter filter, CancellationToken ct)
        => (await operations.WorklistAsync(practiceId, filter, ct)).ToIActionResult(ct);
}
