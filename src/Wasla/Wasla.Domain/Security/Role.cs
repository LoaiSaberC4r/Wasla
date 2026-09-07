using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public sealed class Role : AggregateRoot<Guid>, IAuditableEntity
{
    private Role()
    {
    }

    private Role(Guid id, string name, bool isSystemRole, Guid? createdByApplicationUserId)
        : base(id)
    {
        Name = name;
        IsSystemRole = isSystemRole;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsSystemRole { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public static Result<Role> Create(
        Guid id,
        string name,
        bool isSystemRole,
        Guid? createdByApplicationUserId = null)
    {
        var normalized = name?.Trim() ?? string.Empty;
        return id == Guid.Empty || normalized.Length is 0 or > 150
            ? Result<Role>.Fail(Error.Domain("Role.Invalid", ErrorMessage.RoleNotFound))
            : Result<Role>.Ok(new Role(id, normalized, isSystemRole, createdByApplicationUserId));
    }
}

