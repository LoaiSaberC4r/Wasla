using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Notifications;

public static class NotificationErrors
{
    public static Error Denied => Error.Security("Notifications.AccessDenied", ErrorMessage.GetString("NotificationAccessDenied"));
    public static Error Missing => Error.NotFound("Notifications.NotFound", ErrorMessage.GetString("NotificationNotFound"));
    public static Error Conflict => Error.Conflict("Notifications.Conflict", ErrorMessage.GetString("NotificationConflict"));
}
