using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Reservations;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}")]
public sealed class DoctorEncountersController(ISender sender) : ControllerBase
{
    [HttpGet("encounters")]
    public async Task<IActionResult> List(Guid practiceId, [FromQuery] Guid? patientId = null,
        [FromQuery] EncounterStatus? status = null, [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new ListDoctorEncountersQuery(practiceId, patientId, status, fromDate, toDate, search, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpGet("encounters/{encounterId:guid}")]
    [ProducesResponseType<EncounterDetailsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Details(Guid practiceId, Guid encounterId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorEncounterQuery(practiceId, encounterId), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpGet("tickets/{ticketId:guid}/encounter")]
    [ProducesResponseType<EncounterDetailsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ByTicket(Guid practiceId, Guid ticketId, CancellationToken cancellationToken)
        => (await sender.Send(new GetDoctorEncounterQuery(practiceId, ticketId, true), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpPatch("encounters/{encounterId:guid}/clinical-notes")]
    public async Task<IActionResult> Notes(Guid practiceId, Guid encounterId, ClinicalNotesRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpdateClinicalNotesCommand(practiceId, encounterId, request.ClinicalNotes, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpPost("encounters/{encounterId:guid}/diagnoses")]
    public async Task<IActionResult> AddDiagnosis(Guid practiceId, Guid encounterId, DiagnosisRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ManageDiagnosisCommand(practiceId, encounterId, DiagnosisMutation.Add, null,
            request.Type, request.DisplayText, request.Notes, request.EncounterRowVersion), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }
    [HttpPut("encounters/{encounterId:guid}/diagnoses/{diagnosisId:guid}")]
    public async Task<IActionResult> UpdateDiagnosis(Guid practiceId, Guid encounterId, Guid diagnosisId, DiagnosisRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ManageDiagnosisCommand(practiceId, encounterId, DiagnosisMutation.Update, diagnosisId,
            request.Type, request.DisplayText, request.Notes, request.EncounterRowVersion), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpDelete("encounters/{encounterId:guid}/diagnoses/{diagnosisId:guid}")]
    public async Task<IActionResult> RemoveDiagnosis(Guid practiceId, Guid encounterId, Guid diagnosisId, EncounterRowVersionRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ManageDiagnosisCommand(practiceId, encounterId, DiagnosisMutation.Remove, diagnosisId,
            null, null, null, request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpPost("encounters/{encounterId:guid}/amendments")]
    public async Task<IActionResult> Amend(Guid practiceId, Guid encounterId, EncounterAmendmentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateEncounterAmendmentCommand(practiceId, encounterId,
            request.Reason, request.EncounterRowVersion, request.Changes), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }
    [HttpGet("encounters/{encounterId:guid}/amendments")]
    public async Task<IActionResult> Amendments(Guid practiceId, Guid encounterId, CancellationToken cancellationToken)
        => (await sender.Send(new ListEncounterAmendmentsQuery(practiceId, encounterId), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpPost("encounters/{encounterId:guid}/follow-up-eligibility")]
    public async Task<IActionResult> FollowUp(Guid practiceId, Guid encounterId, FollowUpEligibilityRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateFollowUpEligibilityCommand(practiceId, encounterId, request.ValidUntil), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/encounters/mine")]
public sealed class MyEncountersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new ListMyEncountersQuery(pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpGet("{encounterId:guid}")]
    public async Task<IActionResult> Details(Guid encounterId, CancellationToken cancellationToken)
        => (await sender.Send(new GetMyEncounterQuery(encounterId), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/follow-up-eligibilities/mine")]
public sealed class MyFollowUpEligibilitiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? patientId = null, [FromQuery] FollowUpEligibilityStatus? status = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => (await sender.Send(new ListMyFollowUpEligibilitiesQuery(patientId, status, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);
    [HttpGet("{eligibilityId:guid}")]
    public async Task<IActionResult> Details(Guid eligibilityId, CancellationToken cancellationToken)
        => (await sender.Send(new GetMyFollowUpEligibilityQuery(eligibilityId), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = SystemRoleNames.Reception)]
[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}/patients/{patientId:guid}/follow-up-eligibilities")]
public sealed class ReceptionFollowUpEligibilitiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid practiceId, Guid patientId, CancellationToken cancellationToken)
        => (await sender.Send(new ListReceptionFollowUpEligibilitiesQuery(practiceId, patientId), cancellationToken)).ToIActionResult(cancellationToken);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/reservations/follow-up-eligibilities/{eligibilityId:guid}")]
public sealed class FollowUpBookingController(ISender sender) : ControllerBase
{
    [HttpGet("available-dates")]
    public async Task<IActionResult> Dates(Guid eligibilityId, CancellationToken cancellationToken)
    {
        var eligibility = await sender.Send(new GetMyFollowUpEligibilityQuery(eligibilityId), cancellationToken);
        return eligibility.IsFailure ? eligibility.Errors.ToActionProblem(cancellationToken) :
            (await sender.Send(new GetBookingAvailableDatesQuery(eligibility.Value.Practice.Id, null,
                ReservationAvailabilityChannel.Patient, eligibility.Value.Patient.Id, eligibilityId), cancellationToken)).ToIActionResult(cancellationToken);
    }
    [HttpGet("available-slots")]
    public async Task<IActionResult> Slots(Guid eligibilityId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var eligibility = await sender.Send(new GetMyFollowUpEligibilityQuery(eligibilityId), cancellationToken);
        return eligibility.IsFailure ? eligibility.Errors.ToActionProblem(cancellationToken) :
            (await sender.Send(new GetBookingAvailableSlotsQuery(eligibility.Value.Practice.Id, date, null,
                ReservationAvailabilityChannel.Patient, eligibility.Value.Patient.Id, eligibilityId), cancellationToken)).ToIActionResult(cancellationToken);
    }
    [HttpGet("booking-options")]
    public async Task<IActionResult> Options(Guid eligibilityId, [FromQuery] DateOnly date, [FromQuery] TimeOnly time, CancellationToken cancellationToken)
    {
        var eligibility = await sender.Send(new GetMyFollowUpEligibilityQuery(eligibilityId), cancellationToken);
        return eligibility.IsFailure ? eligibility.Errors.ToActionProblem(cancellationToken) :
            (await sender.Send(new GetReservationBookingOptionsQuery(eligibility.Value.Practice.Id, date, time,
                ReservationAvailabilityChannel.Patient, eligibility.Value.Patient.Id, eligibilityId), cancellationToken)).ToIActionResult(cancellationToken);
    }
}

public sealed record ClinicalNotesRequest(string? ClinicalNotes, string RowVersion);
public sealed record DiagnosisRequest(DiagnosisType Type, string DisplayText, string? Notes, string EncounterRowVersion);
public sealed record EncounterRowVersionRequest(string RowVersion);
public sealed record EncounterAmendmentRequest(string Reason, string EncounterRowVersion, IReadOnlyList<EncounterCorrection> Changes);
public sealed record FollowUpEligibilityRequest(DateOnly ValidUntil);
