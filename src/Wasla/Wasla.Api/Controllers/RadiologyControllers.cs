
using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Diagnostics;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

public sealed record RadiologyMissingTestRequest(string ProcedureName, string? Specimen = null, string? CatalogClarificationNote = null)
{
    public DiagnosticCatalogRequestData Data() => new(ProcedureName, Specimen, CatalogClarificationNote);
}
public sealed record RadiologyCatalogRequestUpdateRequest(RadiologyMissingTestRequest Data, string RowVersion);
public sealed record RadiologyOrderItemRequest(Guid? RadiologyProcedureCatalogId = null, RadiologyMissingTestRequest? NewRadiologyProcedure = null, string? DoctorInstructions = null)
{
    public DiagnosticOrderItemInput Input() => new(RadiologyProcedureCatalogId, NewRadiologyProcedure?.Data(), DoctorInstructions);
}
public sealed record RadiologyAddItemRequest(Guid? RadiologyProcedureCatalogId = null, RadiologyMissingTestRequest? NewRadiologyProcedure = null, string? DoctorInstructions = null,
    string? RadiologyRequestRowVersion = null, string? PatientInstructions = null);
public sealed record RadiologyPostVisitRequest(string PostVisitReason, IReadOnlyList<RadiologyOrderItemRequest> Items, string? PatientInstructions = null);
public sealed record RadiologyAcceptSubmissionRequest(IReadOnlyList<Guid> CoveredRadiologyRequestItemIds, string RowVersion);
public sealed class RadiologyResultUploadRequest
{
    public List<Guid> CoveredRadiologyRequestItemIds { get; set; } = [];
    public List<IFormFile> Attachments { get; set; } = [];
    public List<DiagnosticAttachmentKind> AttachmentKinds { get; set; } = [];
    public string? ExternalRadiologyCenterName { get; set; }
    public DateOnly? ExternalReportDate { get; set; }
    public string? PatientNote { get; set; }
    public string? RowVersion { get; set; }
    public string? Reason { get; set; }
}
public sealed class PatientRadiologySubmissionRequest
{
    public List<IFormFile> Attachments { get; set; } = [];
    public List<DiagnosticAttachmentKind> AttachmentKinds { get; set; } = [];
    public string? ExternalRadiologyCenterName { get; set; }
    public DateOnly? ExternalReportDate { get; set; }
    public string? PatientNote { get; set; }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.MedicalCatalogManager)]
