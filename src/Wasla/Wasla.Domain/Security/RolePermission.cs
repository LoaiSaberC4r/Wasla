using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace Wasla.Domain.Security;

public sealed class RolePermission : Entity<Guid>, IAuditableEntity
{
    private RolePermission()
    {
    }

    public RolePermission(
        Guid id,
        Guid roleId,
        Guid permissionId,
        Guid? createdByApplicationUserId = null)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(roleId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(permissionId, Guid.Empty);
        RoleId = roleId;
        PermissionId = permissionId;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
}
