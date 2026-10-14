using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me/insights")]
[Authorize(Roles = SystemRoleNames.Doctor)]
public sealed class DoctorInsightsController(IDoctorInsightService insights, ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(Phase18PermissionNames.InsightsView)]
    [ProducesResponseType<ReportPage<DoctorInsightResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? practiceId, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, [FromQuery] DoctorInsightState? state = null, CancellationToken ct = default)
        => (await insights.ListAsync(practiceId, pageNumber, pageSize, ct, state)).ToIActionResult(ct);
    [HttpGet("{insightId:guid}")]
    [Permission(Phase18PermissionNames.InsightsView)]
    [ProducesResponseType<DoctorInsightResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid insightId, CancellationToken ct)
        => (await insights.DetailAsync(insightId, ct)).ToIActionResult(ct);
    [HttpGet("{insightId:guid}/history")]
    [Permission(Phase18PermissionNames.InsightsView)]
    [ProducesResponseType<ReportPage<DoctorInsightHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid insightId, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await insights.HistoryAsync(insightId, pageNumber, pageSize, ct)).ToIActionResult(ct);
    [HttpPost("{insightId:guid}/acknowledge")]
    [Permission(Phase18PermissionNames.InsightsTrack)]
    [ProducesResponseType<DoctorInsightResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(Guid insightId, [FromBody] InsightTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new TrackDoctorInsightCommand(insightId, DoctorInsightState.Acknowledged, key, request), ct)).ToIActionResult(ct);
    [HttpPost("{insightId:guid}/resolve")]
    [Permission(Phase18PermissionNames.InsightsTrack)]
    [ProducesResponseType<DoctorInsightResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(Guid insightId, [FromBody] InsightTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new TrackDoctorInsightCommand(insightId, DoctorInsightState.Resolved, key, request), ct)).ToIActionResult(ct);
    [HttpPost("{insightId:guid}/dismiss")]
    [Permission(Phase18PermissionNames.InsightsTrack)]
    [ProducesResponseType<DoctorInsightResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dismiss(Guid insightId, [FromBody] InsightTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new TrackDoctorInsightCommand(insightId, DoctorInsightState.Dismissed, key, request), ct)).ToIActionResult(ct);
}
