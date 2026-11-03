using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Subscriptions;
using Wasla.Application.Persistence;
using Wasla.Domain.Subscriptions;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, Route("api/v{version:apiVersion}/admin/subscriptions")]
public sealed class AdminSubscriptionsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(SubscriptionPermissionNames.ViewAll)]
    [ProducesResponseType<Wasla.Application.Features.Doctors.PagedResponse<DoctorSubscription>>(200)]
    public async Task<IActionResult> List([FromQuery] SubscriptionListFilter filter, CancellationToken ct) => (await sender.Send(new SubscriptionListQuery(filter), ct)).ToIActionResult(ct);
    [HttpGet("{subscriptionId:guid}"), Permission(SubscriptionPermissionNames.ViewDetails)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public async Task<IActionResult> Get(Guid subscriptionId, CancellationToken ct) => (await sender.Send(new SubscriptionQuery(subscriptionId), ct)).ToIActionResult(ct);
    [HttpPost("{subscriptionId:guid}/activate"), Permission(SubscriptionPermissionNames.Activate)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public Task<IActionResult> Activate(Guid subscriptionId, SubscriptionMutationRequest request, CancellationToken ct) => Execute(SubscriptionOperation.Activate, subscriptionId, request, ct);
    [HttpPost("{subscriptionId:guid}/trial-extend"), Permission(SubscriptionPermissionNames.ExtendTrial)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public Task<IActionResult> ExtendTrial(Guid subscriptionId, SubscriptionMutationRequest request, CancellationToken ct) => Execute(SubscriptionOperation.ExtendTrial, subscriptionId, request, ct);
    [HttpPost("{subscriptionId:guid}/renew"), Permission(SubscriptionPermissionNames.Renew)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public Task<IActionResult> Renew(Guid subscriptionId, SubscriptionMutationRequest request, CancellationToken ct) => Execute(SubscriptionOperation.Renew, subscriptionId, request, ct);
    [HttpPost("{subscriptionId:guid}/change-requests/{requestId:guid}/approve"), Permission(SubscriptionPermissionNames.ReviewChangeRequests)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public Task<IActionResult> ApproveChange(Guid subscriptionId, Guid requestId, SubscriptionMutationRequest request, CancellationToken ct) => Execute(SubscriptionOperation.ApproveChange, subscriptionId, request, ct, requestId);
    [HttpPost("{subscriptionId:guid}/change-requests/{requestId:guid}/reject"), Permission(SubscriptionPermissionNames.ReviewChangeRequests)]
    [ProducesResponseType<SubscriptionDetails>(200)]
    public Task<IActionResult> RejectChange(Guid subscriptionId, Guid requestId, SubscriptionMutationRequest request, CancellationToken ct) => Execute(SubscriptionOperation.RejectChange, subscriptionId, request, ct, requestId);
    [HttpPost("legacy-transition/enroll"), Permission(SubscriptionPermissionNames.EnrollLegacyTransition)]
    [ProducesResponseType<LegacyTransitionEnrollmentBatch>(200)]
    public async Task<IActionResult> Enroll(CancellationToken ct) => (await sender.Send(new EnrollLegacyTransitionCommand(), ct)).ToIActionResult(ct);
    private async Task<IActionResult> Execute(SubscriptionOperation operation, Guid id, SubscriptionMutationRequest request, CancellationToken ct, Guid? requestId = null) =>
        (await sender.Send(new SubscriptionCommand(operation, id, request, Request.Headers["Idempotency-Key"].ToString(), requestId), ct)).ToIActionResult(ct);
}
