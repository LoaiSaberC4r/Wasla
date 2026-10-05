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

public sealed record DrugCatalogManagerResponse(Guid Id, string UserName, string Email, string? PhoneNumber,
    bool IsActive, bool IsFirstLogin, DateTime CreatedOnUtc);
public interface IDrugCatalogManagerReader
{
    Task<ClinicalPage<DrugCatalogManagerResponse>> ListAsync(string? search, int page, int size, CancellationToken ct);
}
public sealed record ListDrugCatalogManagersQuery(string? Search = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<ClinicalPage<DrugCatalogManagerResponse>>;
public sealed record GetDrugCatalogManagerQuery(Guid Id) : IQuery<DrugCatalogManagerResponse>;
public sealed record CreateDrugCatalogManagerCommand(string UserName, string Email, string? PhoneNumber,
    string InitialPassword, string ConfirmPassword) : ICommand<DrugCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateDrugCatalogManagerCommand(Guid Id, string Email, string? PhoneNumber)
    : ICommand<DrugCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record SetDrugCatalogManagerActiveCommand(Guid Id, bool Active)
    : ICommand<DrugCatalogManagerResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class CreateDrugCatalogManagerValidator : AbstractValidator<CreateDrugCatalogManagerCommand>
{
    public CreateDrugCatalogManagerValidator()
    {
        RuleFor(r => r.UserName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(r => r.PhoneNumber).MaximumLength(30);
        RuleFor(r => r.InitialPassword).NotEmpty();
        RuleFor(r => r.ConfirmPassword).Equal(r => r.InitialPassword);
    }
}
internal sealed class UpdateDrugCatalogManagerValidator : AbstractValidator<UpdateDrugCatalogManagerCommand>
{
    public UpdateDrugCatalogManagerValidator()
    {
        RuleFor(r => r.Id).NotEmpty(); RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(r => r.PhoneNumber).MaximumLength(30);
    }
}
internal sealed class DrugCatalogManagerAccounts(IWaslaDataStore store, ICurrentUser current)
{
    public async Task<bool> AuthorizeAsync(string permission, CancellationToken ct)
    {
        if (!await RootGuard.IsRootAsync(store, current, ct) || !current.Permissions.Contains(permission)) return false;
        var snapshot = await store.GetAccessSnapshotAsync(current.UserId!.Value, ct);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false, IsRootSuperAdmin: true };
    }
    public async Task<ApplicationUser?> FindAsync(Guid id, CancellationToken ct)
    {
        var user = await store.FindUserByIdAsync(id, ct);
        return user?.UserType == UserType.DrugCatalogManager ? user : null;
    }
    public static DrugCatalogManagerResponse Map(ApplicationUser u)
        => new(u.Id, u.UserName, u.Email, u.PhoneNumber, u.IsActive, u.IsFirstLogin, u.CreatedOnUtc);
}
internal sealed class ListDrugCatalogManagersHandler(DrugCatalogManagerAccounts accounts, IDrugCatalogManagerReader reader)
    : IQueryHandler<ListDrugCatalogManagersQuery, ClinicalPage<DrugCatalogManagerResponse>>
{
    public async Task<Result<ClinicalPage<DrugCatalogManagerResponse>>> Handle(ListDrugCatalogManagersQuery r, CancellationToken ct)
        => await accounts.AuthorizeAsync(PermissionNames.DrugCatalogManagersViewAll, ct)
            ? Result<ClinicalPage<DrugCatalogManagerResponse>>.Ok(await reader.ListAsync(r.Search, Math.Max(1, r.PageNumber), Math.Clamp(r.PageSize, 1, 100), ct))
            : Result<ClinicalPage<DrugCatalogManagerResponse>>.Fail(SecurityErrors.RootRequired);
}
internal sealed class GetDrugCatalogManagerHandler(DrugCatalogManagerAccounts accounts)
    : IQueryHandler<GetDrugCatalogManagerQuery, DrugCatalogManagerResponse>
{
    public async Task<Result<DrugCatalogManagerResponse>> Handle(GetDrugCatalogManagerQuery r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.DrugCatalogManagersViewDetails, ct)) return Result<DrugCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        return user is null ? Result<DrugCatalogManagerResponse>.Fail(Error.NotFound("DrugCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound)) : Result<DrugCatalogManagerResponse>.Ok(DrugCatalogManagerAccounts.Map(user));
    }
}
internal sealed class CreateDrugCatalogManagerHandler(DrugCatalogManagerAccounts accounts, IWaslaDataStore store,
    IPasswordService passwords, ICurrentUser current, IDateTimeProvider clock)
    : ICommandHandler<CreateDrugCatalogManagerCommand, DrugCatalogManagerResponse>
{
    public async Task<Result<DrugCatalogManagerResponse>> Handle(CreateDrugCatalogManagerCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.DrugCatalogManagersCreate, ct)) return Result<DrugCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        if (await store.UserNameExistsAsync(r.UserName.Trim(), null, ct)) return Result<DrugCatalogManagerResponse>.Fail(GovernanceErrors.UserNameAlreadyExists);
        if (await store.EmailExistsAsync(r.Email.Trim().ToLowerInvariant(), null, ct)) return Result<DrugCatalogManagerResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        if (!passwords.IsStrongPassword(r.InitialPassword)) return Result<DrugCatalogManagerResponse>.Fail(GovernanceErrors.PasswordInvalid);
        var role = await store.FindRoleByNameAsync(SystemRoleNames.DrugCatalogManager, ct);
        if (role is null) return Result<DrugCatalogManagerResponse>.Fail(GovernanceErrors.RoleNotFound);
        var created = ApplicationUser.Create(Guid.NewGuid(), r.UserName, r.Email, r.PhoneNumber,
            await passwords.HashAsync(r.InitialPassword, ct), UserType.DrugCatalogManager, true, clock.UtcNow, current.UserId);
        if (created.IsFailure) return Result<DrugCatalogManagerResponse>.Fail(created.Errors);
        store.Add(created.Value); store.Add(new UserRole(Guid.NewGuid(), created.Value.Id, role.Id, current.UserId));
        await store.SaveChangesAsync(ct);
        return Result<DrugCatalogManagerResponse>.Ok(DrugCatalogManagerAccounts.Map(created.Value));
    }
}
internal sealed class UpdateDrugCatalogManagerHandler(DrugCatalogManagerAccounts accounts, IWaslaDataStore store)
    : ICommandHandler<UpdateDrugCatalogManagerCommand, DrugCatalogManagerResponse>
{
    public async Task<Result<DrugCatalogManagerResponse>> Handle(UpdateDrugCatalogManagerCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(PermissionNames.DrugCatalogManagersUpdate, ct)) return Result<DrugCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        if (user is null) return Result<DrugCatalogManagerResponse>.Fail(Error.NotFound("DrugCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound));
        if (await store.EmailExistsAsync(r.Email.Trim().ToLowerInvariant(), user.Id, ct)) return Result<DrugCatalogManagerResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        user.UpdateContact(r.Email, r.PhoneNumber); await store.SaveChangesAsync(ct);
        return Result<DrugCatalogManagerResponse>.Ok(DrugCatalogManagerAccounts.Map(user));
    }
}
internal sealed class SetDrugCatalogManagerActiveHandler(DrugCatalogManagerAccounts accounts, IWaslaDataStore store)
    : ICommandHandler<SetDrugCatalogManagerActiveCommand, DrugCatalogManagerResponse>
{
    public async Task<Result<DrugCatalogManagerResponse>> Handle(SetDrugCatalogManagerActiveCommand r, CancellationToken ct)
    {
        if (!await accounts.AuthorizeAsync(r.Active ? PermissionNames.DrugCatalogManagersActivate : PermissionNames.DrugCatalogManagersDeactivate, ct)) return Result<DrugCatalogManagerResponse>.Fail(SecurityErrors.RootRequired);
        var user = await accounts.FindAsync(r.Id, ct);
        if (user is null) return Result<DrugCatalogManagerResponse>.Fail(Error.NotFound("DrugCatalogManager.NotFound", Wasla.Domain.Resources.ErrorMessage.ApplicationUserNotFound));
        if (r.Active) user.Activate(); else user.Deactivate();
        await store.SaveChangesAsync(ct);
        return Result<DrugCatalogManagerResponse>.Ok(DrugCatalogManagerAccounts.Map(user));
    }
}
