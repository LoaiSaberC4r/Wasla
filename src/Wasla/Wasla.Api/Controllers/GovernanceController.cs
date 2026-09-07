using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Governance;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/superadmins")]
[Authorize]
public sealed class SuperAdminsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.SuperAdminsViewAll)]
    public async Task<IActionResult> List(
        [FromQuery] string? searchText,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => (await sender.Send(
            new ListSuperAdminsQuery(searchText, pageNumber, pageSize, includeDeleted),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{superAdminId:guid}")]
    [Permission(PermissionNames.SuperAdminsViewDetails)]
    public async Task<IActionResult> Details(Guid superAdminId, CancellationToken cancellationToken)
        => (await sender.Send(new GetSuperAdminQuery(superAdminId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost]
    [Permission(PermissionNames.SuperAdminsCreate)]
    public async Task<IActionResult> Create(CreateSuperAdminCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{superAdminId:guid}")]
    [Permission(PermissionNames.SuperAdminsUpdate)]
    public async Task<IActionResult> Update(
        Guid superAdminId,
        UpdateSuperAdminRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateSuperAdminCommand(
            superAdminId,
            request.NameAr,
            request.NameEn,
            request.Email,
            request.PhoneNumber), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{superAdminId:guid}/activate")]
    [Permission(PermissionNames.SuperAdminsActivate)]
    public async Task<IActionResult> Activate(Guid superAdminId, CancellationToken cancellationToken)
        => (await sender.Send(new ActivateSuperAdminCommand(superAdminId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{superAdminId:guid}/deactivate")]
    [Permission(PermissionNames.SuperAdminsDeactivate)]
    public async Task<IActionResult> Deactivate(Guid superAdminId, CancellationToken cancellationToken)
        => (await sender.Send(new DeactivateSuperAdminCommand(superAdminId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpDelete("{superAdminId:guid}")]
    [Permission(PermissionNames.SuperAdminsDelete)]
    public async Task<IActionResult> Delete(Guid superAdminId, CancellationToken cancellationToken)
        => (await sender.Send(new DeleteSuperAdminCommand(superAdminId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{superAdminId:guid}/restore")]
    [Permission(PermissionNames.SuperAdminsRestore)]
    public async Task<IActionResult> Restore(Guid superAdminId, CancellationToken cancellationToken)
        => (await sender.Send(new RestoreSuperAdminCommand(superAdminId), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize]
public sealed class SecurityGovernanceController(ISender sender) : ControllerBase
{
    [HttpGet("roles")]
    [Permission(PermissionNames.RolesView)]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
        => (await sender.Send(new ListRolesQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("roles/{roleId:guid}")]
    [Permission(PermissionNames.RolesView)]
    public async Task<IActionResult> Role(Guid roleId, CancellationToken cancellationToken)
        => (await sender.Send(new GetRoleQuery(roleId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("permissions")]
    [Permission(PermissionNames.PermissionsView)]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken)
        => (await sender.Send(new ListPermissionsQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("roles/{roleId:guid}/permissions")]
    [Permission(PermissionNames.RolesView)]
    public async Task<IActionResult> RolePermissions(Guid roleId, CancellationToken cancellationToken)
        => (await sender.Send(new GetRolePermissionsQuery(roleId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("roles/{roleId:guid}/permissions")]
    [Permission(PermissionNames.RolePermissionsManage)]
    public async Task<IActionResult> ReplaceRolePermissions(
        Guid roleId,
        ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new ReplaceRolePermissionsCommand(roleId, request.PermissionIds),
            cancellationToken)).ToIActionResult(cancellationToken);
}

public sealed record UpdateSuperAdminRequest(
    string NameAr,
    string? NameEn,
    string Email,
    string? PhoneNumber);

public sealed record ReplaceRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
