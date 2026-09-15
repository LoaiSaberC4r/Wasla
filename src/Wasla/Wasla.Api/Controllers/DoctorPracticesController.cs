using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Practices;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me/practices")]
[Authorize]
public sealed class DoctorPracticesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.DoctorPracticesViewOwn)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => (await sender.Send(new ListMyDoctorPracticesQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}")]
    [Permission(PermissionNames.DoctorPracticesViewOwn)]
    public async Task<IActionResult> Get(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetMyDoctorPracticeQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost]
    [Permission(PermissionNames.DoctorPracticesManageOwn)]
    public async Task<IActionResult> Create(DoctorPracticeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorPracticeCommand(
            request.NameAr,
            request.NameEn,
            request.GovernorateId,
            request.CityId,
            request.AreaId,
            request.DetailedAddress,
            request.Latitude,
            request.Longitude), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { practiceId = result.Value.Id, version = "1.0" }, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}")]
    [Permission(PermissionNames.DoctorPracticesManageOwn)]
    public async Task<IActionResult> Update(
        Guid practiceId,
        UpdateDoctorPracticeRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeCommand(
            practiceId,
            request.NameAr,
            request.NameEn,
            request.GovernorateId,
            request.CityId,
            request.AreaId,
            request.DetailedAddress,
            request.Latitude,
            request.Longitude,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/activate")]
    [Permission(PermissionNames.DoctorPracticesActivateOwn)]
    public async Task<IActionResult> Activate(
        Guid practiceId,
        DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new ActivateDoctorPracticeCommand(practiceId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/deactivate")]
    [Permission(PermissionNames.DoctorPracticesActivateOwn)]
    public async Task<IActionResult> Deactivate(
        Guid practiceId,
        DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new DeactivateDoctorPracticeCommand(practiceId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/configuration")]
    [Permission(PermissionNames.DoctorPracticeConfigurationViewOwn)]
    public async Task<IActionResult> Configuration(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorPracticeConfigurationQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("{practiceId:guid}/configuration")]
    [Permission(PermissionNames.DoctorPracticeConfigurationManageOwn)]
    public async Task<IActionResult> UpdateConfiguration(
        Guid practiceId,
        DoctorPracticeConfigurationRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeConfigurationCommand(
            practiceId,
            request.AllowOnlineBooking,
            request.AllowWalkIn,
            request.DefaultSlotDurationMinutes,
            request.CheckInGracePeriodMinutes,
            request.PatientSelfCancellationCutoffMinutes,
            request.MaximumDailyPatients,
            request.MaximumTicketCallAttempts,
            request.TimeZoneId,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/branding")]
    [Permission(PermissionNames.DoctorPracticeBrandingViewOwn)]
    public async Task<IActionResult> Branding(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorPracticeBrandingQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/branding/logo")]
    [Permission(PermissionNames.DoctorPracticeBrandingViewOwn)]
    public async Task<IActionResult> Logo(Guid practiceId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDoctorPracticeLogoQuery(practiceId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}/branding")]
    [Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]
    public async Task<IActionResult> UpdateBranding(
        Guid practiceId,
        DoctorPracticeBrandingRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeBrandingCommand(
            practiceId,
            request.PrimaryColor,
            request.SecondaryColor,
            request.BackgroundColor,
            request.TextColor,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/branding/logo")]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]
    public async Task<IActionResult> ReplaceLogo(
        Guid practiceId,
        [FromForm] DoctorPracticeLogoRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ReplaceDoctorPracticeLogoCommand(
            practiceId,
            new MediaUpload(request.Logo.OpenReadStream(), request.Logo.FileName, request.Logo.ContentType,
                request.Logo.Length),
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpDelete("{practiceId:guid}/branding/logo")]
    [Permission(PermissionNames.DoctorPracticeBrandingManageOwn)]
    public async Task<IActionResult> RemoveLogo(
        Guid practiceId,
        [FromBody] DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new RemoveDoctorPracticeLogoCommand(practiceId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/schedule")]
    [Permission(PermissionNames.DoctorPracticeScheduleViewOwn)]
    public async Task<IActionResult> Schedule(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new ListDoctorPracticeScheduleQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/schedule/effective")]
    [Permission(PermissionNames.DoctorPracticeScheduleViewOwn)]
    public async Task<IActionResult> EffectiveSchedule(
        Guid practiceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetEffectiveDoctorPracticeScheduleQuery(practiceId, date), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/schedule/periods")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> AddPeriod(
        Guid practiceId,
        DoctorPracticeSchedulePeriodRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorPracticeSchedulePeriodCommand(
            practiceId, request.DayOfWeek, request.StartTime, request.EndTime, request.SlotDurationMinutes),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}/schedule/periods/{periodId:guid}")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> UpdatePeriod(
        Guid practiceId,
        Guid periodId,
        UpdateDoctorPracticeSchedulePeriodRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeSchedulePeriodCommand(
            practiceId, periodId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.SlotDurationMinutes, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpDelete("{practiceId:guid}/schedule/periods/{periodId:guid}")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> DeletePeriod(
        Guid practiceId,
        Guid periodId,
        [FromBody] DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new DeleteDoctorPracticeSchedulePeriodCommand(
            practiceId, periodId, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/schedule/exceptions")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> AddException(
        Guid practiceId,
        DoctorPracticeScheduleExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorPracticeScheduleExceptionCommand(
            practiceId, request.Date, request.Type, request.StartTime, request.EndTime,
            request.SlotDurationMinutes), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}/schedule/exceptions/{exceptionId:guid}")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> UpdateException(
        Guid practiceId,
        Guid exceptionId,
        UpdateDoctorPracticeScheduleExceptionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeScheduleExceptionCommand(
            practiceId, exceptionId, request.Date, request.Type, request.StartTime, request.EndTime,
            request.SlotDurationMinutes, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpDelete("{practiceId:guid}/schedule/exceptions/{exceptionId:guid}")]
    [Permission(PermissionNames.DoctorPracticeScheduleManageOwn)]
    public async Task<IActionResult> DeleteException(
        Guid practiceId,
        Guid exceptionId,
        [FromBody] DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new DeleteDoctorPracticeScheduleExceptionCommand(
            practiceId, exceptionId, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/segments")]
    [Permission(PermissionNames.DoctorPracticeSegmentsViewOwn)]
    public async Task<IActionResult> Segments(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new ListDoctorPracticeSegmentsQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/segments")]
    [Permission(PermissionNames.DoctorPracticeSegmentsManageOwn)]
    public async Task<IActionResult> AddSegment(
        Guid practiceId,
        DoctorPracticeSegmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorPracticeSegmentCommand(
            practiceId, request.NameAr, request.NameEn, request.Priority,
            request.ReservedDailyQuota, request.QuotaReleaseBeforeMinutes), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}/segments/{segmentId:guid}")]
    [Permission(PermissionNames.DoctorPracticeSegmentsManageOwn)]
    public async Task<IActionResult> UpdateSegment(
        Guid practiceId,
        Guid segmentId,
        UpdateDoctorPracticeSegmentRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeSegmentCommand(
            practiceId, segmentId, request.NameAr, request.NameEn, request.Priority,
            request.ReservedDailyQuota, request.QuotaReleaseBeforeMinutes, request.IsActive,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/visit-types")]
    [Permission(PermissionNames.DoctorPracticePricingViewOwn)]
    public async Task<IActionResult> VisitTypes(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new ListDoctorPracticeVisitTypesQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("{practiceId:guid}/visit-types/{visitTypeId:guid}")]
    [Permission(PermissionNames.DoctorPracticePricingManageOwn)]
    public async Task<IActionResult> UpdateVisitType(
        Guid practiceId,
        Guid visitTypeId,
        DoctorPracticeVisitTypeRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticeVisitTypeCommand(
            practiceId, visitTypeId, request.NameAr, request.NameEn, request.IsActive,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{practiceId:guid}/prices")]
    [Permission(PermissionNames.DoctorPracticePricingViewOwn)]
    public async Task<IActionResult> Prices(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new ListDoctorPracticePricesQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{practiceId:guid}/prices")]
    [Permission(PermissionNames.DoctorPracticePricingManageOwn)]
    public async Task<IActionResult> AddPrice(
        Guid practiceId,
        DoctorPracticePriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateDoctorPracticePriceCommand(
            practiceId, request.SegmentId, request.VisitTypeId, request.Price), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("{practiceId:guid}/prices/{priceId:guid}")]
    [Permission(PermissionNames.DoctorPracticePricingManageOwn)]
    public async Task<IActionResult> UpdatePrice(
        Guid practiceId,
        Guid priceId,
        UpdateDoctorPracticePriceRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateDoctorPracticePriceCommand(
            practiceId, priceId, request.Price, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpDelete("{practiceId:guid}/prices/{priceId:guid}")]
    [Permission(PermissionNames.DoctorPracticePricingManageOwn)]
    public async Task<IActionResult> DeletePrice(
        Guid practiceId,
        Guid priceId,
        [FromBody] DoctorPracticeRowVersionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new DeleteDoctorPracticePriceCommand(
            practiceId, priceId, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}

public record DoctorPracticeRequest(
    string NameAr,
    string? NameEn,
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude);
public sealed record UpdateDoctorPracticeRequest(
    string NameAr,
    string? NameEn,
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    decimal Latitude,
    decimal Longitude,
    string RowVersion) : DoctorPracticeRequest(
        NameAr, NameEn, GovernorateId, CityId, AreaId, DetailedAddress, Latitude, Longitude);
public sealed record DoctorPracticeRowVersionRequest(string RowVersion);
public sealed record DoctorPracticeConfigurationRequest(
    bool AllowOnlineBooking,
    bool AllowWalkIn,
    int DefaultSlotDurationMinutes,
    int CheckInGracePeriodMinutes,
    int PatientSelfCancellationCutoffMinutes,
    int? MaximumDailyPatients,
    int MaximumTicketCallAttempts,
    string TimeZoneId,
    string RowVersion);
public sealed record DoctorPracticeBrandingRequest(
    string? PrimaryColor,
    string? SecondaryColor,
    string? BackgroundColor,
    string? TextColor,
    string RowVersion);
public sealed class DoctorPracticeLogoRequest
{
    public required IFormFile Logo { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
public record DoctorPracticeSchedulePeriodRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes);
public sealed record UpdateDoctorPracticeSchedulePeriodRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDurationMinutes,
    string RowVersion) : DoctorPracticeSchedulePeriodRequest(DayOfWeek, StartTime, EndTime, SlotDurationMinutes);
public record DoctorPracticeScheduleExceptionRequest(
    DateOnly Date,
    DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int? SlotDurationMinutes);
public sealed record UpdateDoctorPracticeScheduleExceptionRequest(
    DateOnly Date,
    DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    int? SlotDurationMinutes,
    string RowVersion) : DoctorPracticeScheduleExceptionRequest(
        Date, Type, StartTime, EndTime, SlotDurationMinutes);
public record DoctorPracticeSegmentRequest(
    string NameAr,
    string? NameEn,
    int Priority,
    int? ReservedDailyQuota,
    int? QuotaReleaseBeforeMinutes);
public sealed record UpdateDoctorPracticeSegmentRequest(
    string NameAr,
    string? NameEn,
    int Priority,
    int? ReservedDailyQuota,
    int? QuotaReleaseBeforeMinutes,
    bool IsActive,
    string RowVersion) : DoctorPracticeSegmentRequest(
        NameAr, NameEn, Priority, ReservedDailyQuota, QuotaReleaseBeforeMinutes);
public sealed record DoctorPracticeVisitTypeRequest(
    string NameAr,
    string? NameEn,
    bool IsActive,
    string RowVersion);
public sealed record DoctorPracticePriceRequest(Guid SegmentId, Guid VisitTypeId, decimal Price);
public sealed record UpdateDoctorPracticePriceRequest(decimal Price, string RowVersion);
