using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Reservations;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reservations")]
[Authorize(Roles = SystemRoleNames.Patient)]
public sealed class ReservationsController(ISender sender) : ControllerBase
{
    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata(CancellationToken cancellationToken)
        => (await sender.Send(new GetReservationMetadataQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("bookable-patients")]
    public async Task<IActionResult> BookablePatients(CancellationToken cancellationToken)
        => (await sender.Send(new ListBookablePatientsQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateReservationCommand(
            request.PatientId,
            request.DoctorPracticeId,
            request.BusinessDate,
            request.SlotStartTime,
            request.SegmentId,
            request.VisitTypeId,
            request.BookingNote,
            idempotencyKey), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetMine), new { reservationId = result.Value.ReservationId, version = "1.0" }, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] Guid? patientId,
        [FromQuery] string view = "Upcoming",
        [FromQuery] ReservationStatus? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListMineReservationsQuery(
            patientId, view, status, fromDate, toDate, pageNumber, pageSize), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("mine/{reservationId:guid}")]
    public async Task<IActionResult> GetMine(Guid reservationId, CancellationToken cancellationToken)
        => (await sender.Send(new GetMineReservationQuery(reservationId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("mine/{reservationId:guid}/cancel")]
    public async Task<IActionResult> CancelMine(
        Guid reservationId,
        CancelReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CancelMineReservationCommand(
            reservationId,
            request.ReasonCode,
            request.Comment,
            request.RowVersion,
            idempotencyKey), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("mine/{reservationId:guid}/reschedule/available-dates")]
    public async Task<IActionResult> MineRescheduleDates(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var detail = await sender.Send(new GetMineReservationQuery(reservationId), cancellationToken);
        return detail.IsFailure
            ? detail.Errors.ToActionProblem(cancellationToken)
            : (await sender.Send(new GetBookingAvailableDatesQuery(
                detail.Value.Practice.Id,
                reservationId,
                ReservationAvailabilityChannel.Patient), cancellationToken)).ToIActionResult(cancellationToken);
    }

    [HttpGet("mine/{reservationId:guid}/reschedule/available-slots")]
    public async Task<IActionResult> MineRescheduleSlots(
        Guid reservationId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var detail = await sender.Send(new GetMineReservationQuery(reservationId), cancellationToken);
        return detail.IsFailure
            ? detail.Errors.ToActionProblem(cancellationToken)
            : (await sender.Send(new GetBookingAvailableSlotsQuery(
                detail.Value.Practice.Id,
                date,
                reservationId,
                ReservationAvailabilityChannel.Patient), cancellationToken)).ToIActionResult(cancellationToken);
    }

    [HttpPost("mine/{reservationId:guid}/reschedule")]
    public async Task<IActionResult> RescheduleMine(
        Guid reservationId,
        RescheduleReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new RescheduleMineReservationCommand(
            reservationId,
            request.BusinessDate,
            request.SlotStartTime,
            request.RowVersion,
            idempotencyKey), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}")]
[Authorize(Roles = SystemRoleNames.Reception)]
public sealed class ReceptionReservationsController(ISender sender) : ControllerBase
{
    [HttpGet("booking/available-dates")]
    public async Task<IActionResult> AvailableDates(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableDatesQuery(
            practiceId, null, ReservationAvailabilityChannel.Reception), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("booking/available-slots")]
    public async Task<IActionResult> AvailableSlots(
        Guid practiceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableSlotsQuery(
            practiceId, date, null, ReservationAvailabilityChannel.Reception), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("booking/options")]
    public async Task<IActionResult> BookingOptions(
        Guid practiceId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly time,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetReservationBookingOptionsQuery(
            practiceId, date, time, ReservationAvailabilityChannel.Reception), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("reservations")]
    public async Task<IActionResult> Create(
        Guid practiceId,
        CreatePracticeReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreatePracticeReservationCommand(
            practiceId,
            request.PatientId,
            request.BusinessDate,
            request.SlotStartTime,
            request.SegmentId,
            request.VisitTypeId,
            request.BookingNote,
            idempotencyKey), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("reservations")]
    public async Task<IActionResult> List(
        Guid practiceId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] ReservationStatus? status,
        [FromQuery] Guid? segmentId,
        [FromQuery] ReservationBookingSource? bookingSource,
        [FromQuery] bool? isLate,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListPracticeReservationsQuery(
            practiceId, fromDate, toDate, status, segmentId, bookingSource, isLate,
            search, pageNumber, pageSize, false), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("reservations/filter-options")]
    public async Task<IActionResult> FilterOptions(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetReservationFilterOptionsQuery(practiceId, false), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("reservations/{reservationId:guid}")]
    public async Task<IActionResult> Get(
        Guid practiceId,
        Guid reservationId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetPracticeReservationQuery(
            practiceId, reservationId, false), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("reservations/{reservationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid practiceId,
        Guid reservationId,
        CancelReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CancelPracticeReservationCommand(
            practiceId, reservationId, request.ReasonCode, request.Comment,
            request.RowVersion, idempotencyKey, false), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("reservations/{reservationId:guid}/reschedule/available-dates")]
    public async Task<IActionResult> RescheduleDates(
        Guid practiceId,
        Guid reservationId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableDatesQuery(
            practiceId, reservationId, ReservationAvailabilityChannel.Reception), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("reservations/{reservationId:guid}/reschedule/available-slots")]
    public async Task<IActionResult> RescheduleSlots(
        Guid practiceId,
        Guid reservationId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableSlotsQuery(
            practiceId, date, reservationId, ReservationAvailabilityChannel.Reception), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("reservations/{reservationId:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(
        Guid practiceId,
        Guid reservationId,
        ProviderRescheduleReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new ReschedulePracticeReservationCommand(
            practiceId, reservationId, request.BusinessDate, request.SlotStartTime,
            request.PatientConsentConfirmed, request.Reason, request.RowVersion,
            idempotencyKey, false), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("reservations/{reservationId:guid}/restore-no-show")]
    public async Task<IActionResult> RestoreNoShow(
        Guid practiceId,
        Guid reservationId,
        ReservationRowVersionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new RestorePracticeNoShowReservationCommand(
            practiceId, reservationId, request.RowVersion, idempotencyKey), cancellationToken))
            .ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/reservations")]
[Authorize]
public sealed class DoctorReservationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]
    public async Task<IActionResult> List(
        Guid practiceId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] ReservationStatus? status,
        [FromQuery] Guid? segmentId,
        [FromQuery] ReservationBookingSource? bookingSource,
        [FromQuery] bool? isLate,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListPracticeReservationsQuery(
            practiceId, fromDate, toDate, status, segmentId, bookingSource, isLate,
            search, pageNumber, pageSize, true), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("filter-options")]
    [Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]
    public async Task<IActionResult> FilterOptions(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetReservationFilterOptionsQuery(practiceId, true), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{reservationId:guid}")]
    [Permission(PermissionNames.DoctorPracticeReservationsViewOwn)]
    public async Task<IActionResult> Get(Guid practiceId, Guid reservationId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPracticeReservationQuery(
            practiceId, reservationId, true), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{reservationId:guid}/cancel")]
    [Permission(PermissionNames.DoctorPracticeReservationsCancelOwn)]
    public async Task<IActionResult> Cancel(
        Guid practiceId,
        Guid reservationId,
        CancelReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CancelPracticeReservationCommand(
            practiceId, reservationId, request.ReasonCode, request.Comment,
            request.RowVersion, idempotencyKey, true), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{reservationId:guid}/reschedule/available-dates")]
    [Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]
    public async Task<IActionResult> RescheduleDates(
        Guid practiceId,
        Guid reservationId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableDatesQuery(
            practiceId, reservationId, ReservationAvailabilityChannel.Doctor), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{reservationId:guid}/reschedule/available-slots")]
    [Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]
    public async Task<IActionResult> RescheduleSlots(
        Guid practiceId,
        Guid reservationId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetBookingAvailableSlotsQuery(
            practiceId, date, reservationId, ReservationAvailabilityChannel.Doctor), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{reservationId:guid}/reschedule")]
    [Permission(PermissionNames.DoctorPracticeReservationsRescheduleOwn)]
    public async Task<IActionResult> Reschedule(
        Guid practiceId,
        Guid reservationId,
        ProviderRescheduleReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new ReschedulePracticeReservationCommand(
            practiceId, reservationId, request.BusinessDate, request.SlotStartTime,
            request.PatientConsentConfirmed, request.Reason, request.RowVersion,
            idempotencyKey, true), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/reservations")]
[Authorize]
[Permission(PermissionNames.ReservationsViewAdministrative)]
public sealed class AdministrativeReservationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] Guid? doctorId,
        [FromQuery] Guid? practiceId,
        [FromQuery] ReservationStatus? status,
        [FromQuery] ReservationBookingSource? bookingSource,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new ListAdministrativeReservationsQuery(
            search, doctorId, practiceId, status, bookingSource, fromDate, toDate,
            pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{reservationId:guid}")]
    public async Task<IActionResult> Get(Guid reservationId, CancellationToken cancellationToken)
        => (await sender.Send(new GetAdministrativeReservationQuery(reservationId), cancellationToken))
            .ToIActionResult(cancellationToken);
}

public sealed record CreateReservationRequest(
    Guid PatientId,
    Guid DoctorPracticeId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    Guid SegmentId,
    Guid VisitTypeId,
    string? BookingNote);
public sealed record CreatePracticeReservationRequest(
    Guid PatientId,
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    Guid SegmentId,
    Guid VisitTypeId,
    string? BookingNote);
public sealed record CancelReservationRequest(string ReasonCode, string? Comment, string RowVersion);
public sealed record RescheduleReservationRequest(
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    string RowVersion);
public sealed record ProviderRescheduleReservationRequest(
    DateOnly BusinessDate,
    TimeOnly SlotStartTime,
    bool PatientConsentConfirmed,
    string Reason,
    string RowVersion);
public sealed record ReservationRowVersionRequest(string RowVersion);
