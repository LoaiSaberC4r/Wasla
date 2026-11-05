using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Api.Controllers;

public sealed record MovePracticeAllocationRequest(Guid TargetSubscriptionContractId, IReadOnlyList<Guid> ContinuedReceptionIds, string RowVersion);
public sealed record ReleasePracticeAllocationRequest(string RowVersion, string? Reason = null);
[ApiController, ApiVersion("1.0"), Authorize, Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/subscription-allocation")]
public sealed class PracticeSubscriptionAllocationsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> Get(Guid practiceId, CancellationToken ct) => (await sender.Send(new PracticeAllocationQuery(practiceId), ct)).ToIActionResult(ct);
    [HttpPost("move"), Permission(SubscriptionPermissionNames.AllocationsManageOwn)]
    public async Task<IActionResult> Move(Guid practiceId, MovePracticeAllocationRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new PracticeAllocationCommand(practiceId, request.TargetSubscriptionContractId, request.ContinuedReceptionIds, request.RowVersion, key), ct)).ToIActionResult(ct);
    [HttpPost("release"), Permission(SubscriptionPermissionNames.AllocationsManageOwn)]
    public async Task<IActionResult> Release(Guid practiceId, ReleasePracticeAllocationRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new PracticeAllocationCommand(practiceId, null, [], request.RowVersion, key, request.Reason), ct)).ToIActionResult(ct);
}
