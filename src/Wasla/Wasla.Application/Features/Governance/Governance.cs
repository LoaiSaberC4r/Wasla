using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Governance;

public sealed record ListSuperAdminsQuery(
    string? SearchText,
    int PageNumber = 1,
    int PageSize = 20,
    bool IncludeDeleted = false)
    : IQuery<PagedResponse<SuperAdminResponse>>;

public sealed record GetSuperAdminQuery(Guid SuperAdminId) : IQuery<SuperAdminResponse>;

public sealed record SuperAdminResponse(
    Guid SuperAdminId,
    Guid ApplicationUserId,
    string UserName,
    string Email,
    string? PhoneNumber,
    string NameAr,
    string? NameEn,
    bool IsRootSuperAdmin,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedOnUtc);

public sealed record CreateSuperAdminCommand(
    string UserName,
    string Email,
    string? PhoneNumber,
    string NameAr,
    string? NameEn,
    string InitialPassword,
    string ConfirmPassword)
    : ICommand<SuperAdminResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record UpdateSuperAdminCommand(
    Guid SuperAdminId,
    string NameAr,
    string? NameEn,
    string Email,
    string? PhoneNumber)
    : ICommand<SuperAdminResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ActivateSuperAdminCommand(Guid SuperAdminId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DeactivateSuperAdminCommand(Guid SuperAdminId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DeleteSuperAdminCommand(Guid SuperAdminId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RestoreSuperAdminCommand(Guid SuperAdminId)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListRolesQuery : IQuery<IReadOnlyList<RoleResponse>>;
public sealed record GetRoleQuery(Guid RoleId) : IQuery<RoleResponse>;
public sealed record ListPermissionsQuery : IQuery<IReadOnlyList<PermissionResponse>>;
public sealed record GetRolePermissionsQuery(Guid RoleId) : IQuery<RolePermissionsResponse>;
public sealed record ReplaceRolePermissionsCommand(Guid RoleId, IReadOnlyList<Guid> PermissionIds)
    : ICommand<RolePermissionsResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RoleResponse(Guid Id, string Name, bool IsSystemRole);
public sealed record PermissionResponse(Guid Id, string Name, bool IsSystemPermission);
public sealed record RolePermissionsResponse(Guid RoleId, IReadOnlyList<PermissionResponse> Permissions);

internal sealed class ListSuperAdminsQueryValidator : AbstractValidator<ListSuperAdminsQuery>
{
    public ListSuperAdminsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.SearchText).MaximumLength(200);
    }
}

internal sealed class CreateSuperAdminCommandValidator : AbstractValidator<CreateSuperAdminCommand>
{
    public CreateSuperAdminCommandValidator()
    {
        RuleFor(command => command.UserName).NotEmpty().WithMessage(ErrorMessage.UserNameRequired).MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().WithMessage(ErrorMessage.EmailRequired).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
        RuleFor(command => command.InitialPassword).NotEmpty().WithMessage(ErrorMessage.PasswordRequired);
        RuleFor(command => command.ConfirmPassword).Equal(command => command.InitialPassword).WithMessage(ErrorMessage.PasswordConfirmationMismatch);
    }
}

internal sealed class UpdateSuperAdminCommandValidator : AbstractValidator<UpdateSuperAdminCommand>
{
    public UpdateSuperAdminCommandValidator()
    {
        RuleFor(command => command.SuperAdminId).NotEmpty();
        RuleFor(command => command.Email).NotEmpty().WithMessage(ErrorMessage.EmailRequired).EmailAddress().WithMessage(ErrorMessage.EmailInvalid).MaximumLength(200);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.NameAr).NotEmpty().WithMessage(ErrorMessage.NameArRequired).MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
    }
}

internal sealed class ReplaceRolePermissionsCommandValidator : AbstractValidator<ReplaceRolePermissionsCommand>
{
    public ReplaceRolePermissionsCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotNull();
        RuleFor(command => command.PermissionIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage(ErrorMessage.DuplicatePermission);
    }
}

internal sealed class ListSuperAdminsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListSuperAdminsQuery, PagedResponse<SuperAdminResponse>>
{
    public async Task<Result<PagedResponse<SuperAdminResponse>>> Handle(
        ListSuperAdminsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await RootGuard.IsRootAsync(dataStore, currentUser, cancellationToken))
        {
            return Result<PagedResponse<SuperAdminResponse>>.Fail(SecurityErrors.RootRequired);
        }

