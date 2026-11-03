using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, Route("api/v{version:apiVersion}/doctors/me/subscription")]
[ProducesResponseType<SubscriptionDetails>(200)]
public sealed class DoctorSubscriptionsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> Get(CancellationToken ct) => (await sender.Send(new SubscriptionQuery(), ct)).ToIActionResult(ct);
    [HttpPost("selection"), Permission(SubscriptionPermissionNames.RequestChangeOwn)]
    public async Task<IActionResult> Select(SubscriptionSelection selection, CancellationToken ct) =>
        (await sender.Send(new SubscriptionCommand(SubscriptionOperation.SelectPaid, null, new("", Selection: selection)), ct)).ToIActionResult(ct);
    [HttpPost("change-requests"), Permission(SubscriptionPermissionNames.RequestChangeOwn)]
    public async Task<IActionResult> RequestChange(SubscriptionMutationRequest request, CancellationToken ct) =>
        (await sender.Send(new SubscriptionCommand(SubscriptionOperation.RequestChange, null, request), ct)).ToIActionResult(ct);
    [HttpPost("cancel-at-period-end"), Permission(SubscriptionPermissionNames.CancelAtPeriodEndOwn)]
    public async Task<IActionResult> Cancel(SubscriptionMutationRequest request, CancellationToken ct) =>
        (await sender.Send(new SubscriptionCommand(SubscriptionOperation.Cancel, null, request), ct)).ToIActionResult(ct);
}
