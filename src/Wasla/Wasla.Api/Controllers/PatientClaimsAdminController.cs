using Wasla.Api.OpenApi;
using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Api.Security;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Application.Media;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;
[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/admin/patient-claims"), Authorize]
[EnableRateLimiting("patient-identity"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientClaimsAdminController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(PermissionNames.PatientClaimsViewAll)]
    [ProducesResponseType<PatientIdentityPage<PatientClaimListItem>>(200)]
    public Task<IActionResult> List([FromQuery] PatientClaimStatus? status, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] string? emailSearch, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Read(PatientIdentityRead.Claims,
            new() { Status = status, FromUtc = fromUtc, ToUtc = toUtc, Email = emailSearch, PageNumber = pageNumber, PageSize = pageSize }, null, ct);
    [HttpGet("{id:guid}"), Permission(PermissionNames.PatientClaimsViewDetails)]
    [ProducesResponseType<PatientClaimDetailsResponse>(200)]
    public Task<IActionResult> Details(Guid id, CancellationToken ct) => Read(PatientIdentityRead.ClaimDetails, new(), id, ct);
    [HttpGet("{id:guid}/evidence/{evidenceId:guid}"), Permission(PermissionNames.PatientClaimsViewEvidence)]
    public async Task<IActionResult> Evidence(Guid id, Guid evidenceId, CancellationToken ct)
    {
        var r = await sender.Send(new PatientIdentityQuery(PatientIdentityRead.ClaimEvidence, new(), Id: id, EvidenceId: evidenceId), ct);
        if (r.IsFailure) return r.Errors.ToActionProblem(ct);
        var file = (PrivateMedia)r.Value; Response.Headers.CacheControl = "no-store"; return File(file.Content, file.ContentType, file.FileName);
    }
    [HttpPost("{id:guid}/approve"), Permission(PermissionNames.PatientClaimsApprove)]
    [ProducesResponseType<PatientClaimApprovedResponse>(200)]
    public Task<IActionResult> Approve(Guid id, ApprovePatientClaimRequest p, CancellationToken ct) => Write(PatientIdentityMutation.Approve, id,
        new() { TargetPatientId = p.TargetPatientId, RowVersion = p.RowVersion, ReviewedEvidenceIds = p.ReviewedEvidenceIds, Reason = p.ReviewNote }, ct);
    [HttpPost("{id:guid}/reject"), Permission(PermissionNames.PatientClaimsReject)]
    [ProducesResponseType<PatientClaimDecisionResponse>(200)]
    public Task<IActionResult> Reject(Guid id, PatientClaimDecisionRequest p, CancellationToken ct) => Write(PatientIdentityMutation.Reject, id, new() { RowVersion = p.RowVersion, Reason = p.Reason }, ct);
    [HttpPost("{id:guid}/request-modification"), Permission(PermissionNames.PatientClaimsRequestModification)]
    [ProducesResponseType<PatientClaimDecisionResponse>(200)]
    public Task<IActionResult> Modify(Guid id, PatientClaimDecisionRequest p, CancellationToken ct) => Write(PatientIdentityMutation.Modify, id, new() { RowVersion = p.RowVersion, Reason = p.Reason }, ct);
    [HttpGet("{id:guid}/history"), Permission(PermissionNames.PatientClaimsViewDetails)]
    [ProducesResponseType<PatientClaimHistoryResponse>(200)]
    public Task<IActionResult> History(Guid id, CancellationToken ct) => Read(PatientIdentityRead.ClaimHistory, new(), id, ct);
    private async Task<IActionResult> Read(PatientIdentityRead op, PatientIdentityInput input, Guid? id, CancellationToken ct) => (await sender.Send(new PatientIdentityQuery(op, input, Id: id), ct)).ToIActionResult(ct);
    private async Task<IActionResult> Write(PatientIdentityMutation op, Guid id, PatientIdentityInput input, CancellationToken ct) => (await sender.Send(new PatientIdentityCommand(op, input, Request.Headers["Idempotency-Key"].ToString(), Id: id), ct)).ToIActionResult(ct);
}
public sealed record ApprovePatientClaimRequest(Guid TargetPatientId, string RowVersion, Guid[] ReviewedEvidenceIds, string? ReviewNote = null);
public sealed record PatientClaimDecisionRequest(string RowVersion, string Reason);
