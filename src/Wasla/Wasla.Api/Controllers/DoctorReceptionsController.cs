using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Practices;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me/receptions")]
[Authorize]
public sealed class DoctorReceptionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.ReceptionUsersViewOwn)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => (await sender.Send(new ListDoctorReceptionsQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{receptionId:guid}")]
    [Permission(PermissionNames.ReceptionUsersViewOwn)]
    public async Task<IActionResult> Get(Guid receptionId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorReceptionQuery(receptionId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost]
    [Permission(PermissionNames.ReceptionUsersManageOwn)]
    public async Task<IActionResult> Create(
        CreateDoctorReceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorReceptionCommand(
            request.UserName,
            request.Email,
            request.PhoneNumber,
            request.TemporaryPassword,
            request.NameAr,
            request.NameEn), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { receptionId = result.Value.Id, version = "1.0" }, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("{receptionId:guid}/assignments")]
    [Permission(PermissionNames.ReceptionAssignmentsManageOwn)]
    public async Task<IActionResult> Assign(
        Guid receptionId,
        ReceptionAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateReceptionAssignmentCommand(
            receptionId, request.DoctorPracticeId, request.PermissionIds), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{receptionId:guid}/assignments/{assignmentId:guid}")]
    [Permission(PermissionNames.ReceptionAssignmentsManageOwn)]
    public async Task<IActionResult> UpdateAssignment(
        Guid receptionId,
        Guid assignmentId,
        UpdateReceptionAssignmentRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateReceptionAssignmentCommand(
            receptionId, assignmentId, request.PermissionIds, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{receptionId:guid}/assignments/{assignmentId:guid}/activate")]
    [Permission(PermissionNames.ReceptionAssignmentsManageOwn)]
    public async Task<IActionResult> ActivateAssignment(
        Guid receptionId,
        Guid assignmentId,
        DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ActivateReceptionAssignmentCommand(
            receptionId, assignmentId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{receptionId:guid}/assignments/{assignmentId:guid}/deactivate")]
    [Permission(PermissionNames.ReceptionAssignmentsManageOwn)]
    public async Task<IActionResult> DeactivateAssignment(
        Guid receptionId,
        Guid assignmentId,
        DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new DeactivateReceptionAssignmentCommand(
            receptionId, assignmentId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);
}

public sealed record CreateDoctorReceptionRequest(
    string UserName,
    string Email,
    string? PhoneNumber,
    string? TemporaryPassword,
    string NameAr,
    string? NameEn);
public sealed record ReceptionAssignmentRequest(
    Guid DoctorPracticeId,
    IReadOnlyCollection<Guid> PermissionIds);
public sealed record UpdateReceptionAssignmentRequest(
    IReadOnlyCollection<Guid> PermissionIds,
    string RowVersion);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reception/practices")]
[Authorize(Roles = SystemRoleNames.Reception)]
public sealed class ReceptionPracticesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => (await sender.Send(new ListMyReceptionPracticesQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);
}
