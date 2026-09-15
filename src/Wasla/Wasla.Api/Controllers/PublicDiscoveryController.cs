using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.PublicDiscovery;

namespace Wasla.Api.Controllers;

/// <summary>
/// Public doctor discovery. PublicSearchPrice is always the active Normal segment plus
/// active NewConsultation visit type price. Availability is limited to the 30-day public
/// horizon and all dates/times use the Doctor Practice's configured local time zone.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public")]
[AllowAnonymous]
public sealed class PublicDiscoveryController(ISender sender) : ControllerBase
{
    [HttpGet("specializations")]
    [ProducesResponseType<IReadOnlyList<PublicSpecializationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Specializations(CancellationToken cancellationToken)
        => (await sender.Send(new ListPublicSpecializationsQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("doctors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Doctors(
        [FromQuery] string? searchText,
        [FromQuery] Guid? specializationId,
        [FromQuery] int? governorateId,
        [FromQuery] int? cityId,
        [FromQuery] int? areaId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new SearchPublicDoctorsQuery(
            searchText,
            specializationId,
            governorateId,
            cityId,
            areaId,
            pageNumber,
            pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("doctors/{doctorId:guid}")]
    [ProducesResponseType<PublicDoctorDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Doctor(Guid doctorId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPublicDoctorDetailsQuery(doctorId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("doctors/{doctorId:guid}/profile-image")]
    [Produces("image/jpeg", "image/png", "image/gif")]
    public async Task<IActionResult> DoctorProfileImage(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicDoctorProfileImageQuery(doctorId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("practices/{practiceId:guid}/logo")]
    [Produces("image/jpeg", "image/png", "image/gif")]
    public async Task<IActionResult> PracticeLogo(
        Guid practiceId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicPracticeLogoQuery(practiceId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    /// <summary>Returns only dates with at least one currently available slot.</summary>
    [HttpGet("practices/{practiceId:guid}/available-dates")]
    [ProducesResponseType<IReadOnlyList<PublicAvailableDateResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableDates(
        Guid practiceId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetPublicAvailableDatesQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    /// <summary>
    /// Returns only currently available slot starts for the Practice-local date. Same-day
    /// slots remain eligible whenever their start is after the current Practice-local time.
    /// </summary>
    [HttpGet("practices/{practiceId:guid}/available-slots")]
    [ProducesResponseType<IReadOnlyList<PublicAvailableSlotResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableSlots(
        Guid practiceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetPublicAvailableSlotsQuery(practiceId, date), cancellationToken))
            .ToIActionResult(cancellationToken);

    /// <summary>
    /// Returns server-priced active VisitType/Segment combinations for a currently available
    /// Practice-local slot. FollowUp is omitted until real clinical eligibility can be validated.
    /// </summary>
    [HttpGet("practices/{practiceId:guid}/booking-options")]
    [ProducesResponseType<PublicBookingOptionsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BookingOptions(
        Guid practiceId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly time,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetPublicBookingOptionsQuery(
            practiceId, date, time), cancellationToken)).ToIActionResult(cancellationToken);
}
