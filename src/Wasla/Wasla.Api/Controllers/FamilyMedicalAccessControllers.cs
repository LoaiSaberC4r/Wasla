using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[Route("api/v{version:apiVersion}/families/mine/medical-access-requests")]
public sealed class FamilyMedicalAccessRequestsController(ISender sender) : ControllerBase
{
    [HttpPost, Consumes("multipart/form-data"), ProducesResponseType<FamilyMedicalCaseResponse>(201)]
    [Permission(PermissionNames.FamilyMedicalAccessRequestsCreateOwn), RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] FamilyMedicalCreateBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (body.EvidenceFiles.Count != body.DocumentTypes.Count) return new[] { FamilyMedicalErrors.Invalid }.ToActionProblem(ct);
        var result = await sender.Send(new FamilyMedicalCommand("create", RequestType: body.RequestType, ChildPatientId: body.ChildPatientId,
            AffectedAdultPatientId: body.AffectedAdultPatientId, Reason: body.Reason,
            Evidence: FamilyRelationshipRequestsController.ToEvidence(body.EvidenceFiles, body.DocumentTypes), IdempotencyKey: key), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("mine"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalQueueResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]
    public async Task<IActionResult> Mine([FromQuery] FamilyMedicalRequestType? requestType, [FromQuery] FamilyMedicalRequestStatus? status,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new FamilyMedicalQuery("own-list", RequestType: requestType, Status: (int?)status, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]
    public async Task<IActionResult> Detail(Guid requestId, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalQuery("own-detail", requestId), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}/history"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalHistoryResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwn)]
    public async Task<IActionResult> History(Guid requestId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new FamilyMedicalQuery("own-history", requestId, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}/documents/{documentId:guid}/content"), Produces("application/octet-stream"), ProducesResponseType(200),
        Permission(PermissionNames.FamilyMedicalAccessRequestsViewOwnDocuments), EnableRateLimiting("medical-record-media")]
    public async Task<IActionResult> Document(Guid requestId, Guid documentId, CancellationToken ct)
    {
        var result = await sender.Send(new FamilyMedicalEvidenceQuery(requestId, documentId, false), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, false) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{requestId:guid}/withdraw"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsWithdrawOwn)]
    public async Task<IActionResult> Withdraw(Guid requestId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("withdraw", requestId, RowVersion: body.RowVersion, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/resubmit"), Consumes("multipart/form-data"), ProducesResponseType<FamilyMedicalCaseResponse>(200),
        Permission(PermissionNames.FamilyMedicalAccessRequestsResubmitOwn), RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<IActionResult> Resubmit(Guid requestId, [FromForm] FamilyMedicalResubmitBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (body.EvidenceFiles.Count != body.DocumentTypes.Count) return new[] { FamilyMedicalErrors.Invalid }.ToActionProblem(ct);
        return (await sender.Send(new FamilyMedicalCommand("resubmit", requestId, Reason: body.Reason, RowVersion: body.RowVersion,
            Evidence: FamilyRelationshipRequestsController.ToEvidence(body.EvidenceFiles, body.DocumentTypes), IdempotencyKey: key), ct)).ToIActionResult(ct);
    }
}

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}/medical-access")]
public sealed class FamilyMedicalStatusController(ISender sender) : ControllerBase
{
    [HttpGet, ProducesResponseType<FamilyMedicalAccessStatusResponse>(200), Permission(PermissionNames.FamiliesViewOwn)]
    public async Task<IActionResult> Status(Guid childPatientId, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalQuery("status", childPatientId), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[Route("api/v{version:apiVersion}/admin/family-medical-access-requests")]
public sealed class AdminFamilyMedicalAccessRequestsController(ISender sender) : ControllerBase
{
    [HttpGet("summary"), ProducesResponseType<FamilyMedicalSummaryResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAll)]
    public async Task<IActionResult> Summary(CancellationToken ct) => (await sender.Send(new FamilyMedicalQuery("admin-summary"), ct)).ToIActionResult(ct);
    [HttpGet, ProducesResponseType<FamilyMedicalPage<FamilyMedicalQueueResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAll)]
    public async Task<IActionResult> List([FromQuery(Name = "type")] FamilyMedicalRequestType? type, [FromQuery] FamilyMedicalRequestStatus? status,
        [FromQuery] Guid? familyId, [FromQuery] string? search, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new FamilyMedicalQuery("admin-list", RequestType: type, Status: (int?)status, FamilyId: familyId, Search: search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}"), ProducesResponseType<FamilyMedicalAdminCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails)]
    public async Task<IActionResult> Detail(Guid requestId, CancellationToken ct) => (await sender.Send(new FamilyMedicalQuery("admin-detail", requestId), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}/history"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalAdminHistoryResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails)]
    public async Task<IActionResult> History(Guid requestId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new FamilyMedicalQuery("admin-history", requestId, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}/documents/{documentId:guid}/content"), Produces("application/octet-stream"), ProducesResponseType(200),
        Permission(PermissionNames.FamilyMedicalAccessRequestsViewAdminDocuments), EnableRateLimiting("medical-record-media")]
    public async Task<IActionResult> Document(Guid requestId, Guid documentId, CancellationToken ct)
    {
        var result = await sender.Send(new FamilyMedicalEvidenceQuery(requestId, documentId, true), ct); Response.Headers["X-Content-Type-Options"] = "nosniff";
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, false) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{requestId:guid}/approve"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsDecide)]
    public async Task<IActionResult> Approve(Guid requestId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("approve", requestId, Reason: body.DecisionNote, RowVersion: body.RowVersion, IdempotencyKey: key, BasisReference: body.BasisReference), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/reject"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsDecide)]
    public async Task<IActionResult> Reject(Guid requestId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("reject", requestId, Reason: body.Reason, RowVersion: body.RowVersion, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/request-modification"), ProducesResponseType<FamilyMedicalCaseResponse>(200), Permission(PermissionNames.FamilyMedicalAccessRequestsRequestModification)]
    public async Task<IActionResult> Modify(Guid requestId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("request-modification", requestId, Reason: body.Message, RowVersion: body.RowVersion, IdempotencyKey: key), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[Route("api/v{version:apiVersion}/admin/family-medical-eligibilities")]
public sealed class AdminFamilyMedicalEligibilitiesController(ISender sender) : ControllerBase
{
    [HttpGet("reviews"), ProducesResponseType<FamilyMedicalPage<FamilyMedicalEligibilityQueueResponse>>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityReview)]
    public async Task<IActionResult> Reviews([FromQuery] FamilyMedicalEligibilityStatus? status, [FromQuery] Guid? familyId, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new FamilyMedicalQuery("eligibility-list", Status: (int?)status, FamilyId: familyId, Search: search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{reviewId:guid}"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityReview)]
    public async Task<IActionResult> Detail(Guid reviewId, CancellationToken ct) => (await sender.Send(new FamilyMedicalQuery("eligibility-detail", reviewId), ct)).ToIActionResult(ct);
    [HttpPost("{reviewId:guid}/approve"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityDecide)]
    public async Task<IActionResult> Approve(Guid reviewId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("eligibility-approve", reviewId, RowVersion: body.RowVersion, IdempotencyKey: key, PolicyVersion: body.PolicyVersion, BasisReference: body.BasisReference, AuthorityRelationshipRequestId: body.AuthorityRelationshipRequestId), ct)).ToIActionResult(ct);
    [HttpPost("{reviewId:guid}/reject"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityDecide)]
    public async Task<IActionResult> Reject(Guid reviewId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("eligibility-reject", reviewId, Reason: body.Reason, RowVersion: body.RowVersion, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("{reviewId:guid}/revoke"), ProducesResponseType<FamilyMedicalEligibilityResponse>(200), Permission(PermissionNames.FamilyMedicalAccessEligibilityRevoke)]
    public async Task<IActionResult> Revoke(Guid reviewId, FamilyMedicalDecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new FamilyMedicalCommand("eligibility-revoke", reviewId, Reason: body.Reason, RowVersion: body.RowVersion, IdempotencyKey: key), ct)).ToIActionResult(ct);
}

public sealed record FamilyMedicalDecisionBody(string RowVersion, string? Reason = null, string? Message = null,
    string? DecisionNote = null, string? PolicyVersion = null, string? BasisReference = null, Guid? AuthorityRelationshipRequestId = null);
public sealed class FamilyMedicalCreateBody
{
    public FamilyMedicalRequestType RequestType { get; set; }
    public Guid ChildPatientId { get; set; }
    public Guid AffectedAdultPatientId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
public sealed class FamilyMedicalResubmitBody
{
    public string RowVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
