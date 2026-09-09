using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/families")]
[Authorize]
public sealed class FamiliesController(ISender sender) : ControllerBase
{
    [HttpGet("mine")]
    [Permission(PermissionNames.FamiliesViewOwn)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyFamilyQuery(), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/family-relationship-requests")]
[Authorize]
public sealed class FamilyRelationshipRequestsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.FamilyRelationshipRequestsCreate)]
    public async Task<IActionResult> Submit([FromForm] SubmitFamilyRelationshipRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitFamilyRelationshipRequestCommand(
            request.RequestType, request.FamilyId, request.TargetPatientId, request.RequesterClaimedRole,
            request.TargetClaimedRole, ToEvidence(request.EvidenceFiles, request.DocumentTypes)), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("mine")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]
    public async Task<IActionResult> Mine([FromQuery] FamilyRelationshipRequestStatus? status,
        [FromQuery] FamilyRelationshipRequestType? requestType, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new GetMyFamilyRelationshipRequestsQuery(status, requestType, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetFamilyRelationshipRequestDetailsQuery(requestId, FamilyRelationshipAccessMode.Self), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}/documents/{documentId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewOwn)]
    public async Task<IActionResult> Document(Guid requestId, Guid documentId, CancellationToken cancellationToken)
        => await FileResultAsync(new GetFamilyRelationshipDocumentQuery(requestId, documentId, FamilyRelationshipAccessMode.Self), cancellationToken);

    [HttpPost("{requestId:guid}/resubmit")]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.FamilyRelationshipRequestsResubmitOwn)]
    public async Task<IActionResult> Resubmit(Guid requestId, [FromForm] ResubmitFamilyRelationshipRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ResubmitFamilyRelationshipRequestCommand(
            requestId, ToEvidence(request.EvidenceFiles, request.DocumentTypes), request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    private async Task<IActionResult> FileResultAsync(GetFamilyRelationshipDocumentQuery query, CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, true) : result.Errors.ToActionProblem(cancellationToken);
    }

    internal static IReadOnlyList<FamilyEvidenceUpload> ToEvidence(IReadOnlyList<IFormFile> files, IReadOnlyList<FamilyRelationshipDocumentType> types)
        => files.Select((file, index) => new FamilyEvidenceUpload(
            index < types.Count ? types[index] : 0,
            new MediaUpload(file.OpenReadStream(), file.FileName, file.ContentType, file.Length))).ToArray();
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/family-relationship-requests/assisted")]
[Authorize]
public sealed class AssistedFamilyRelationshipRequestsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.FamilyRelationshipRequestsCreateAssisted)]
    public async Task<IActionResult> Submit([FromForm] SubmitAssistedFamilyRelationshipRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitAssistedFamilyRelationshipRequestCommand(
            request.RequestType, request.FamilyId, request.RequesterPatientId, request.TargetPatientId,
            request.RequesterClaimedRole, request.TargetClaimedRole,
            FamilyRelationshipRequestsController.ToEvidence(request.EvidenceFiles, request.DocumentTypes)), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]
    public async Task<IActionResult> List([FromQuery] FamilyRelationshipRequestStatus? status,
        [FromQuery] FamilyRelationshipRequestType? requestType, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new ListAssistedFamilyRelationshipRequestsQuery(status, requestType, search, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetFamilyRelationshipRequestDetailsQuery(requestId, FamilyRelationshipAccessMode.Assisted), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}/documents/{documentId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewAssisted)]
    public async Task<IActionResult> Document(Guid requestId, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFamilyRelationshipDocumentQuery(requestId, documentId, FamilyRelationshipAccessMode.Assisted), cancellationToken);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, true) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("{requestId:guid}/resubmit")]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.FamilyRelationshipRequestsResubmitAssisted)]
    public async Task<IActionResult> Resubmit(Guid requestId, [FromForm] ResubmitFamilyRelationshipRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ResubmitAssistedFamilyRelationshipRequestCommand(requestId,
            FamilyRelationshipRequestsController.ToEvidence(request.EvidenceFiles, request.DocumentTypes), request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/family-relationship-requests")]
[Authorize]
public sealed class AdminFamilyRelationshipRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewAll)]
    public async Task<IActionResult> List([FromQuery] FamilyRelationshipRequestStatus? status,
        [FromQuery] FamilyRelationshipRequestType? requestType, [FromQuery] string? search,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new ListFamilyRelationshipRequestsQuery(status, requestType, search, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetFamilyRelationshipRequestDetailsQuery(requestId, FamilyRelationshipAccessMode.Admin), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}/documents/{documentId:guid}")]
    [Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]
    public async Task<IActionResult> Document(Guid requestId, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFamilyRelationshipDocumentQuery(requestId, documentId, FamilyRelationshipAccessMode.Admin), cancellationToken);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, true) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("{requestId:guid}/approve")]
    [Permission(PermissionNames.FamilyRelationshipRequestsApprove)]
    public async Task<IActionResult> Approve(Guid requestId, FamilyRowVersionRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ApproveFamilyRelationshipRequestCommand(requestId, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{requestId:guid}/reject")]
    [Permission(PermissionNames.FamilyRelationshipRequestsReject)]
    public async Task<IActionResult> Reject(Guid requestId, RejectFamilyRelationshipRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new RejectFamilyRelationshipRequestCommand(requestId, request.Reason, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{requestId:guid}/request-modification")]
    [Permission(PermissionNames.FamilyRelationshipRequestsRequestModification)]
    public async Task<IActionResult> RequestModification(Guid requestId, ModifyFamilyRelationshipRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new RequestFamilyRelationshipModificationCommand(requestId, request.Message, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}

public class SubmitFamilyRelationshipRequest
{
    public FamilyRelationshipRequestType RequestType { get; set; }
    public Guid? FamilyId { get; set; }
    public Guid TargetPatientId { get; set; }
    public FamilyMemberRole RequesterClaimedRole { get; set; }
    public FamilyMemberRole TargetClaimedRole { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
public sealed class SubmitAssistedFamilyRelationshipRequest : SubmitFamilyRelationshipRequest
{
    public Guid RequesterPatientId { get; set; }
}
public sealed class ResubmitFamilyRelationshipRequest
{
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
    public string RowVersion { get; set; } = string.Empty;
}
public sealed record FamilyRowVersionRequest(string RowVersion);
public sealed record RejectFamilyRelationshipRequest(string Reason, string RowVersion);
public sealed record ModifyFamilyRelationshipRequest(string Message, string RowVersion);
