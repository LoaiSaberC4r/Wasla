using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Tickets.CallNext;
using Wasla.Application.Features.Tickets.CancelTicket;
using Wasla.Application.Features.Tickets.CheckInReservation;
using Wasla.Application.Features.Tickets.CompleteTicket;
using Wasla.Application.Features.Tickets.ConfirmNoResponse;
using Wasla.Application.Features.Tickets.CreateWalkIn;
using Wasla.Application.Features.Tickets.ForceCheckInReservation;
using Wasla.Application.Features.Tickets.GetMyActiveTickets;
using Wasla.Application.Features.Tickets.GetMyTicketDetails;
using Wasla.Application.Features.Tickets.GetPracticeQueue;
using Wasla.Application.Features.Tickets.GetTicketDetails;
using Wasla.Application.Features.Tickets.ManualCall;
using Wasla.Application.Features.Tickets.Recall;
using Wasla.Application.Features.Tickets.RestoreNoShow;
using Wasla.Application.Features.Tickets.StartVisit;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/practices/{practiceId:guid}")]
[Authorize(Roles = SystemRoleNames.Doctor + "," + SystemRoleNames.Reception)]
public sealed class PracticeTicketsController(ISender sender) : ControllerBase
{
    [HttpPost("reservations/{reservationId:guid}/check-in")]
    public async Task<IActionResult> CheckIn(
        Guid practiceId,
        Guid reservationId,
        PaidTicketRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CheckInReservationCommand(
            practiceId, reservationId, request.PaidAmount, idempotencyKey ?? string.Empty),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("reservations/{reservationId:guid}/force-check-in")]
    public async Task<IActionResult> ForceCheckIn(
        Guid practiceId,
        Guid reservationId,
        ForceCheckInTicketRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ForceCheckInReservationCommand(
            practiceId, reservationId, request.PaidAmount, request.Reason,
            idempotencyKey ?? string.Empty), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("tickets/walk-in")]
    public async Task<IActionResult> WalkIn(
        Guid practiceId,
        CreateWalkInTicketRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateWalkInCommand(
            practiceId,
            request.PatientId,
            request.SegmentId,
            request.VisitTypeId,
            request.PaidAmount,
            idempotencyKey ?? string.Empty), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("queue")]
    public async Task<IActionResult> Queue(Guid practiceId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPracticeQueueQuery(practiceId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("tickets/{ticketId:guid}")]
    public async Task<IActionResult> Details(
        Guid practiceId,
        Guid ticketId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetTicketDetailsQuery(practiceId, ticketId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("queue/call-next")]
    public async Task<IActionResult> CallNext(
        Guid practiceId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CallNextCommand(
            practiceId, idempotencyKey ?? string.Empty), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/manual-call")]
    public async Task<IActionResult> ManualCall(
        Guid practiceId,
        Guid ticketId,
        ReasonedTicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new ManualCallCommand(
            practiceId, ticketId, request.Reason, request.RowVersion,
            idempotencyKey ?? string.Empty), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/recall")]
    public async Task<IActionResult> Recall(
        Guid practiceId,
        Guid ticketId,
        TicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new RecallTicketCommand(
            practiceId, ticketId, request.RowVersion, idempotencyKey ?? string.Empty),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/confirm-no-response")]
    public async Task<IActionResult> ConfirmNoResponse(
        Guid practiceId,
        Guid ticketId,
        TicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new ConfirmNoResponseCommand(
            practiceId, ticketId, request.RowVersion, idempotencyKey ?? string.Empty),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/restore-no-show")]
    public async Task<IActionResult> RestoreNoShow(
        Guid practiceId,
        Guid ticketId,
        TicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new RestoreNoShowTicketCommand(
            practiceId, ticketId, request.RowVersion, idempotencyKey ?? string.Empty),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/start")]
    public async Task<IActionResult> Start(
        Guid practiceId,
        Guid ticketId,
        TicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new StartVisitCommand(
            practiceId, ticketId, request.RowVersion, idempotencyKey ?? string.Empty),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid practiceId,
        Guid ticketId,
        TicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CompleteTicketCommand(
            practiceId, ticketId, request.RowVersion, idempotencyKey ?? string.Empty),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("tickets/{ticketId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid practiceId,
        Guid ticketId,
        ReasonedTicketMutationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
        => (await sender.Send(new CancelTicketCommand(
            practiceId, ticketId, request.Reason, request.RowVersion,
            idempotencyKey ?? string.Empty), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets")]
[Authorize(Roles = SystemRoleNames.Patient)]
public sealed class MyTicketsController(ISender sender) : ControllerBase
{
    [HttpGet("mine/active")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyActiveTicketsQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("mine/{ticketId:guid}")]
    public async Task<IActionResult> Details(Guid ticketId, CancellationToken cancellationToken)
        => (await sender.Send(new GetMyTicketDetailsQuery(ticketId), cancellationToken))
            .ToIActionResult(cancellationToken);
}

public sealed record PaidTicketRequest(decimal PaidAmount);
public sealed record ForceCheckInTicketRequest(decimal PaidAmount, string Reason);
public sealed record CreateWalkInTicketRequest(
    Guid PatientId,
    Guid SegmentId,
    Guid VisitTypeId,
    decimal PaidAmount);
public sealed record TicketMutationRequest(string RowVersion);
public sealed record ReasonedTicketMutationRequest(string Reason, string RowVersion);
