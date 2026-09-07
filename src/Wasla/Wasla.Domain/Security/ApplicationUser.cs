using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public sealed class ApplicationUser : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<UserRole> _userRoles = [];

    private ApplicationUser()
    {
    }

    private ApplicationUser(
        Guid id,
        string userName,
        string email,
        string? phoneNumber,
        string passwordHash,
        UserType userType,
        bool isFirstLogin,
        DateTime passwordChangedOnUtc,
        Guid? createdByApplicationUserId)
        : base(id)
    {
        UserName = userName;
        Email = email;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        UserType = userType;
        IsActive = true;
        IsFirstLogin = isFirstLogin;
        PasswordChangedOnUtc = passwordChangedOnUtc;
        CreatedByApplicationUserId = createdByApplicationUserId;
    }

    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public UserType UserType { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsFirstLogin { get; private set; }
    public DateTime PasswordChangedOnUtc { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public static Result<ApplicationUser> Create(
        Guid id,
        string userName,
        string email,
        string? phoneNumber,
        string passwordHash,
        UserType userType,
        bool isFirstLogin,
        DateTime passwordChangedOnUtc,
        Guid? createdByApplicationUserId = null)
    {
        var normalizedUserName = userName?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (id == Guid.Empty ||
            normalizedUserName.Length is 0 or > 100 ||
            normalizedEmail.Length is 0 or > 200 ||
            normalizedPhone?.Length > 30 ||
            string.IsNullOrWhiteSpace(passwordHash) ||
            passwordHash.Length > 1000 ||
            !Enum.IsDefined(userType))
        {
            return Result<ApplicationUser>.Fail(Error.Domain(
                "Identity.InvalidApplicationUser",
                ErrorMessage.ApplicationUserNotFound));
        }

        return Result<ApplicationUser>.Ok(new ApplicationUser(
            id,
            normalizedUserName,
            normalizedEmail,
            normalizedPhone,
            passwordHash,
            userType,
            isFirstLogin,
            RequireUtc(passwordChangedOnUtc),
            createdByApplicationUserId));
    }

    public void ChangePassword(string passwordHash, DateTime changedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (passwordHash.Length > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        PasswordChangedOnUtc = RequireUtc(changedOnUtc);
        IsFirstLogin = false;
    }

    public void UpdateContact(string email, string? phoneNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (normalizedEmail.Length > 200 || normalizedPhone?.Length > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(email));
        }

        Email = normalizedEmail;
        PhoneNumber = normalizedPhone;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

