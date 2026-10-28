using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.DoctorPatients;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("medical-record")]
[Route("api/v{version:apiVersion}/doctors/me/patients")]
public sealed class DoctorPatientsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DoctorPatientsPageResponse>(200)]
    public async Task<IActionResult> Directory([FromQuery] DoctorPatientsQuery query, CancellationToken ct)
        => (await sender.Send(query, ct)).ToIActionResult(ct);
    [HttpGet("{patientId:guid}")]
    [ProducesResponseType<DoctorMyPatientOverviewResponse>(200)]
    public async Task<IActionResult> Overview(Guid patientId, CancellationToken ct)
        => (await sender.Send(new DoctorMyPatientQuery(patientId), ct)).ToIActionResult(ct);
    [HttpGet("{patientId:guid}/attendances")]
    [ProducesResponseType<ClinicalPage<DoctorPatientAttendance>>(200)]
    public Task<IActionResult> Attendances(Guid patientId, [FromQuery] Guid? practiceId = null, [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => History(patientId, DoctorPatientHistory.Attendances, practiceId, status, fromDate, toDate, pageNumber, pageSize, ct);
    [HttpGet("{patientId:guid}/diagnoses")]
    [ProducesResponseType<ClinicalPage<DoctorPatientDiagnosis>>(200)]
    public Task<IActionResult> Diagnoses(Guid patientId, [FromQuery] Guid? practiceId = null,
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => History(patientId, DoctorPatientHistory.Diagnoses, practiceId, null, fromDate, toDate, pageNumber, pageSize, ct);
    [HttpGet("{patientId:guid}/prescriptions")]
    [ProducesResponseType<ClinicalPage<DoctorPatientPrescription>>(200)]
    public Task<IActionResult> Prescriptions(Guid patientId, [FromQuery] Guid? practiceId = null, [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => History(patientId, DoctorPatientHistory.Prescriptions, practiceId, status, fromDate, toDate, pageNumber, pageSize, ct);
    [HttpGet("{patientId:guid}/appointments")]
    [ProducesResponseType<ClinicalPage<DoctorPatientAppointment>>(200)]
    public Task<IActionResult> Appointments(Guid patientId, [FromQuery] Guid? practiceId = null, [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => History(patientId, DoctorPatientHistory.Appointments, practiceId, status, fromDate, toDate, pageNumber, pageSize, ct);
    [HttpGet("{patientId:guid}/follow-ups")]
    [ProducesResponseType<ClinicalPage<DoctorPatientFollowUp>>(200)]
    public Task<IActionResult> FollowUps(Guid patientId, [FromQuery] Guid? practiceId = null, [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => History(patientId, DoctorPatientHistory.FollowUps, practiceId, status, null, null, pageNumber, pageSize, ct);
    private async Task<IActionResult> History(Guid patientId, DoctorPatientHistory kind, Guid? practiceId, string? status,
        DateOnly? fromDate, DateOnly? toDate, int page, int size, CancellationToken ct)
        => (await sender.Send(new DoctorPatientHistoryQuery(patientId, kind, practiceId, status, fromDate, toDate, page, size), ct)).ToIActionResult(ct);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/doctors/me/prescriptions")]
public sealed class DoctorPrescriptionPdfController(ISender sender) : ControllerBase
{
    [HttpGet("{prescriptionId:guid}/versions/{versionNumber:int}/pdf")]
    [EnableRateLimiting("reports-export")]
    [Produces("application/pdf", "application/problem+json")]
    [ProducesResponseType<FileContentResult>(200)]
    public async Task<IActionResult> Pdf(Guid prescriptionId, int versionNumber, [FromQuery] string disposition = "inline", CancellationToken ct = default)
    {
        var result = await sender.Send(new PrescriptionPdfQuery(prescriptionId, versionNumber, disposition), ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        var pdf = result.Value;
        Response.Headers.ContentDisposition = $"{pdf.Disposition}; filename=\"{pdf.FileName}\"";
        return File(pdf.Bytes, "application/pdf");
    }
}