        var (records, total) = await dataStore.ListSuperAdminsAsync(
            request.SearchText,
            request.PageNumber,
            request.PageSize,
            request.IncludeDeleted,
            cancellationToken);
        return Result<PagedResponse<SuperAdminResponse>>.Ok(new PagedResponse<SuperAdminResponse>(
            records.Select(SuperAdminMapper.Map).ToArray(),
            total,
            request.PageNumber,
            request.PageSize));
    }
}

internal sealed class GetSuperAdminQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetSuperAdminQuery, SuperAdminResponse>
{
    public async Task<Result<SuperAdminResponse>> Handle(GetSuperAdminQuery request, CancellationToken cancellationToken)
    {
        if (!await RootGuard.IsRootAsync(dataStore, currentUser, cancellationToken))
        {
            return Result<SuperAdminResponse>.Fail(SecurityErrors.RootRequired);
        }

        var record = await dataStore.FindSuperAdminAsync(request.SuperAdminId, true, cancellationToken);
        return record is null
            ? Result<SuperAdminResponse>.Fail(GovernanceErrors.SuperAdminNotFound)
            : Result<SuperAdminResponse>.Ok(SuperAdminMapper.Map(record));
    }
}

internal sealed class CreateSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    IPasswordService passwordService,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<CreateSuperAdminCommand, SuperAdminResponse>
{
    public async Task<Result<SuperAdminResponse>> Handle(
        CreateSuperAdminCommand request,
        CancellationToken cancellationToken)
    {
        if (!await RootGuard.IsRootAsync(dataStore, currentUser, cancellationToken) || currentUser.UserId is not { } rootUserId)
        {
            return Result<SuperAdminResponse>.Fail(SecurityErrors.RootRequired);
        }

        if (await dataStore.UserNameExistsAsync(request.UserName.Trim(), null, cancellationToken))
        {
            return Result<SuperAdminResponse>.Fail(GovernanceErrors.UserNameAlreadyExists);
        }

        if (await dataStore.EmailExistsAsync(request.Email.Trim().ToLowerInvariant(), null, cancellationToken))
        {
            return Result<SuperAdminResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        }

        if (!passwordService.IsStrongPassword(request.InitialPassword))
        {
            return Result<SuperAdminResponse>.Fail(GovernanceErrors.PasswordInvalid);
        }

        var role = await dataStore.FindRoleByNameAsync(SystemRoleNames.SuperAdmin, cancellationToken);
        if (role is null)
        {
            return Result<SuperAdminResponse>.Fail(GovernanceErrors.RoleNotFound);
        }

        var now = clock.UtcNow;
        var userResult = ApplicationUser.Create(
            Guid.NewGuid(),
            request.UserName,
            request.Email,
            request.PhoneNumber,
            await passwordService.HashAsync(request.InitialPassword, cancellationToken),
            UserType.SuperAdmin,
            isFirstLogin: true,
            now,
            rootUserId);
        if (userResult.IsFailure)
        {
            return Result<SuperAdminResponse>.Fail(userResult.Errors);
        }

        var adminResult = SuperAdmin.Create(
            Guid.NewGuid(),
            userResult.Value.Id,
            request.NameAr,
            request.NameEn,
            isRootSuperAdmin: false,
            rootUserId);
        if (adminResult.IsFailure)
        {
            return Result<SuperAdminResponse>.Fail(adminResult.Errors);
        }

        dataStore.Add(userResult.Value);
        dataStore.Add(adminResult.Value);
        dataStore.Add(new UserRole(Guid.NewGuid(), userResult.Value.Id, role.Id, rootUserId));
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<SuperAdminResponse>.Ok(SuperAdminMapper.Map(new SuperAdminRecord(
            adminResult.Value,
            userResult.Value)));
    }
}

internal sealed class UpdateSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateSuperAdminCommand, SuperAdminResponse>
{
    public async Task<Result<SuperAdminResponse>> Handle(UpdateSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var recordResult = await RootGuard.GetMutableTargetAsync(dataStore, currentUser, request.SuperAdminId, cancellationToken);
        if (recordResult.IsFailure)
        {
            return Result<SuperAdminResponse>.Fail(recordResult.Errors);
        }

        var record = recordResult.Value;
        if (await dataStore.EmailExistsAsync(request.Email.Trim().ToLowerInvariant(), record.User.Id, cancellationToken))
        {
            return Result<SuperAdminResponse>.Fail(GovernanceErrors.EmailAlreadyExists);
        }

        var update = record.SuperAdmin.UpdateNames(request.NameAr, request.NameEn);
        if (update.IsFailure)
        {
            return Result<SuperAdminResponse>.Fail(update.Errors);
        }

        record.User.UpdateContact(request.Email, request.PhoneNumber);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<SuperAdminResponse>.Ok(SuperAdminMapper.Map(record));
    }
}

