using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine")]
public sealed class ChildOnboardingController(ISender sender) : ControllerBase
{
    [HttpPost("child-onboarding"), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    [Permission(FamilySelfServicePermissionNames.Onboard)]
    public async Task<IActionResult> Submit([FromForm] ChildOnboardingForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (form.EvidenceFiles.Count != form.DocumentTypes.Count) return new[] { FamilyErrors.EvidenceRequired }.ToActionProblem(ct);
        var result = await sender.Send(new SubmitChildOnboardingCommand(new(form.NameAr, form.NameEn, form.DateOfBirth, form.Gender,
            form.PhoneNumber, form.PrimaryContactNameAr, form.PrimaryContactPhone, form.RelationToChild),
            FamilyRelationshipRequestsController.ToEvidence(form.EvidenceFiles, form.DocumentTypes), key), ct);
        return result.IsSuccess ? StatusCode(202, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("child-onboarding-requests"), Permission(FamilySelfServicePermissionNames.OnboardingOwn)]
    public async Task<IActionResult> Mine([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new GetChildOnboardingRequestsQuery(null, false, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("child-onboarding-requests/{requestId:guid}"), Permission(FamilySelfServicePermissionNames.OnboardingOwn)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken ct)
        => (await sender.Send(new GetChildOnboardingRequestsQuery(requestId, false), ct)).ToIActionResult(ct);
    [HttpPost("child-onboarding-requests/{requestId:guid}/resubmit"), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    [Permission(FamilySelfServicePermissionNames.OnboardingResubmit)]
    public async Task<IActionResult> Resubmit(Guid requestId, [FromForm] ChildOnboardingResubmissionForm form,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (form.EvidenceFiles.Count != form.DocumentTypes.Count) return new[] { FamilyErrors.EvidenceRequired }.ToActionProblem(ct);
        return (await sender.Send(new ResubmitChildOnboardingCommand(requestId, form.RowVersion,
            FamilyRelationshipRequestsController.ToEvidence(form.EvidenceFiles, form.DocumentTypes), key,
            form.NameAr is null ? null : new ChildOnboardingInput(form.NameAr, form.NameEn, form.DateOfBirth, form.Gender, form.PhoneNumber, form.PrimaryContactNameAr, form.PrimaryContactPhone, form.RelationToChild)), ct)).ToIActionResult(ct);
    }
}

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/admin/child-onboarding-requests")]
public sealed class AdminChildOnboardingController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(FamilySelfServicePermissionNames.OnboardingInbox)]
    public async Task<IActionResult> Inbox([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new GetChildOnboardingRequestsQuery(null, true, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}"), Permission(FamilySelfServicePermissionNames.OnboardingDetails)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken ct, [FromQuery] int matchPageNumber = 1, [FromQuery] int matchPageSize = 20)
        => (await sender.Send(new GetChildOnboardingRequestsQuery(requestId, true, MatchPageNumber: matchPageNumber, MatchPageSize: matchPageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}/evidence/{documentId:guid}/content"), Permission(FamilySelfServicePermissionNames.OnboardingEvidence)]
    public async Task<IActionResult> Evidence(Guid requestId, Guid documentId, CancellationToken ct)
    {
        var result = await sender.Send(new GetChildOnboardingEvidenceQuery(requestId, documentId), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{requestId:guid}/approve"), Permission(FamilySelfServicePermissionNames.OnboardingReview)]
    public Task<IActionResult> Approve(Guid requestId, [FromBody] ChildOnboardingDecision form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Decide(requestId, form, key, ChildOnboardingStatus.Approved, ct);
    [HttpPost("{requestId:guid}/reject"), Permission(FamilySelfServicePermissionNames.OnboardingReview)]
    public Task<IActionResult> Reject(Guid requestId, [FromBody] ChildOnboardingDecision form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Decide(requestId, form, key, ChildOnboardingStatus.Rejected, ct);
    [HttpPost("{requestId:guid}/request-modification"), Permission(FamilySelfServicePermissionNames.OnboardingReview)]
    public Task<IActionResult> Modify(Guid requestId, [FromBody] ChildOnboardingDecision form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Decide(requestId, form, key, ChildOnboardingStatus.ModificationRequested, ct);
    private async Task<IActionResult> Decide(Guid id, ChildOnboardingDecision form, string key, ChildOnboardingStatus decision, CancellationToken ct)
        => (await sender.Send(new ReviewChildOnboardingCommand(id, decision, form.RowVersion, form.Reason, form.ConfirmedExistingPatientId, key), ct)).ToIActionResult(ct);
}
[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/admin/family-operational-authorities")]
public sealed class OperationalAuthorityController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(FamilySelfServicePermissionNames.RevokeAuthority)]
    public async Task<IActionResult> List([FromQuery] Guid relationshipRequestId, CancellationToken ct)
        => (await sender.Send(new GetOperationalAuthoritiesQuery(relationshipRequestId), ct)).ToIActionResult(ct);
    [HttpPost("{authorityId:guid}/revoke"), Permission(FamilySelfServicePermissionNames.RevokeAuthority)]
    public async Task<IActionResult> Revoke(Guid authorityId, [FromBody] RejectFamilyRelationshipRequest form,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new RevokeOperationalAuthorityCommand(authorityId, form.RowVersion, form.Reason, key), ct)).ToIActionResult(ct);
}
public sealed record ChildOnboardingDecision(string RowVersion, string? Reason, Guid? ConfirmedExistingPatientId);
public sealed class ChildOnboardingResubmissionForm
{
    public string RowVersion { get; set; } = "";
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PrimaryContactNameAr { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public FamilyMemberRole RelationToChild { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
public sealed class ChildOnboardingForm
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PrimaryContactNameAr { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public FamilyMemberRole RelationToChild { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
