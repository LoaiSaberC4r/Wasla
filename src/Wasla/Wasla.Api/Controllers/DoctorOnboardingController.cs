using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Onboarding;
using Wasla.Domain.Doctors;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/medical-specializations")]
[Authorize]
public sealed class MedicalSpecializationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.SpecializationsView)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isDeleted,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListMedicalSpecializationsQuery(
            search, isActive, isDeleted, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{id:guid}")]
    [Permission(PermissionNames.SpecializationsView)]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => (await sender.Send(new GetMedicalSpecializationQuery(id), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost]
    [Permission(PermissionNames.SpecializationsCreate)]
    public async Task<IActionResult> Create(CreateMedicalSpecializationCommand command, CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("{id:guid}")]
    [Permission(PermissionNames.SpecializationsUpdate)]
    public async Task<IActionResult> Update(
        Guid id,
        MedicalSpecializationUpdateRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateMedicalSpecializationCommand(
            id,
            request.NameAr,
            request.NameEn,
            request.DescriptionAr,
            request.DescriptionEn,
            request.SortOrder,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Permission(PermissionNames.SpecializationsActivate)]
    public Task<IActionResult> Activate(Guid id, RowVersionRequest request, CancellationToken cancellationToken)
        => ChangeState(id, MedicalSpecializationStateChange.Activate, request.RowVersion, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Permission(PermissionNames.SpecializationsDeactivate)]
    public Task<IActionResult> Deactivate(Guid id, RowVersionRequest request, CancellationToken cancellationToken)
        => ChangeState(id, MedicalSpecializationStateChange.Deactivate, request.RowVersion, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Permission(PermissionNames.SpecializationsDelete)]
    public Task<IActionResult> Delete(Guid id, [FromBody] RowVersionRequest request, CancellationToken cancellationToken)
        => ChangeState(id, MedicalSpecializationStateChange.Delete, request.RowVersion, cancellationToken);

    [HttpPost("{id:guid}/restore")]
    [Permission(PermissionNames.SpecializationsRestore)]
    public Task<IActionResult> Restore(Guid id, RowVersionRequest request, CancellationToken cancellationToken)
        => ChangeState(id, MedicalSpecializationStateChange.Restore, request.RowVersion, cancellationToken);

    private async Task<IActionResult> ChangeState(
        Guid id,
        MedicalSpecializationStateChange change,
        string rowVersion,
        CancellationToken cancellationToken)
        => (await sender.Send(new ChangeMedicalSpecializationStateCommand(id, change, rowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize]
public sealed class DoctorSpecializationOnboardingController(ISender sender) : ControllerBase
{
    [HttpGet("specializations/options")]
    [Permission(PermissionNames.DoctorSpecializationsViewOwn)]
    public async Task<IActionResult> Options(CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorSpecializationOptionsQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("specializations")]
    [Permission(PermissionNames.DoctorSpecializationsViewOwn)]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyDoctorSpecializationsQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("specialization-request")]
    [Permission(PermissionNames.DoctorSpecializationsViewOwn)]
    public async Task<IActionResult> GetRequest(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyDoctorSpecializationRequestQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("specialization-request")]
    [Permission(PermissionNames.DoctorSpecializationsSubmitOwn)]
    public async Task<IActionResult> Submit(
        SubmitSpecializationsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new SubmitDoctorSpecializationRequestCommand(request.Specializations), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("specialization-request/resubmit")]
    [Permission(PermissionNames.DoctorSpecializationsResubmitOwn)]
    public async Task<IActionResult> Resubmit(
        ResubmitSpecializationsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ResubmitDoctorSpecializationRequestCommand(
            request.Specializations, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("specialization-request/history")]
    [Permission(PermissionNames.DoctorSpecializationsViewOwn)]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyDoctorSpecializationRequestHistoryQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("practice-location")]
    [Permission(PermissionNames.DoctorPracticeLocationViewOwn)]
    public async Task<IActionResult> PracticeLocation(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyDoctorPracticeLocationQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("practice-location")]
    [Permission(PermissionNames.DoctorPracticeLocationManageOwn)]
    public async Task<IActionResult> UpsertPracticeLocation(
        UpsertMyDoctorPracticeLocationCommand command,
        CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/doctor-specialization-requests")]
[Authorize]
public sealed class DoctorSpecializationRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.DoctorSpecializationRequestsViewAll)]
    public async Task<IActionResult> List(
        [FromQuery] DoctorSpecializationRequestStatus? status,
        [FromQuery] DoctorSpecializationRequestType? type,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListDoctorSpecializationRequestsQuery(
            status, type, search, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    [Permission(PermissionNames.DoctorSpecializationRequestsViewDetails)]
    public async Task<IActionResult> Details(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorSpecializationRequestDetailsQuery(requestId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}/history")]
    [Permission(PermissionNames.DoctorSpecializationRequestsViewDetails)]
    public async Task<IActionResult> History(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorSpecializationRequestHistoryQuery(requestId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("{requestId:guid}/specializations")]
    [Permission(PermissionNames.DoctorSpecializationRequestsAdjust)]
    public async Task<IActionResult> Adjust(
        Guid requestId,
        AdjustSpecializationsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new AdjustDoctorSpecializationRequestCommand(
            requestId, request.Specializations, request.Reason, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{requestId:guid}/approve")]
    [Permission(PermissionNames.DoctorSpecializationRequestsApprove)]
    public async Task<IActionResult> Approve(Guid requestId, RowVersionRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ApproveDoctorSpecializationRequestCommand(requestId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{requestId:guid}/request-modification")]
    [Permission(PermissionNames.DoctorSpecializationRequestsRequestModification)]
    public async Task<IActionResult> RequestModification(
        Guid requestId,
        RequestModificationRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new RequestDoctorSpecializationModificationCommand(
            requestId, request.Message, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{requestId:guid}/reject")]
    [Permission(PermissionNames.DoctorSpecializationRequestsReject)]
    public async Task<IActionResult> Reject(
        Guid requestId,
        RejectSpecializationsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new RejectDoctorSpecializationRequestCommand(
            requestId, request.Reason, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public")]
[AllowAnonymous]
public sealed class EgyptLocationsController(ISender sender) : ControllerBase
{
    [HttpGet("governorates")]
    public async Task<IActionResult> Governorates(CancellationToken cancellationToken)
        => (await sender.Send(new GetGovernoratesQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("governorates/{governorateId:int}/cities")]
    public async Task<IActionResult> Cities(int governorateId, CancellationToken cancellationToken)
        => (await sender.Send(new GetCitiesQuery(governorateId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("cities/{cityId:int}/areas")]
    public async Task<IActionResult> Areas(int cityId, CancellationToken cancellationToken)
        => (await sender.Send(new GetAreasQuery(cityId), cancellationToken)).ToIActionResult(cancellationToken);
}

public sealed record MedicalSpecializationUpdateRequest(
    string NameAr,
    string? NameEn,
    string? DescriptionAr,
    string? DescriptionEn,
    int SortOrder,
    string RowVersion);
public sealed record RowVersionRequest(string RowVersion);
public sealed record SubmitSpecializationsRequest(IReadOnlyList<SpecializationSelectionRequest> Specializations);
public sealed record ResubmitSpecializationsRequest(
    IReadOnlyList<SpecializationSelectionRequest> Specializations,
    string RowVersion);
public sealed record AdjustSpecializationsRequest(
    IReadOnlyList<SpecializationSelectionRequest> Specializations,
    string Reason,
    string RowVersion);
public sealed record RequestModificationRequest(string Message, string RowVersion);
public sealed record RejectSpecializationsRequest(string Reason, string RowVersion);
