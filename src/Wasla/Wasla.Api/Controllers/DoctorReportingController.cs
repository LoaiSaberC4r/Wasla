using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize(Roles = SystemRoleNames.Doctor)]
public sealed class DoctorReportingController(IReportingQueryService reports) : ControllerBase
{
    [HttpGet("dashboard/overview")]
    [Permission(Phase18PermissionNames.DoctorDashboard)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "overview", filter, ct)).ToIActionResult(ct);
    [HttpGet("dashboard/filter-options")]
    [ProducesResponseType<ReportFilterOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterOptions([FromQuery] ReportOptionsFilter filter, CancellationToken ct)
        => (await reports.FilterOptionsAsync(ReportActor.Doctor, ct, filter)).ToIActionResult(ct);
    [HttpGet("reports/catalog")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<IReadOnlyList<ReportCatalogEntry>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Catalog(CancellationToken ct)
        => (await reports.CatalogAsync(ReportActor.Doctor, ct)).ToIActionResult(ct);
    [HttpGet("reports/reservations")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reservations([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "reservations", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/queue")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "queue", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/patients")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Patients([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "patients", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/practice-comparison")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PracticeComparison([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "practice-comparison", filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/{reportCode}/drilldown")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportPage<ReportRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Drilldown(string reportCode, [FromQuery] string metricCode, [FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.DrilldownAsync(ReportActor.Doctor, reportCode, metricCode, filter, ct)).ToIActionResult(ct);
    [HttpGet("reports/{reportCode}")]
    [Permission(Phase18PermissionNames.DoctorReports)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(string reportCode, [FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, reportCode, filter, ct)).ToIActionResult(ct);
    [HttpGet("expenses/dashboard")]
    [Permission(Phase18PermissionNames.ExpensesView)]
    [ProducesResponseType<ReportModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Expenses([FromQuery] ReportFilter filter, CancellationToken ct)
        => (await reports.GetAsync(ReportActor.Doctor, "expenses", filter, ct)).ToIActionResult(ct);
}
