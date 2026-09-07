using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public sealed class SuperAdmin : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private SuperAdmin()
    {
    }

    private SuperAdmin(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        bool isRootSuperAdmin,
        Guid? createdByApplicationUserId)
        : base(id)
    {
        ApplicationUserId = applicationUserId;
        NameAr = nameAr;
        NameEn = nameEn;
        IsRootSuperAdmin = isRootSuperAdmin;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public Guid ApplicationUserId { get; private set; }
    public ApplicationUser ApplicationUser { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public bool IsRootSuperAdmin { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }

    public static Result<SuperAdmin> Create(
        Guid id,
        Guid applicationUserId,
        string nameAr,
        string? nameEn,
        bool isRootSuperAdmin,
        Guid? createdByApplicationUserId)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (id == Guid.Empty || applicationUserId == Guid.Empty ||
            normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200 ||
            isRootSuperAdmin && createdByApplicationUserId is not null)
        {
            return Result<SuperAdmin>.Fail(Error.Domain(
                "SuperAdmin.Invalid",
                ErrorMessage.SuperAdminNotFound));
        }

        return Result<SuperAdmin>.Ok(new SuperAdmin(
            id,
            applicationUserId,
            normalizedAr,
            normalizedEn,
            isRootSuperAdmin,
            createdByApplicationUserId));
    }

    public Result UpdateNames(string nameAr, string? nameEn)
    {
        if (IsRootSuperAdmin)
        {
            return Result.Fail(SecurityErrors.RootProtected);
        }

        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200)
        {
            return Result.Fail(Error.Validation("SuperAdmin.InvalidName", ErrorMessage.NameArRequired));
        }

        NameAr = normalizedAr;
        NameEn = normalizedEn;
        return Result.Ok();
    }

    public Result EnsureMutable()
        => IsRootSuperAdmin ? Result.Fail(SecurityErrors.RootProtected) : Result.Ok();
}

