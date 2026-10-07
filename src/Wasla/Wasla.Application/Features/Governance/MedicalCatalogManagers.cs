using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Governance;

public sealed record MedicalCatalogManagerResponse(Guid Id, string UserName, string Email, string? PhoneNumber,
    bool IsActive, bool IsFirstLogin, DateTime CreatedOnUtc);
public interface IMedicalCatalogManagerReader
{
    Task<ClinicalPage<MedicalCatalogManagerResponse>> ListAsync(string? search, int page, int size, CancellationToken ct);
}
public sealed record ListMedicalCatalogManagersQuery(string? Search = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<ClinicalPage<MedicalCatalogManagerResponse>>;
public sealed record GetMedicalCatalogManagerQuery(Guid Id) : IQuery<MedicalCatalogManagerResponse>;
public sealed record CreateMedicalCatalogManagerCommand(string UserName, string Email, string? PhoneNumber,
    string InitialPassword, string ConfirmPassword) : ICommand<MedicalCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateMedicalCatalogManagerCommand(Guid Id, string Email, string? PhoneNumber)
    : ICommand<MedicalCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record SetMedicalCatalogManagerActiveCommand(Guid Id, bool Active)
    : ICommand<MedicalCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateMedicalCatalogManagerValidator : AbstractValidator<CreateMedicalCatalogManagerCommand>
{
    public CreateMedicalCatalogManagerValidator()
    {
        RuleFor(r => r.UserName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(r => r.PhoneNumber).MaximumLength(30);
        RuleFor(r => r.InitialPassword).NotEmpty();
        RuleFor(r => r.ConfirmPassword).Equal(r => r.InitialPassword);
    }
}
internal sealed class UpdateMedicalCatalogManagerValidator : AbstractValidator<UpdateMedicalCatalogManagerCommand>
{
    public UpdateMedicalCatalogManagerValidator()
    {
        RuleFor(r => r.Id).NotEmpty(); RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(r => r.PhoneNumber).MaximumLength(30);
    }
}
internal sealed class MedicalCatalogManagerAccounts(IWaslaDataStore store, ICurrentUser current)
{
    public async Task<bool> AuthorizeAsync(string permission, CancellationToken ct)
    {
        if (!await RootGuard.IsRootAsync(store, current, ct) || !current.Permissions.Contains(permission)) return false;
        var snapshot = await store.GetAccessSnapshotAsync(current.UserId!.Value, ct);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false, IsRootSuperAdmin: true } &&
            Wasla.Application.Features.Auth.LoginCommandHandler.EffectivePermissions(snapshot).Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
    public async Task<ApplicationUser?> FindAsync(Guid id, CancellationToken ct)
    {
        var user = await store.FindUserByIdAsync(id, ct);
        return user?.UserType == UserType.MedicalCatalogManager ? user : null;
    }
    public static MedicalCatalogManagerResponse Map(ApplicationUser u)
        => new(u.Id, u.UserName, u.Email, u.PhoneNumber, u.IsActive, u.IsFirstLogin, u.CreatedOnUtc);
}
internal sealed class ListMedicalCatalogManagersHandler(MedicalCatalogManagerAccounts accounts, IMedicalCatalogManagerReader reader)
    : IQueryHandler<ListMedicalCatalogManagersQuery, ClinicalPage<MedicalCatalogManagerResponse>>
{
    public async Task<Result<ClinicalPage<MedicalCatalogManagerResponse>>> Handle(ListMedicalCatalogManagersQuery r, CancellationToken ct)
        => await accounts.AuthorizeAsync(PermissionNames.MedicalCatalogManagersViewAll, ct)
            ? Result<ClinicalPage<MedicalCatalogManagerResponse>>.Ok(await reader.ListAsync(r.Search, Math.Max(1, r.PageNumber), Math.Clamp(r.PageSize, 1, 100), ct))
            : Result<ClinicalPage<MedicalCatalogManagerResponse>>.Fail(SecurityErrors.RootRequired);
}
internal sealed class GetMedicalCatalogManagerHandler(MedicalCatalogManagerAccounts accounts)
    : IQueryHandler<GetMedicalCatalogManagerQuery, MedicalCatalogManagerResponse>
{
    public async Task<Result<MedicalCatalogManagerResponse>> Handle(GetMedicalCatalogManagerQuery r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.MedicalCatalogManagersViewDetails, ct)) return Result<MedicalCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        return user is null ? Result<MedicalCatalogManagerResponse>.Fail(Error.NotFound("MedicalCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound)) : Result<MedicalCatalogManagerResponse>.Ok(MedicalCatalogManagerAccounts.Map(user));
    }
}
internal sealed class CreateMedicalCatalogManagerHandler(MedicalCatalogManagerAccounts accounts, IWaslaDataStore store,
    IPasswordService passwords, ICurrentUser current, IDateTimeProvider clock)
    : ICommandHandler<CreateMedicalCatalogManagerCommand, MedicalCatalogManagerResponse>
{
    public async Task<Result<MedicalCatalogManagerResponse>> Handle(CreateMedicalCatalogManagerCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.MedicalCatalogManagersCreate, ct)) return Result<MedicalCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        if (await store.UserNameExistsAsync(r.UserName.Trim(), null, ct)) return Result<MedicalCatalogManagerResponse>.Fail(GovernanceErrors.UserNameAlreadyExists);
        if (await store.EmailExistsAsync(r.Email.Trim().ToLowerInvariant(), null, ct)) return Result<MedicalCatalogManagerResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        if (!passwords.IsStrongPassword(r.InitialPassword)) return Result<MedicalCatalogManagerResponse>.Fail(GovernanceErrors.PasswordInvalid);
        var role = await store.FindRoleByNameAsync(SystemRoleNames.MedicalCatalogManager, ct);
        if (role is null) return Result<MedicalCatalogManagerResponse>.Fail(GovernanceErrors.RoleNotFound);
        var created = ApplicationUser.Create(Guid.NewGuid(), r.UserName, r.Email, r.PhoneNumber,
            await passwords.HashAsync(r.InitialPassword, ct), UserType.MedicalCatalogManager, true, clock.UtcNow, current.UserId);
        if (created.IsFailure) return Result<MedicalCatalogManagerResponse>.Fail(created.Errors);
        store.Add(created.Value); store.Add(new UserRole(Guid.NewGuid(), created.Value.Id, role.Id, current.UserId));
        await store.SaveChangesAsync(ct);
        return Result<MedicalCatalogManagerResponse>.Ok(MedicalCatalogManagerAccounts.Map(created.Value));
    }
}
internal sealed class UpdateMedicalCatalogManagerHandler(MedicalCatalogManagerAccounts accounts, IWaslaDataStore store)
    : ICommandHandler<UpdateMedicalCatalogManagerCommand, MedicalCatalogManagerResponse>
{
    public async Task<Result<MedicalCatalogManagerResponse>> Handle(UpdateMedicalCatalogManagerCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.MedicalCatalogManagersUpdate, ct)) return Result<MedicalCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        if (user is null) return Result<MedicalCatalogManagerResponse>.Fail(Error.NotFound("MedicalCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound));
        if (await store.EmailExistsAsync(r.Email.Trim().ToLowerInvariant(), user.Id, ct)) return Result<MedicalCatalogManagerResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        user.UpdateContact(r.Email, r.PhoneNumber); await store.SaveChangesAsync(ct);
        return Result<MedicalCatalogManagerResponse>.Ok(MedicalCatalogManagerAccounts.Map(user));
    }
}
internal sealed class SetMedicalCatalogManagerActiveHandler(MedicalCatalogManagerAccounts accounts, IWaslaDataStore store)
    : ICommandHandler<SetMedicalCatalogManagerActiveCommand, MedicalCatalogManagerResponse>
{
    public async Task<Result<MedicalCatalogManagerResponse>> Handle(SetMedicalCatalogManagerActiveCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(r.Active ? PermissionNames.MedicalCatalogManagersActivate : PermissionNames.MedicalCatalogManagersDeactivate, ct)) return Result<MedicalCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        if (user is null) return Result<MedicalCatalogManagerResponse>.Fail(Error.NotFound("MedicalCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound));
        if (r.Active) user.Activate(); else user.Deactivate();
        await store.SaveChangesAsync(ct);
        return Result<MedicalCatalogManagerResponse>.Ok(MedicalCatalogManagerAccounts.Map(user));
    }
}
