using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Clinical;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(500)]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/tickets/{ticketId:guid}/medical-record")]
public sealed class TreatingMedicalRecordController(ISender sender) : ControllerBase
{
    /// <summary>P16-07: Current safe record overview and first timeline page.</summary>
    [HttpGet("overview"), ProducesResponseType<DoctorSharedMedicalRecordOverviewResponse>(200)]
    public async Task<IActionResult> Overview(Guid practiceId, Guid ticketId, [FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Overview, MedicalRecordMode.TreatingDoctor, PracticeId: practiceId, TicketId: ticketId, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-08: Safe current clinical outputs with protected keyset cursor.</summary>
    [HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]
    public async Task<IActionResult> Timeline(Guid practiceId, Guid ticketId, [FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Timeline, MedicalRecordMode.TreatingDoctor, PracticeId: practiceId, TicketId: ticketId, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-09: Authorized completed encounter with safe diagnoses and current output links.</summary>
    [HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<DoctorSharedMedicalRecordEncounterResponse>(200)]
    public async Task<IActionResult> Encounter(Guid practiceId, Guid ticketId, Guid encounterId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Encounter, MedicalRecordMode.TreatingDoctor, PracticeId: practiceId, TicketId: ticketId, EncounterId: encounterId), ct)).ToIActionResult(ct);
    /// <summary>P16-10: Current typed prescription or diagnostic item.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<DoctorSharedMedicalRecordItemResponse>(200)]
    public async Task<IActionResult> Item(Guid practiceId, Guid ticketId, string resourceType, Guid sourceId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Item, MedicalRecordMode.TreatingDoctor, PracticeId: practiceId, TicketId: ticketId, ResourceType: resourceType, SourceId: sourceId), ct)).ToIActionResult(ct);
    /// <summary>P16-11: Private current official attachment, reauthorized on every download.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]
    [EnableRateLimiting("medical-record-media")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> Content(Guid practiceId, Guid ticketId, string resourceType, Guid sourceId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new ReadMedicalRecordMediaQuery(MedicalRecordMode.TreatingDoctor, resourceType, sourceId, attachmentId, PracticeId: practiceId, TicketId: ticketId), ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers.CacheControl = "no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: false);
    }
}

