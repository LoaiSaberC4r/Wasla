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
public sealed class FamilyGovernanceController(ISender sender) : ControllerBase
{
    [HttpPost("dependents/{childPatientId:guid}/identity-corrections")]
    [Consumes("multipart/form-data"), RequestSizeLimit(52440000), Permission(FamilyGovernancePermissionNames.IdentityCorrectionRequest)]
    public async Task<IActionResult> Submit(Guid childPatientId, [FromForm] DependentIdentityCorrectionForm form,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (form.EvidenceFiles.Count != form.DocumentTypes.Count) return new[] { FamilyErrors.EvidenceRequired }.ToActionProblem(ct);
        var result = await sender.Send(new SubmitDependentIdentityCorrectionCommand(childPatientId,
            new(form.NameAr, form.NameEn, form.DateOfBirth, form.Gender), form.Reason,
            FamilyRelationshipRequestsController.ToEvidence(form.EvidenceFiles, form.DocumentTypes), key, form.CaseId, form.RowVersion), ct);
        return result.IsSuccess ? StatusCode(202, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("governance-cases/{caseId:guid}/resubmit-conflict"), Permission(FamilySelfServicePermissionNames.Submit)]
    [Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public async Task<IActionResult> ResubmitConflict(Guid caseId, [FromForm] FamilyConflictRevisionForm form,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (form.EvidenceFiles.Count != form.DocumentTypes.Count) return new[] { FamilyErrors.EvidenceRequired }.ToActionProblem(ct);
        return (await sender.Send(new ResubmitFamilyConflictCommand(caseId, form.RowVersion,
            FamilyRelationshipRequestsController.ToEvidence(form.EvidenceFiles, form.DocumentTypes), key), ct)).ToIActionResult(ct);
    }
    [HttpGet("governance-cases"), Permission(FamilySelfServicePermissionNames.Workspace)]
    public async Task<IActionResult> Mine([FromQuery] FamilyGovernanceCaseKind? kind, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new GetFamilyGovernanceCasesQuery(null, false, kind, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("governance-cases/{caseId:guid}"), Permission(FamilySelfServicePermissionNames.Workspace)]
    public async Task<IActionResult> Details(Guid caseId, CancellationToken ct)
        => (await sender.Send(new GetFamilyGovernanceCasesQuery(caseId, false), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/admin/family-governance-cases")]
public sealed class AdminFamilyGovernanceController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(PermissionNames.FamilyRelationshipRequestsViewAll)]
    public async Task<IActionResult> Inbox([FromQuery] FamilyGovernanceCaseKind? kind, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new GetFamilyGovernanceCasesQuery(null, true, kind, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{caseId:guid}"), Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]
    public async Task<IActionResult> Details(Guid caseId, CancellationToken ct)
        => (await sender.Send(new GetFamilyGovernanceCasesQuery(caseId, true), ct)).ToIActionResult(ct);
    [HttpGet("{caseId:guid}/evidence/{evidenceId:guid}/content"), Permission(PermissionNames.FamilyRelationshipRequestsViewDetails)]
    public async Task<IActionResult> Evidence(Guid caseId, Guid evidenceId, CancellationToken ct)
    {
        var result = await sender.Send(new FamilyGovernanceEvidenceQuery(caseId, evidenceId), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{caseId:guid}/approve"), Permission(PermissionNames.FamilyRelationshipRequestsApprove)]
    public Task<IActionResult> Approve(Guid caseId, [FromBody] FamilyGovernanceDecisionBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => Decide(caseId, FamilyGovernanceCaseStatus.Approved, body, key, ct);
    [HttpPost("{caseId:guid}/reject"), Permission(PermissionNames.FamilyRelationshipRequestsReject)]
    public Task<IActionResult> Reject(Guid caseId, [FromBody] FamilyGovernanceDecisionBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => Decide(caseId, FamilyGovernanceCaseStatus.Rejected, body, key, ct);
    [HttpPost("{caseId:guid}/request-modification"), Permission(PermissionNames.FamilyRelationshipRequestsRequestModification)]
    public Task<IActionResult> Modification(Guid caseId, [FromBody] FamilyGovernanceDecisionBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => Decide(caseId, FamilyGovernanceCaseStatus.ModificationRequested, body, key, ct);
    [HttpPost("{caseId:guid}/refer-governance"), Permission(PermissionNames.FamilyRelationshipRequestsApprove)]
    public Task<IActionResult> Refer(Guid caseId, [FromBody] FamilyGovernanceDecisionBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) => Decide(caseId, FamilyGovernanceCaseStatus.PendingGovernanceDecision, body, key, ct);
    private async Task<IActionResult> Decide(Guid id, FamilyGovernanceCaseStatus status, FamilyGovernanceDecisionBody body, string key, CancellationToken ct)
        => (await sender.Send(new DecideFamilyGovernanceCaseCommand(id, status, body.RowVersion, body.Reason, key), ct)).ToIActionResult(ct);
}
public sealed class DependentIdentityCorrectionForm
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? RowVersion { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
public sealed class FamilyConflictRevisionForm
{
    public string RowVersion { get; set; } = string.Empty;
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
public sealed record FamilyGovernanceDecisionBody(string RowVersion, string Reason);
