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
[Route("api/v{version:apiVersion}/patients/me/medical-record")]
public sealed class PatientMedicalRecordController(ISender sender) : ControllerBase
{
    /// <summary>P16-01: Current safe record overview and first timeline page.</summary>
    [HttpGet("overview"), ProducesResponseType<PatientSafeMedicalRecordOverviewResponse>(200)]
    public async Task<IActionResult> Overview([FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Overview, MedicalRecordMode.OwnPatient, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-02: Safe current clinical outputs with protected keyset cursor.</summary>
    [HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]
    public async Task<IActionResult> Timeline([FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Timeline, MedicalRecordMode.OwnPatient, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-03: Authorized completed encounter with safe diagnoses and current output links.</summary>
    [HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<PatientSafeMedicalRecordEncounterResponse>(200)]
    public async Task<IActionResult> Encounter(Guid encounterId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Encounter, MedicalRecordMode.OwnPatient, EncounterId: encounterId), ct)).ToIActionResult(ct);
    /// <summary>P16-04: Current typed prescription or diagnostic item.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<PatientSafeMedicalRecordItemResponse>(200)]
    public async Task<IActionResult> Item(string resourceType, Guid sourceId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Item, MedicalRecordMode.OwnPatient, ResourceType: resourceType, SourceId: sourceId), ct)).ToIActionResult(ct);
    /// <summary>P16-05: Private current official attachment, reauthorized on every download.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]
    [EnableRateLimiting("medical-record-media")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> Content(string resourceType, Guid sourceId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new ReadMedicalRecordMediaQuery(MedicalRecordMode.OwnPatient, resourceType, sourceId, attachmentId), ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers.CacheControl = "no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: false);
    }
    /// <summary>P16-06: Patient-safe metadata access history, without internal reasons or raw audit identifiers.</summary>
    [HttpGet("access-log"), ProducesResponseType<RecordPage<PatientSafeAccessLog>>(200)]
    public async Task<IActionResult> AccessLog([FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.AccessLog, MedicalRecordMode.OwnPatient, Filter: new(Cursor: cursor, PageSize: pageSize)), ct)).ToIActionResult(ct);
}

