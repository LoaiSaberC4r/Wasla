using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Doctors;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/doctors")]
[Authorize]
public sealed class AdminDoctorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.DoctorsViewAll)]
    public async Task<IActionResult> List(
        [FromQuery] DoctorApprovalStatus? approvalStatus,
        [FromQuery] string? searchText,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(
            new ListDoctorsQuery(approvalStatus, searchText, pageNumber, pageSize),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{doctorId:guid}")]
    [Permission(PermissionNames.DoctorsViewDetails)]
    public async Task<IActionResult> Details(Guid doctorId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorDetailsQuery(doctorId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{doctorId:guid}/media/{mediaType}")]
    [Permission(PermissionNames.DoctorsViewDetails)]
    public async Task<IActionResult> Media(
        Guid doctorId,
        DoctorMediaType mediaType,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDoctorMediaQuery(doctorId, mediaType), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("{doctorId:guid}/approve")]
    [Permission(PermissionNames.DoctorsApprove)]
    public async Task<IActionResult> Approve(
        Guid doctorId,
        DoctorApprovalRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new ApproveDoctorCommand(doctorId, request.NationalId, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{doctorId:guid}/reject")]
    [Permission(PermissionNames.DoctorsReject)]
    public async Task<IActionResult> Reject(
        Guid doctorId,
        DoctorReasonRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new RejectDoctorCommand(doctorId, request.Reason, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{doctorId:guid}/suspend")]
    [Permission(PermissionNames.DoctorsSuspend)]
    public async Task<IActionResult> Suspend(
        Guid doctorId,
        DoctorReasonRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new SuspendDoctorCommand(doctorId, request.Reason, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{doctorId:guid}/reactivate")]
    [Permission(PermissionNames.DoctorsReactivate)]
    public async Task<IActionResult> Reactivate(
        Guid doctorId,
        DoctorRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new ReactivateDoctorCommand(doctorId, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize]
public sealed class DoctorSelfController(ISender sender) : ControllerBase
{
    [HttpGet("onboarding")]
    [Permission(PermissionNames.DoctorOnboardingViewOwn)]
    public async Task<IActionResult> Onboarding(CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorOnboardingQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);
}

public sealed record DoctorApprovalRequest(string NationalId, string RowVersion);
public sealed record DoctorReasonRequest(string Reason, string RowVersion);
public sealed record DoctorRowVersionRequest(string RowVersion);

