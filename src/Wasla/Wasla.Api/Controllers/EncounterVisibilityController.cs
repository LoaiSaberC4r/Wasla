using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/encounters/{encounterId:guid}/visibility")]
public sealed class EncounterVisibilityController(ISender sender) : ControllerBase
{
    /// <summary>P16-22: Change sharing visibility of a completed owned encounter.</summary>
    [HttpPatch, ProducesResponseType<EncounterVisibilityResponse>(200)]
    public async Task<IActionResult> Change(Guid practiceId, Guid encounterId, ChangeVisibilityRequest body, CancellationToken ct)
        => (await sender.Send(new ChangeEncounterVisibilityCommand(practiceId, encounterId, body.Visibility, body.RowVersion), ct)).ToIActionResult(ct);
    /// <summary>P16-23: Read immutable owner-only visibility history.</summary>
    [HttpGet("history"), ProducesResponseType<RecordPage<VisibilityHistoryResponse>>(200)]
    public async Task<IActionResult> History(Guid practiceId, Guid encounterId, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new VisibilityHistoryQuery(practiceId, encounterId, cursor, pageSize), ct)).ToIActionResult(ct);
}
public sealed record ChangeVisibilityRequest(EncounterVisibility Visibility, string RowVersion);