[Route("api/v{version:apiVersion}/admin/radiology-catalog")]
[ProducesResponseType<MedicalCatalogResponse>(StatusCodes.Status200OK)]
public sealed class RadiologyCatalogController(ISender sender, IOptions<DiagnosticCatalogImportOptions> options) : ControllerBase
{
    [HttpGet, ProducesResponseType<ClinicalPage<MedicalCatalogResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] MedicalCatalogStatus? status = null, [FromQuery] MedicalCatalogSource? source = null,
        [FromQuery] bool? hasArabic = null, [FromQuery] bool commonOnly = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new SearchMedicalCatalogQuery(DiagnosticKind.Radiology, false, search, status, source, hasArabic, commonOnly, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new SearchMedicalCatalogQuery(DiagnosticKind.Radiology, false, Id: id), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost, ProducesResponseType<MedicalCatalogResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CatalogPresentation r, CancellationToken ct)
    {
        var result = await sender.Send(new MutateMedicalCatalogCommand(DiagnosticKind.Radiology, CatalogMutation.Create, Data: r), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, MedicalCatalogUpdateRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogCommand(DiagnosticKind.Radiology, CatalogMutation.Update, id, r.Data, r.RowVersion), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogCommand(DiagnosticKind.Radiology, CatalogMutation.Activate, id, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogCommand(DiagnosticKind.Radiology, CatalogMutation.Deactivate, id, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/merge")]
    public async Task<IActionResult> Merge(Guid id, DiagnosticActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogCommand(DiagnosticKind.Radiology, CatalogMutation.Merge, id, RowVersion: r.RowVersion, Reason: r.Reason, TargetId: r.TargetCatalogId, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("imports/preview"), Consumes("multipart/form-data"), RequestSizeLimit(220L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 210L * 1024 * 1024)]
    [ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview([FromForm] DiagnosticImportUploadRequest r, CancellationToken ct)
    {
        if (r.File is null) return new[] { DiagnosticErrors.Validation("DiagnosticCatalogImport.RequiredFileMissing") }.ToActionProblem(ct);
        if (r.File.Length > options.Value.MaxPackageBytes) return new[] { DiagnosticErrors.Validation("DiagnosticCatalogImport.PackageTooLarge") }.ToActionProblem(ct);
        using var buffer = new MemoryStream(); await r.File.CopyToAsync(buffer, ct);
        return (await sender.Send(new PreviewDiagnosticImportCommand(DiagnosticKind.Radiology, buffer.ToArray(), r.File.FileName, r.SourceVersion), ct)).ToIActionResult(ct);
    }
    [HttpGet("imports"), ProducesResponseType<ClinicalPage<DiagnosticImportBatchResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Imports([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ReadDiagnosticImportQuery(DiagnosticKind.Radiology, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("imports/{batchId:guid}"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(Guid batchId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticImportQuery(DiagnosticKind.Radiology, batchId), ct)).ToIActionResult(ct);
    [HttpGet("imports/{batchId:guid}/changes"), ProducesResponseType<ClinicalPage<DiagnosticImportRecordResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Changes(Guid batchId, [FromQuery] DiagnosticImportDisposition? disposition = null, [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ReadDiagnosticImportQuery(DiagnosticKind.Radiology, batchId, true, disposition, search, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpPost("imports/{batchId:guid}/apply"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply(Guid batchId, DiagnosticImportApplyRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutateDiagnosticImportCommand(DiagnosticKind.Radiology, batchId, true, r.RowVersion, key, r.SkipPossibleConflicts), ct)).ToIActionResult(ct);
    [HttpPost("imports/{batchId:guid}/discard"), ProducesResponseType<DiagnosticImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Discard(Guid batchId, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new MutateDiagnosticImportCommand(DiagnosticKind.Radiology, batchId, false, r.RowVersion), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.MedicalCatalogManager)]
[Route("api/v{version:apiVersion}/admin/radiology-catalog-requests")]
[ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]
public sealed class RadiologyCatalogRequestsController(ISender sender) : ControllerBase
{
    [HttpGet, ProducesResponseType<ClinicalPage<MedicalCatalogRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] MedicalCatalogRequestStatus? status = null, [FromQuery] Guid? doctorId = null, [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListMedicalCatalogRequestsQuery(DiagnosticKind.Radiology, false, Status: status, DoctorId: doctorId, Search: search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
    {
        var result = await sender.Send(new ListMedicalCatalogRequestsQuery(DiagnosticKind.Radiology, false, requestId), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{requestId:guid}/request-more-info")]
    public async Task<IActionResult> MoreInfo(Guid requestId, MedicalRequestReviewRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogRequestCommand(DiagnosticKind.Radiology, CatalogRequestMutation.MoreInfo, requestId, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid requestId, MedicalRequestReviewRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogRequestCommand(DiagnosticKind.Radiology, CatalogRequestMutation.Approve, requestId, RowVersion: r.RowVersion, Reason: r.Reason, CanonicalCatalogId: r.CanonicalCatalogId, ApprovedData: r.ApprovedData, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, MedicalRequestReviewRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogRequestCommand(DiagnosticKind.Radiology, CatalogRequestMutation.Reject, requestId, RowVersion: r.RowVersion, Reason: r.Reason, CanonicalCatalogId: r.CanonicalCatalogId), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Doctor)]
[Route("api/v{version:apiVersion}/doctors/me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DoctorRadiologyController(ISender sender) : ControllerBase
{
    [HttpGet("radiology-catalog"), ProducesResponseType<ClinicalPage<MedicalCatalogResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new SearchMedicalCatalogQuery(DiagnosticKind.Radiology, true, search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("radiology-catalog-requests"), ProducesResponseType<ClinicalPage<MedicalCatalogRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CatalogRequests([FromQuery] MedicalCatalogRequestStatus? status = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListMedicalCatalogRequestsQuery(DiagnosticKind.Radiology, true, Status: status, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("radiology-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CatalogRequest(Guid requestId, CancellationToken ct)
    {
        var result = await sender.Send(new ListMedicalCatalogRequestsQuery(DiagnosticKind.Radiology, true, requestId), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("radiology-catalog-requests"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitCatalogRequest(RadiologyMissingTestRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogRequestCommand(DiagnosticKind.Radiology, CatalogRequestMutation.Create, Data: r.Data()), ct)).ToIActionResult(ct);
    [HttpPut("radiology-catalog-requests/{requestId:guid}"), ProducesResponseType<MedicalCatalogRequestResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCatalogRequest(Guid requestId, RadiologyCatalogRequestUpdateRequest r, CancellationToken ct)
        => (await sender.Send(new MutateMedicalCatalogRequestCommand(DiagnosticKind.Radiology, CatalogRequestMutation.Update, requestId, r.Data.Data(), r.RowVersion), ct)).ToIActionResult(ct);
    [HttpGet("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Draft(Guid practiceId, Guid encounterId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Draft, PracticeId: practiceId, EncounterId: encounterId), ct)).ToIActionResult(ct);
    [HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Add(Guid practiceId, Guid encounterId, RadiologyAddItemRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.Add, practiceId, encounterId, RowVersion: r.RadiologyRequestRowVersion, Item: new(r.RadiologyProcedureCatalogId, r.NewRadiologyProcedure?.Data(), r.DoctorInstructions), PatientInstructions: r.PatientInstructions, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPut("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid practiceId, Guid encounterId, Guid itemId, RadiologyAddItemRequest r, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.Update, practiceId, encounterId, ItemId: itemId, RowVersion: r.RadiologyRequestRowVersion, Item: new(DoctorInstructions: r.DoctorInstructions)), ct)).ToIActionResult(ct);
    [HttpDelete("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-request/items/{itemId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Remove(Guid practiceId, Guid encounterId, Guid itemId, [FromQuery] string rowVersion, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.Remove, practiceId, encounterId, ItemId: itemId, RowVersion: rowVersion), ct)).ToIActionResult(ct);
    [HttpPost("practices/{practiceId:guid}/encounters/{encounterId:guid}/radiology-requests/post-visit"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostVisit(Guid practiceId, Guid encounterId, RadiologyPostVisitRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.PostVisit, practiceId, encounterId, Items: r.Items?.Select(i => i.Input()).ToArray(), PatientInstructions: r.PatientInstructions, Reason: r.PostVisitReason, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpGet("radiology-requests"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Requests([FromQuery] DiagnosticFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Requests, Filter: filter), ct)).ToIActionResult(ct);
    [HttpGet("radiology-requests/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestDetails(Guid requestId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Requests, Id: requestId), ct)).ToIActionResult(ct);
    [HttpGet("radiology-requests/{requestId:guid}/history"), ProducesResponseType<IReadOnlyList<DiagnosticHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid requestId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.RequestHistory, Id: requestId), ct)).ToIActionResult(ct);
    [HttpPost("radiology-requests/{requestId:guid}/items/{itemId:guid}/cancel"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid requestId, Guid itemId, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.Cancel, RequestId: requestId, ItemId: itemId, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("radiology-requests/{requestId:guid}/cancel-remaining"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelRemaining(Guid requestId, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new DiagnosticOrderCommand(DiagnosticKind.Radiology, DiagnosticOrderMutation.Cancel, RequestId: requestId, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("radiology-requests/{requestId:guid}/results"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]
    [ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(Guid requestId, [FromForm] RadiologyResultUploadRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Upload, requestId, RowVersion: r.RowVersion, CoveredItemIds: r.CoveredRadiologyRequestItemIds, Attachments: await DiagnosticForms.ReadAsync(r.Attachments, r.AttachmentKinds, ct, true), ExternalProviderName: r.ExternalRadiologyCenterName, ExternalReportDate: r.ExternalReportDate, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Results([FromQuery] DiagnosticFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Results, Filter: filter), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Result(Guid resultId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Results, Id: resultId), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/{resultId:guid}/versions"), ProducesResponseType<IReadOnlyList<DiagnosticVersionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Versions(Guid resultId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Versions, Id: resultId), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/{resultId:guid}/versions/{versionNumber:int}"), ProducesResponseType<DiagnosticVersionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Version(Guid resultId, int versionNumber, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Versions, Id: resultId, VersionNumber: versionNumber), ct)).ToIActionResult(ct);
    [HttpPost("radiology-results/{resultId:guid}/corrections"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]
    [ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Correct(Guid resultId, [FromForm] RadiologyResultUploadRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Correct, ResultId: resultId, RowVersion: r.RowVersion, CoveredItemIds: r.CoveredRadiologyRequestItemIds, Attachments: await DiagnosticForms.ReadAsync(r.Attachments, r.AttachmentKinds, ct, true), ExternalProviderName: r.ExternalRadiologyCenterName, ExternalReportDate: r.ExternalReportDate, Reason: r.Reason, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("radiology-results/{resultId:guid}/void"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Void(Guid resultId, DiagnosticActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Void, ResultId: resultId, RowVersion: r.RowVersion, Reason: r.Reason, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpGet("radiology-result-submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox([FromQuery] DiagnosticFilter filter, [FromQuery] PatientSubmissionStatus? status = null, CancellationToken ct = default)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Submissions, Filter: filter with { SubmissionStatus = status ?? filter.SubmissionStatus }), ct)).ToIActionResult(ct);
    [HttpGet("radiology-result-submissions/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submission(Guid submissionId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Submissions, Id: submissionId), ct)).ToIActionResult(ct);
    [HttpPost("radiology-result-submissions/{submissionId:guid}/accept"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid submissionId, RadiologyAcceptSubmissionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Accept, SubmissionId: submissionId, RowVersion: r.RowVersion, CoveredItemIds: r.CoveredRadiologyRequestItemIds, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("radiology-result-submissions/{submissionId:guid}/reject"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid submissionId, DiagnosticRejectSubmissionRequest r, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Reject, SubmissionId: submissionId, RowVersion: r.RowVersion, Reason: r.PatientVisibleReason), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/{resultId:guid}/versions/{versionNumber:int}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> ResultContent(Guid resultId, int versionNumber, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new DiagnosticMediaQuery(DiagnosticKind.Radiology, false, false, resultId, attachmentId, versionNumber), ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("radiology-result-submissions/{submissionId:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> SubmissionContent(Guid submissionId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new DiagnosticMediaQuery(DiagnosticKind.Radiology, false, true, submissionId, attachmentId), ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Patient)]
[Route("api/v{version:apiVersion}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientRadiologyController(ISender sender) : ControllerBase
{
    [HttpGet("radiology-requests/mine"), ProducesResponseType<ClinicalPage<DiagnosticRequestSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Requests([FromQuery] DiagnosticFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Requests, true, Filter: filter), ct)).ToIActionResult(ct);
    [HttpGet("radiology-requests/mine/{requestId:guid}"), ProducesResponseType<DiagnosticRequestStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestDetails(Guid requestId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Requests, true, requestId), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/mine"), ProducesResponseType<ClinicalPage<DiagnosticResultResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Results([FromQuery] DiagnosticFilter filter, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Results, true, Filter: filter), ct)).ToIActionResult(ct);
    [HttpGet("radiology-results/mine/{resultId:guid}"), ProducesResponseType<DiagnosticResultResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Result(Guid resultId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Results, true, resultId), ct)).ToIActionResult(ct);
    [HttpPost("radiology-requests/mine/{requestId:guid}/submissions"), Consumes("multipart/form-data"), RequestSizeLimit(110L * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 105L * 1024 * 1024)]
    [ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid requestId, [FromForm] PatientRadiologySubmissionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (Request.Form.Keys.Any(k => k.Contains("covered", StringComparison.OrdinalIgnoreCase))) return new[] { DiagnosticErrors.Validation("RadiologyResultSubmission.InvalidCoverage") }.ToActionProblem(ct);
        return (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Submit, requestId, Attachments: await DiagnosticForms.ReadAsync(r.Attachments, r.AttachmentKinds, ct, true), ExternalProviderName: r.ExternalRadiologyCenterName, ExternalReportDate: r.ExternalReportDate, PatientNote: r.PatientNote, IdempotencyKey: key), ct)).ToIActionResult(ct);
    }
    [HttpGet("radiology-requests/mine/{requestId:guid}/submissions"), ProducesResponseType<ClinicalPage<DiagnosticSubmissionSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submissions(Guid requestId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Submissions, true, Filter: new(RequestId: requestId, PageNumber: pageNumber, PageSize: pageSize)), ct)).ToIActionResult(ct);
    [HttpGet("radiology-result-submissions/mine/{submissionId:guid}"), ProducesResponseType<DiagnosticSubmissionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submission(Guid submissionId, CancellationToken ct)
        => (await sender.Send(new ReadDiagnosticQuery(DiagnosticKind.Radiology, DiagnosticReadResource.Submissions, true, submissionId), ct)).ToIActionResult(ct);
    [HttpPost("radiology-result-submissions/mine/{submissionId:guid}/withdraw"), ProducesResponseType<DiagnosticResultMutationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Withdraw(Guid submissionId, DiagnosticActionRequest r, CancellationToken ct)
        => (await sender.Send(new DiagnosticResultCommand(DiagnosticKind.Radiology, DiagnosticResultMutation.Withdraw, SubmissionId: submissionId, RowVersion: r.RowVersion), ct)).ToIActionResult(ct);
    [HttpGet("radiology-result-submissions/mine/{submissionId:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> SubmissionContent(Guid submissionId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new DiagnosticMediaQuery(DiagnosticKind.Radiology, true, true, submissionId, attachmentId), ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("radiology-results/mine/{resultId:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> ResultContent(Guid resultId, Guid attachmentId, CancellationToken ct)
    {
        var result = await sender.Send(new DiagnosticMediaQuery(DiagnosticKind.Radiology, true, false, resultId, attachmentId), ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
}