internal abstract class SuperAdminStateHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
{
    protected async Task<Result> ExecuteAsync(
        Guid superAdminId,
        SuperAdminAction action,
        CancellationToken cancellationToken)
    {
        var recordResult = await RootGuard.GetMutableTargetAsync(dataStore, currentUser, superAdminId, cancellationToken);
        if (recordResult.IsFailure)
        {
            return Result.Fail(recordResult.Errors);
        }

        var record = recordResult.Value;
        var now = clock.UtcNow;
        switch (action)
        {
            case SuperAdminAction.Activate:
                record.User.Activate();
                break;
            case SuperAdminAction.Deactivate:
                record.User.Deactivate();
                break;
            case SuperAdminAction.Delete:
                record.SuperAdmin.IsDeleted = true;
                record.SuperAdmin.DeletedOnUtc = now;
                record.SuperAdmin.RestoredOnUtc = null;
                record.User.Deactivate();
                break;
            case SuperAdminAction.Restore:
                record.SuperAdmin.IsDeleted = false;
                record.SuperAdmin.DeletedOnUtc = null;
                record.SuperAdmin.RestoredOnUtc = now;
                record.User.Activate();
                break;
            default:
                throw new InvalidOperationException("Unsupported SuperAdmin state transition.");
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

internal sealed class ActivateSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : SuperAdminStateHandler(dataStore, currentUser, clock), ICommandHandler<ActivateSuperAdminCommand>
{
    public Task<Result> Handle(ActivateSuperAdminCommand request, CancellationToken cancellationToken)
        => ExecuteAsync(request.SuperAdminId, SuperAdminAction.Activate, cancellationToken);
}

internal sealed class DeactivateSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : SuperAdminStateHandler(dataStore, currentUser, clock), ICommandHandler<DeactivateSuperAdminCommand>
{
    public Task<Result> Handle(DeactivateSuperAdminCommand request, CancellationToken cancellationToken)
        => ExecuteAsync(request.SuperAdminId, SuperAdminAction.Deactivate, cancellationToken);
}

internal sealed class DeleteSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : SuperAdminStateHandler(dataStore, currentUser, clock), ICommandHandler<DeleteSuperAdminCommand>
{
    public Task<Result> Handle(DeleteSuperAdminCommand request, CancellationToken cancellationToken)
        => ExecuteAsync(request.SuperAdminId, SuperAdminAction.Delete, cancellationToken);
}

internal sealed class RestoreSuperAdminCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : SuperAdminStateHandler(dataStore, currentUser, clock), ICommandHandler<RestoreSuperAdminCommand>
{
    public Task<Result> Handle(RestoreSuperAdminCommand request, CancellationToken cancellationToken)
        => ExecuteAsync(request.SuperAdminId, SuperAdminAction.Restore, cancellationToken);
}

internal sealed class ListRolesQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<RoleResponse>>.Ok((await dataStore.ListRolesAsync(cancellationToken))
            .Select(role => new RoleResponse(role.Id, role.Name, role.IsSystemRole)).ToArray());
}

internal sealed class GetRoleQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetRoleQuery, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleQuery request, CancellationToken cancellationToken)
    {
        var role = await dataStore.FindRoleByIdAsync(request.RoleId, cancellationToken);
        return role is null
            ? Result<RoleResponse>.Fail(GovernanceErrors.RoleNotFound)
            : Result<RoleResponse>.Ok(new RoleResponse(role.Id, role.Name, role.IsSystemRole));
    }
}

internal sealed class ListPermissionsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionResponse>>
{
    public async Task<Result<IReadOnlyList<PermissionResponse>>> Handle(ListPermissionsQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<PermissionResponse>>.Ok((await dataStore.ListPermissionsAsync(cancellationToken))
            .Select(permission => new PermissionResponse(permission.Id, permission.Name, permission.IsSystemPermission)).ToArray());
}

internal sealed class GetRolePermissionsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetRolePermissionsQuery, RolePermissionsResponse>
{
    public async Task<Result<RolePermissionsResponse>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var role = await dataStore.FindRoleByIdAsync(request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RolePermissionsResponse>.Fail(GovernanceErrors.RoleNotFound);
        }

        var mappings = await dataStore.ListRolePermissionsAsync(role.Id, cancellationToken);
        var permissions = await dataStore.ListPermissionsByIdsAsync(
            mappings.Select(mapping => mapping.PermissionId).ToArray(),
            cancellationToken);
        return Result<RolePermissionsResponse>.Ok(new RolePermissionsResponse(
            role.Id,
            permissions.Select(permission => new PermissionResponse(
                permission.Id,
                permission.Name,
                permission.IsSystemPermission)).ToArray()));
    }
}

internal sealed class ReplaceRolePermissionsCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<ReplaceRolePermissionsCommand, RolePermissionsResponse>
{
    public async Task<Result<RolePermissionsResponse>> Handle(
        ReplaceRolePermissionsCommand request,
        CancellationToken cancellationToken)
    {
        var role = await dataStore.FindRoleByIdAsync(request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RolePermissionsResponse>.Fail(GovernanceErrors.RoleNotFound);
        }

        var isRoot = await RootGuard.IsRootAsync(dataStore, currentUser, cancellationToken);
        if (string.Equals(role.Name, SystemRoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase) && !isRoot)
        {
            return Result<RolePermissionsResponse>.Fail(SecurityErrors.RootRequired);
        }

        var permissions = await dataStore.ListPermissionsByIdsAsync(request.PermissionIds, cancellationToken);
        if (permissions.Count != request.PermissionIds.Count || permissions.Any(permission => !permission.IsSystemPermission))
        {
            return Result<RolePermissionsResponse>.Fail(GovernanceErrors.PermissionNotFound);
        }

        if (permissions.Any(permission => PermissionNames.RootOnly.Contains(permission.Name)))
        {
            return Result<RolePermissionsResponse>.Fail(GovernanceErrors.RootPermissionNotAssignable);
        }

        var existing = await dataStore.ListRolePermissionsAsync(role.Id, cancellationToken);
        foreach (var mapping in existing)
        {
            dataStore.Remove(mapping);
        }

        foreach (var permission in permissions)
        {
            dataStore.Add(new RolePermission(Guid.NewGuid(), role.Id, permission.Id, currentUser.UserId));
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<RolePermissionsResponse>.Ok(new RolePermissionsResponse(
            role.Id,
            permissions.OrderBy(permission => permission.Name, StringComparer.OrdinalIgnoreCase)
                .Select(permission => new PermissionResponse(permission.Id, permission.Name, permission.IsSystemPermission))
                .ToArray()));
    }
}

internal static class RootGuard
{
    public static async Task<bool> IsRootAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
        => currentUser.IsAuthenticated && currentUser.UserId is { } userId &&
           await dataStore.FindSuperAdminByUserIdAsync(userId, false, cancellationToken) is
               { IsRootSuperAdmin: true, IsDeleted: false };

    public static async Task<Result<SuperAdminRecord>> GetMutableTargetAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (!await IsRootAsync(dataStore, currentUser, cancellationToken))
        {
            return Result<SuperAdminRecord>.Fail(SecurityErrors.RootRequired);
        }

        var target = await dataStore.FindSuperAdminAsync(targetId, true, cancellationToken);
        if (target is null)
        {
            return Result<SuperAdminRecord>.Fail(GovernanceErrors.SuperAdminNotFound);
        }

        return target.SuperAdmin.IsRootSuperAdmin
            ? Result<SuperAdminRecord>.Fail(SecurityErrors.RootProtected)
            : Result<SuperAdminRecord>.Ok(target);
    }
}

internal static class SuperAdminMapper
{
    public static SuperAdminResponse Map(SuperAdminRecord record)
        => new(
            record.SuperAdmin.Id,
            record.User.Id,
            record.User.UserName,
            record.User.Email,
            record.User.PhoneNumber,
            record.SuperAdmin.NameAr,
            record.SuperAdmin.NameEn,
            record.SuperAdmin.IsRootSuperAdmin,
            record.User.IsActive,
            record.SuperAdmin.IsDeleted,
            record.SuperAdmin.CreatedOnUtc);
}

internal static class GovernanceErrors
{
    public static Error SuperAdminNotFound => Error.NotFound("SuperAdmin.NotFound", ErrorMessage.SuperAdminNotFound);
    public static Error UserNameAlreadyExists => Error.Conflict("Identity.UserNameAlreadyExists", ErrorMessage.UserNameAlreadyExists);
    public static Error EmailAlreadyExists => Error.Conflict("Identity.EmailAlreadyExists", ErrorMessage.EmailAlreadyExists);
    public static Error PasswordInvalid => Error.Validation("Auth.PasswordInvalid", ErrorMessage.PasswordInvalid);
    public static Error RoleNotFound => Error.NotFound("Role.NotFound", ErrorMessage.RoleNotFound);
    public static Error PermissionNotFound => Error.NotFound("Permission.NotFound", ErrorMessage.PermissionNotFound);
    public static Error RootPermissionNotAssignable => Error.Security("RolePermission.RootPermissionNotAssignable", ErrorMessage.RootPermissionNotAssignable);
}

internal enum SuperAdminAction
{
    Activate,
    Deactivate,
    Delete,
    Restore
}
