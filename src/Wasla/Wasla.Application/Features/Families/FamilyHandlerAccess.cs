using BuildingBlock.Application.Abstraction.Security;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;

namespace Wasla.Application.Features.Families;
internal static class FamilyHandlerAccess
{
    public static async Task<bool> AllowedAsync(IWaslaDataStore store, ICurrentUser current, string permission, UserType type, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } actor || !current.Permissions.Contains(permission)) return false;
        var snapshot = await store.GetAccessSnapshotAsync(actor, ct);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false } && snapshot.User.UserType == type &&
            (type != UserType.SuperAdmin || snapshot.SuperAdminId is not null) && snapshot.Permissions.Contains(permission);
    }
}
