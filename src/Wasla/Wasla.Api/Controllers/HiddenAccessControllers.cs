using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(500)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/tickets/{ticketId:guid}/medical-record/access-requests")]
public sealed class SentMedicalRecordAccessRequestsController(ISender sender) : ControllerBase
{
    /// <summary>P16-12: Current ticket's sent access requests.</summary>
    [HttpGet, ProducesResponseType<RecordPage<HiddenAccessResponse>>(200)]
    public async Task<IActionResult> List(Guid practiceId, Guid ticketId, [FromQuery] HiddenAccessRequestStatus? status = null, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.Sent, practiceId, ticketId, Status: status, Cursor: cursor, PageSize: pageSize), ct)).ToIActionResult(ct);
    /// <summary>P16-13: Poll the effective status of a request for this ticket.</summary>
    [HttpGet("{requestId:guid}"), ProducesResponseType<HiddenAccessResponse>(200)]
    public async Task<IActionResult> Details(Guid practiceId, Guid ticketId, Guid requestId, CancellationToken ct)
        => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.SentDetail, practiceId, ticketId, requestId), ct)).ToIActionResult(ct);
    /// <summary>P16-14: Request access with a protected placeholder token and clinical reason.</summary>
    [HttpPost, EnableRateLimiting("medical-record-request"), ProducesResponseType<HiddenAccessResponse>(201)]
    public async Task<IActionResult> Create(Guid practiceId, Guid ticketId, HiddenAccessCreateRequest body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        var result = await sender.Send(new RequestHiddenEncounterAccessCommand(practiceId, ticketId, body.HiddenEncounterToken, body.Reason, key ?? string.Empty), ct);
        return result.IsFailure ? result.Errors.ToActionProblem(ct) : Created($"{Request.Path}/{result.Value.RequestId}", result.Value);
    }
}
[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(500)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v{version:apiVersion}/doctors/me/medical-record-access-requests")]
public sealed class IncomingMedicalRecordAccessRequestsController(ISender sender) : ControllerBase
{
    /// <summary>P16-15: Global doctor inbox badge, across owned practices.</summary>
    [HttpGet("inbox/summary"), ProducesResponseType<AccessInboxSummary>(200)]
    public async Task<IActionResult> Summary(CancellationToken ct) => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.InboxSummary), ct)).ToIActionResult(ct);
    /// <summary>P16-16: Global owner inbox with filters and protected cursor.</summary>
    [HttpGet("inbox"), ProducesResponseType<RecordPage<HiddenAccessResponse>>(200)]
    public async Task<IActionResult> Inbox([FromQuery] HiddenAccessRequestStatus? status = null, [FromQuery] Guid? practiceId = null, [FromQuery] string? search = null, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.Inbox, practiceId, Status: status, Search: search, Cursor: cursor, PageSize: pageSize), ct)).ToIActionResult(ct);
    /// <summary>P16-17: Owner review details.</summary>
    [HttpGet("{requestId:guid}"), ProducesResponseType<HiddenAccessResponse>(200)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken ct) => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.OwnerDetail, RequestId: requestId), ct)).ToIActionResult(ct);
    /// <summary>P16-18: Owner approval for a live treating context.</summary>
    [HttpPost("{requestId:guid}/approve"), ProducesResponseType<HiddenAccessResponse>(200)]
    public async Task<IActionResult> Approve(Guid requestId, AccessDecisionRequest body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DecideHiddenEncounterAccessCommand(requestId, HiddenAccessDecision.Approve, body.RowVersion, key), ct)).ToIActionResult(ct);
    /// <summary>P16-19: Owner rejection; terminal for this ticket.</summary>
    [HttpPost("{requestId:guid}/reject"), ProducesResponseType<HiddenAccessResponse>(200)]
    public async Task<IActionResult> Reject(Guid requestId, AccessDecisionRequest body, CancellationToken ct)
        => (await sender.Send(new DecideHiddenEncounterAccessCommand(requestId, HiddenAccessDecision.Reject, body.RowVersion), ct)).ToIActionResult(ct);
    /// <summary>P16-20: Owner revocation of approved access.</summary>
    [HttpPost("{requestId:guid}/revoke"), ProducesResponseType<HiddenAccessResponse>(200)]
    public async Task<IActionResult> Revoke(Guid requestId, AccessDecisionRequest body, CancellationToken ct)
        => (await sender.Send(new DecideHiddenEncounterAccessCommand(requestId, HiddenAccessDecision.Revoke, body.RowVersion), ct)).ToIActionResult(ct);
    /// <summary>P16-21: Immutable owner-only request history.</summary>
    [HttpGet("{requestId:guid}/history"), ProducesResponseType<IReadOnlyList<RequestHistoryResponse>>(200)]
    public async Task<IActionResult> History(Guid requestId, CancellationToken ct) => (await sender.Send(new HiddenAccessQuery(HiddenAccessRead.History, RequestId: requestId), ct)).ToIActionResult(ct);
}
public sealed record HiddenAccessCreateRequest(string HiddenEncounterToken, string Reason);
public sealed record AccessDecisionRequest(string RowVersion);
