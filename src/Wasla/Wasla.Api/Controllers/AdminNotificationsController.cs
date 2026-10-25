using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = SystemRoleNames.SuperAdmin)]
[Route("api/v{version:apiVersion}/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminNotificationsController(ISender sender) : ControllerBase
{
    [HttpGet("notification-settings")]
    [Permission(PermissionNames.NotificationsSettingsView)]
    public async Task<IActionResult> Settings(CancellationToken ct)
        => (await sender.Send(new GetPlatformNotificationSettingsQuery(), ct)).ToIActionResult(ct);
    [HttpPut("notification-settings")]
    [Permission(PermissionNames.NotificationsSettingsManage)]
    public async Task<IActionResult> Settings(UpdateNotificationSettingsRequest request, CancellationToken ct)
        => (await sender.Send(new UpdatePlatformNotificationSettingsCommand(request), ct)).ToIActionResult(ct);
    [HttpGet("notification-deliveries")]
    [Permission(PermissionNames.NotificationsDeliveriesView)]
    public async Task<IActionResult> Deliveries([FromQuery] DeliveryFilter filter, CancellationToken ct)
        => (await sender.Send(new ListNotificationDeliveriesQuery(filter), ct)).ToIActionResult(ct);
    [HttpGet("notification-deliveries/{deliveryId:guid}")]
    [Permission(PermissionNames.NotificationsDeliveriesView)]
    public async Task<IActionResult> Delivery(Guid deliveryId, CancellationToken ct)
        => (await sender.Send(new GetNotificationDeliveryQuery(deliveryId), ct)).ToIActionResult(ct);
    [HttpPost("notification-deliveries/{deliveryId:guid}/retry")]
    [Permission(PermissionNames.NotificationsDeliveriesRetry)]
    public async Task<IActionResult> Retry(Guid deliveryId, RetryDeliveryRequest request, CancellationToken ct)
        => (await sender.Send(new RetryNotificationDeliveryCommand(deliveryId, request), ct)).ToIActionResult(ct);
}
