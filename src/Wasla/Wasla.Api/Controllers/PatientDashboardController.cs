using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Api.Security;
using Wasla.Application.Features.Dashboards;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = SystemRoleNames.Patient)]
[Route("api/v{version:apiVersion}/patients/me/dashboard")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("medical-record")]
public sealed class PatientDashboardController(ISender sender) : ControllerBase
{
    [HttpGet("overview")]
    [Permission(Phase18PermissionNames.PatientDashboard)]
    [ProducesResponseType<PatientDashboardOverview>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken ct)
        => (await sender.Send(new GetPatientDashboardOverviewQuery(), ct)).ToIActionResult(ct);
    [HttpGet("capabilities")]
    [Permission(Phase18PermissionNames.PatientDashboard)]
    [ProducesResponseType<PatientDashboardCapabilities>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Capabilities(CancellationToken ct)
        => (await sender.Send(new GetPatientDashboardCapabilitiesQuery(), ct)).ToIActionResult(ct);
}
