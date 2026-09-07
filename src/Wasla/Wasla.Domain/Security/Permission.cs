using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public sealed class Permission : AggregateRoot<Guid>, IAuditableEntity
{
    private Permission()
    {
    }

    private Permission(Guid id, string name, bool isSystemPermission, Guid? createdByApplicationUserId)
        : base(id)
    {
        Name = name;
        IsSystemPermission = isSystemPermission;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsSystemPermission { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }

    public static Result<Permission> Create(
        Guid id,
        string name,
        bool isSystemPermission,
        Guid? createdByApplicationUserId = null)
    {
        var normalized = name?.Trim() ?? string.Empty;
        return id == Guid.Empty || normalized.Length is 0 or > 200
            ? Result<Permission>.Fail(Error.Domain("Permission.Invalid", ErrorMessage.PermissionNotFound))
            : Result<Permission>.Ok(new Permission(
                id,
                normalized,
                isSystemPermission,
                createdByApplicationUserId));
    }
}

