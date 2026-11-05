using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, Route("api/v{version:apiVersion}/doctors/me/subscriptions")]
public sealed class DoctorSubscriptionContractsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> List(CancellationToken ct) => (await sender.Send(new MySubscriptionContractsQuery(), ct)).ToIActionResult(ct);
    [HttpGet("{contractId:guid}"), Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> Get(Guid contractId, CancellationToken ct) => (await sender.Send(new MySubscriptionContractsQuery(contractId), ct)).ToIActionResult(ct);
    [HttpGet("trial/eligibility"), Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> Eligibility(CancellationToken ct) => (await sender.Send(new TrialEligibilityQuery(), ct)).ToIActionResult(ct);
    [HttpPost("trial/activate"), Permission(SubscriptionPermissionNames.ActivateTrialOwn)]
    public async Task<IActionResult> ActivateTrial([FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new StartFreeTrialCommand(key), ct)).ToIActionResult(ct);
    [HttpPost("selection"), Permission(SubscriptionPermissionNames.RequestChangeOwn)]
    public async Task<IActionResult> Select(SubscriptionSelection selection, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new SubscriptionCommand(SubscriptionOperation.SelectPaid, null, new(Selection: selection), key), ct)).ToIActionResult(ct);
    [HttpPost("{contractId:guid}/change-requests"), Permission(SubscriptionPermissionNames.RequestChangeOwn)]
    public async Task<IActionResult> Change(Guid contractId, SubscriptionMutationRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new ContractSubscriptionCommand(contractId, SubscriptionOperation.RequestChange, request, key), ct)).ToIActionResult(ct);
    [HttpGet("{contractId:guid}/renewal-preview"), Permission(SubscriptionPermissionNames.ViewOwn)]
    public async Task<IActionResult> Preview(Guid contractId, [FromQuery] string productCode, [FromQuery] string planCode, [FromQuery] BillingCycle billingCycle, [FromQuery] int? selectedPracticeCount, CancellationToken ct) =>
        (await sender.Send(new RenewalPreviewQuery(contractId, new(productCode, planCode, billingCycle, SelectedPracticeCount: selectedPracticeCount)), ct)).ToIActionResult(ct);
    [HttpPost("{contractId:guid}/renewal-selection"), Permission(SubscriptionPermissionNames.RequestChangeOwn)]
    public async Task<IActionResult> Renewal(Guid contractId, SubscriptionMutationRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new RenewalSelectionCommand(contractId, request, key), ct)).ToIActionResult(ct);
    [HttpPost("{contractId:guid}/cancel-at-period-end"), Permission(SubscriptionPermissionNames.CancelAtPeriodEndOwn)]
    public async Task<IActionResult> Cancel(Guid contractId, SubscriptionMutationRequest request, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        (await sender.Send(new ContractSubscriptionCommand(contractId, SubscriptionOperation.Cancel, request, key), ct)).ToIActionResult(ct);
}
