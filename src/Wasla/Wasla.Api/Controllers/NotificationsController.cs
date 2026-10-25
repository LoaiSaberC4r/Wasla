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
[Authorize]
[Route("api/v{version:apiVersion}/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet("mine")]
    [Permission(PermissionNames.NotificationsViewOwn)]
    public async Task<IActionResult> Mine([FromQuery] InboxFilter filter, CancellationToken ct)
        => (await sender.Send(new ListMyNotificationsQuery(filter), ct)).ToIActionResult(ct);
    [HttpGet("mine/unread-count")]
    [Permission(PermissionNames.NotificationsViewOwn)]
    public async Task<IActionResult> Unread(CancellationToken ct)
        => (await sender.Send(new GetMyUnreadCountQuery(), ct)).ToIActionResult(ct);
    [HttpPost("mine/{notificationId:guid}/read")]
    [Permission(PermissionNames.NotificationsMarkReadOwn)]
    public async Task<IActionResult> Read(Guid notificationId, CancellationToken ct)
        => (await sender.Send(new MarkMyNotificationReadCommand(notificationId), ct)).ToIActionResult(ct);
    [HttpPost("mine/read-all")]
    [Permission(PermissionNames.NotificationsMarkReadOwn)]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
        => (await sender.Send(new MarkAllMyNotificationsReadCommand(), ct)).ToIActionResult(ct);
    [HttpGet("me/preferences")]
    [Permission(PermissionNames.NotificationsPreferencesViewOwn)]
    public async Task<IActionResult> Preferences(CancellationToken ct)
        => (await sender.Send(new GetMyNotificationPreferencesQuery(), ct)).ToIActionResult(ct);
    [HttpPut("me/preferences")]
    [Permission(PermissionNames.NotificationsPreferencesManageOwn)]
    public async Task<IActionResult> Preferences(UpdatePreferencesRequest request, CancellationToken ct)
        => (await sender.Send(new UpdateMyNotificationPreferencesCommand(request), ct)).ToIActionResult(ct);
}
