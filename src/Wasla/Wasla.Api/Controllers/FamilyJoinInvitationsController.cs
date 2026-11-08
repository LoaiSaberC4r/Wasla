using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;
namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine")]
public sealed class FamilyJoinInvitationsController(ISender sender) : ControllerBase
{
    [HttpPost("join-invitations"), Permission(FamilyGovernancePermissionNames.InviteCreate)]
    public async Task<IActionResult> Create([FromBody] CreateFamilyJoinInvitationBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new CreateFamilyJoinInvitationCommand(body.ChildPatientId, body.IntendedParentRole, key), ct)).ToIActionResult(ct);
    [HttpPost("join-invitations/revoke"), Permission(FamilyGovernancePermissionNames.InviteCreate)]
    public async Task<IActionResult> Revoke([FromBody] RevokeFamilyJoinInvitationBody body,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new RevokeFamilyJoinInvitationCommand(body.InvitationReference, key), ct)).ToIActionResult(ct);
    [HttpPost("join-requests"), Permission(FamilyGovernancePermissionNames.InviteRedeem)]
    [Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public async Task<IActionResult> Redeem([FromForm] RedeemFamilyJoinInvitationForm form,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (form.EvidenceFiles.Count != form.DocumentTypes.Count) return new[] { FamilyErrors.EvidenceRequired }.ToActionProblem(ct);
        var result = await sender.Send(new RedeemFamilyJoinInvitationCommand(form.InvitationReference, form.IntendedParentRole,
            FamilyRelationshipRequestsController.ToEvidence(form.EvidenceFiles, form.DocumentTypes), key), ct);
        return result.IsSuccess ? StatusCode(202, result.Value) : result.Errors.ToActionProblem(ct);
    }
}
public sealed record RevokeFamilyJoinInvitationBody(string InvitationReference);
public sealed record CreateFamilyJoinInvitationBody(Guid ChildPatientId, FamilyMemberRole IntendedParentRole);
public sealed class RedeemFamilyJoinInvitationForm
{
    public string InvitationReference { get; set; } = string.Empty;
    public FamilyMemberRole IntendedParentRole { get; set; }
    public List<IFormFile> EvidenceFiles { get; set; } = [];
    public List<FamilyRelationshipDocumentType> DocumentTypes { get; set; } = [];
}
