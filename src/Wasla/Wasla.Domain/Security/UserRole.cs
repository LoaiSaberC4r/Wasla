using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace Wasla.Domain.Security;

public sealed class UserRole : Entity<Guid>, IAuditableEntity
{
    private UserRole()
    {
    }

    public UserRole(
        Guid id,
        Guid applicationUserId,
        Guid roleId,
        Guid? createdByApplicationUserId = null)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(applicationUserId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(roleId, Guid.Empty);
        ApplicationUserId = applicationUserId;
        RoleId = roleId;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public Guid ApplicationUserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
}
