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
[Route("api/v{version:apiVersion}/families/mine/dependents/{dependentPatientId:guid}/medical-record")]
public sealed class DependentMedicalRecordController(ISender sender) : ControllerBase
{
    /// <summary>P16-25: Current safe record overview and first timeline page.</summary>
    [HttpGet("overview"), ProducesResponseType<PatientSafeMedicalRecordOverviewResponse>(200)]
    public async Task<IActionResult> Overview(Guid dependentPatientId, [FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Overview, MedicalRecordMode.Dependent, DependentPatientId: dependentPatientId, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-26: Safe current clinical outputs with protected keyset cursor.</summary>
    [HttpGet("timeline"), ProducesResponseType<RecordPage<TimelineEntry>>(200)]
    public async Task<IActionResult> Timeline(Guid dependentPatientId, [FromQuery] MedicalRecordFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Timeline, MedicalRecordMode.Dependent, DependentPatientId: dependentPatientId, Filter: filter), ct)).ToIActionResult(ct);
    /// <summary>P16-27: Authorized completed encounter with safe diagnoses and current output links.</summary>
    [HttpGet("encounters/{encounterId:guid}"), ProducesResponseType<PatientSafeMedicalRecordEncounterResponse>(200)]
    public async Task<IActionResult> Encounter(Guid dependentPatientId, Guid encounterId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Encounter, MedicalRecordMode.Dependent, DependentPatientId: dependentPatientId, EncounterId: encounterId), ct)).ToIActionResult(ct);
    /// <summary>P16-28: Current typed prescription or diagnostic item.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}"), ProducesResponseType<PatientSafeMedicalRecordItemResponse>(200)]
    public async Task<IActionResult> Item(Guid dependentPatientId, string resourceType, Guid sourceId, CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Item, MedicalRecordMode.Dependent, DependentPatientId: dependentPatientId, ResourceType: resourceType, SourceId: sourceId), ct)).ToIActionResult(ct);
    /// <summary>P16-29: Private current official attachment, reauthorized on every download.</summary>
    [HttpGet("items/{resourceType}/{sourceId:guid}/attachments/{attachmentId:guid}/content")]
    [EnableRateLimiting("medical-record-media")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public async Task<IActionResult> Content(Guid dependentPatientId, string resourceType, Guid sourceId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new ReadMedicalRecordMediaQuery(MedicalRecordMode.Dependent, resourceType, sourceId, attachmentId, DependentPatientId: dependentPatientId), ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        Response.Headers.CacheControl = "no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: false);
    }
}

