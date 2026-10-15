using Asp.Versioning;
using BuildingBlock.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize]
[Route("api/v{version:apiVersion}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("archive-audit")]
[Wasla.Api.Security.ArchiveDeniedRequest]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403),
 ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429), ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(503)]
public sealed class Phase18PartTwoController(IArchiveQueries archives, IArchiveOperations operations, IAuditExplorer audit) : ControllerBase
{
    // Annex C's narrowly-scoped dependent selector avoids a booking/family-management permission prerequisite.
    [HttpGet("reservations/mine/archived/patients/options"), Authorize(Roles = SystemRoleNames.Patient), ProducesResponseType<OptionsPage<ArchivePatientOption>>(200)]
    public async Task<IActionResult> PatientOptions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await archives.PatientsAsync(pageNumber, pageSize, ct)).ToIActionResult(ct);

    // Current operational contexts remain reusable; this supplements historically inactive assigned Practices.
    [HttpGet("reception/archives/practices/options"), Authorize(Roles = SystemRoleNames.Reception), ProducesResponseType<OptionsPage<PracticeOption>>(200)]
    public async Task<IActionResult> ReceptionArchiveOptions([FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await archives.PracticesAsync(ReportActor.Reception, search, pageNumber, pageSize, ct)).ToIActionResult(ct);
    // P18.2-07
    [HttpGet("doctors/me/practices/{practiceId:guid}/archives/tickets"), ProducesResponseType<ArchivePage<ArchivedTicketItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint07(Guid practiceId, [FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.TicketsAsync(ReportActor.Doctor, practiceId, filter, ct)).ToIActionResult(ct);

    // P18.2-08
    [HttpGet("doctors/me/practices/{practiceId:guid}/archives/reservations"), ProducesResponseType<ArchivePage<ArchivedReservationItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint08(Guid practiceId, [FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.ReservationsAsync(ReportActor.Doctor, practiceId, filter, ct)).ToIActionResult(ct);

    // P18.2-09
    [HttpGet("reception/practices/{practiceId:guid}/archives/tickets"), ProducesResponseType<ArchivePage<ArchivedTicketItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Reception)]
    public async Task<IActionResult> Endpoint09(Guid practiceId, [FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.TicketsAsync(ReportActor.Reception, practiceId, filter, ct)).ToIActionResult(ct);

    // P18.2-10
    [HttpGet("reception/practices/{practiceId:guid}/archives/reservations"), ProducesResponseType<ArchivePage<ArchivedReservationItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Reception)]
    public async Task<IActionResult> Endpoint10(Guid practiceId, [FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.ReservationsAsync(ReportActor.Reception, practiceId, filter, ct)).ToIActionResult(ct);

    // P18.2-11
    [HttpGet("tickets/mine/archived"), ProducesResponseType<ArchivePage<ArchivedTicketItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Patient)]
    public async Task<IActionResult> Endpoint11([FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.TicketsAsync(ReportActor.Patient, null, filter, ct)).ToIActionResult(ct);

    // P18.2-12
    [HttpGet("reservations/mine/archived"), ProducesResponseType<ArchivePage<ArchivedReservationItem>>(200)]
    [Authorize(Roles = SystemRoleNames.Patient)]
    public async Task<IActionResult> Endpoint12([FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.ReservationsAsync(ReportActor.Patient, null, filter, ct)).ToIActionResult(ct);

    // P18.2-13
    [HttpGet("admin/archives/tickets"), ProducesResponseType<ArchivePage<PlatformTicketMetadata>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint13([FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.PlatformTicketsAsync(filter, ct)).ToIActionResult(ct);

    // P18.2-14
    [HttpGet("admin/archives/reservations"), ProducesResponseType<ArchivePage<PlatformReservationMetadata>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint14([FromQuery] ArchiveFilter filter, CancellationToken ct)
        => (await archives.PlatformReservationsAsync(filter, ct)).ToIActionResult(ct);

    // P18.2-15
    [HttpGet("admin/archives/tickets/{ticketId:guid}"), ProducesResponseType<PlatformTicketMetadata>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint15(Guid ticketId, CancellationToken ct)
        => (await archives.PlatformTicketAsync(ticketId, ct)).ToIActionResult(ct);

    // P18.2-16
    [HttpGet("admin/archives/operations/health"), ProducesResponseType<ArchiveHealth>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint16(CancellationToken ct)
        => (await operations.HealthAsync(ct)).ToIActionResult(ct);

    // P18.2-17
    [HttpGet("admin/archives/operations/runs"), ProducesResponseType<ArchivePage<ArchiveRunItem>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint17([FromQuery] ArchiveOperationsFilter filter, CancellationToken ct)
        => (await operations.RunsAsync(filter, ct)).ToIActionResult(ct);

    // P18.2-18
    [HttpGet("admin/archives/operations/runs/{runId:guid}"), ProducesResponseType<ArchiveRunItem>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint18(Guid runId, CancellationToken ct)
        => (await operations.RunAsync(runId, ct)).ToIActionResult(ct);

    // P18.2-19
    [HttpGet("admin/archives/operations/exceptions"), ProducesResponseType<ArchivePage<ArchiveExceptionItem>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint19([FromQuery] ArchiveOperationsFilter filter, CancellationToken ct)
        => (await operations.ExceptionsAsync(filter, ct)).ToIActionResult(ct);

    // P18.2-20
    [HttpGet("admin/archives/operations/exceptions/{exceptionId:guid}"), ProducesResponseType<ArchiveExceptionItem>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint20(Guid exceptionId, CancellationToken ct)
        => (await operations.ExceptionAsync(exceptionId, ct)).ToIActionResult(ct);

    // P18.2-21
    [HttpPost("admin/archives/operations/pause"), ProducesResponseType<ArchiveControlResponse>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint21([FromBody] ArchiveControlBody body, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required] string key, CancellationToken ct)
        => (await operations.ControlAsync(true, body, key, ct)).ToIActionResult(ct);

    // P18.2-22
    [HttpPost("admin/archives/operations/resume"), ProducesResponseType<ArchiveControlResponse>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint22([FromBody] ArchiveControlBody body, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required] string key, CancellationToken ct)
        => (await operations.ControlAsync(false, body, key, ct)).ToIActionResult(ct);

    // P18.2-23
    [HttpGet("doctors/me/practices/{practiceId:guid}/audit/events"), ProducesResponseType<ArchivePage<SafeAuditEvent>>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint23(Guid practiceId, [FromQuery] AuditFilter filter, CancellationToken ct)
        => (await audit.SearchAsync(ReportActor.Doctor, practiceId, filter, false, null, ct)).ToIActionResult(ct);

    // P18.2-25
    [HttpGet("doctors/me/practices/{practiceId:guid}/audit/sensitive-access"), ProducesResponseType<ArchivePage<SafeAuditEvent>>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint25(Guid practiceId, [FromQuery] AuditFilter filter, CancellationToken ct)
        => (await audit.SearchAsync(ReportActor.Doctor, practiceId, filter, true, null, ct)).ToIActionResult(ct);

    // P18.2-26
    [HttpGet("reception/practices/{practiceId:guid}/audit/my-activity"), ProducesResponseType<ArchivePage<SafeAuditEvent>>(200)]
    [Authorize(Roles = SystemRoleNames.Reception)]
    public async Task<IActionResult> Endpoint26(Guid practiceId, [FromQuery] AuditFilter filter, CancellationToken ct)
        => (await audit.SearchAsync(ReportActor.Reception, practiceId, filter, false, null, ct)).ToIActionResult(ct);

    // P18.2-28
    [HttpGet("admin/audit/events"), ProducesResponseType<ArchivePage<SafeAuditEvent>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint28([FromQuery] AuditFilter filter, CancellationToken ct)
        => (await audit.SearchAsync(ReportActor.SuperAdmin, null, filter, false, null, ct)).ToIActionResult(ct);

    // P18.2-24
    [HttpGet("doctors/me/practices/{practiceId:guid}/audit/events/{eventKey}"), ProducesResponseType<SafeAuditEvent>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint24(Guid practiceId, string eventKey, CancellationToken ct)
        => (await audit.DetailAsync(ReportActor.Doctor, practiceId, eventKey, ct)).ToIActionResult(ct);

    // P18.2-27
    [HttpGet("reception/practices/{practiceId:guid}/audit/my-activity/{eventKey}"), ProducesResponseType<SafeAuditEvent>(200)]
    [Authorize(Roles = SystemRoleNames.Reception)]
    public async Task<IActionResult> Endpoint27(Guid practiceId, string eventKey, CancellationToken ct)
        => (await audit.DetailAsync(ReportActor.Reception, practiceId, eventKey, ct)).ToIActionResult(ct);

    // P18.2-29
    [HttpGet("admin/audit/events/{eventKey}"), ProducesResponseType<SafeAuditEvent>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint29(string eventKey, CancellationToken ct)
        => (await audit.DetailAsync(ReportActor.SuperAdmin, null, eventKey, ct)).ToIActionResult(ct);

    // P18.2-30
    [HttpPost("admin/audit/sensitive-access/search"), ProducesResponseType<ArchivePage<SafeAuditEvent>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint30([FromBody] RestrictedAuditSearch body, CancellationToken ct)
        => (await audit.SearchAsync(ReportActor.SuperAdmin, body.Filters.PracticeId, body.Filters, true, body.Reason, ct)).ToIActionResult(ct);

    // P18.2-31
    [HttpGet("admin/archives/reservations/{reservationId:guid}"), ProducesResponseType<PlatformReservationMetadata>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint31(Guid reservationId, CancellationToken ct)
        => (await archives.PlatformReservationAsync(reservationId, ct)).ToIActionResult(ct);

    // P18.2-32: static vocabulary only; no private selectors or worker permissions.
    [HttpGet("archives/metadata"), ProducesResponseType<ArchiveMetadata>(200)]
    public IActionResult Endpoint32() => Ok(new ArchiveMetadata(
        Enum.GetNames<TicketStatus>().Select(x => new ArchiveOption(x, ArchiveLabels.Arabic(x), ArchiveLabels.English(x))).ToArray(),
        Enum.GetNames<ReservationStatus>().Select(x => new ArchiveOption(x, ArchiveLabels.Arabic(x), ArchiveLabels.English(x))).ToArray(),
        [new("Ticket", "تذكرة", "Ticket"), new("Reservation", "حجز", "Reservation")],
        ["fromBusinessDate", "toBusinessDate", "status", "reference", "ticketNumber", "patientId", "practiceId", "search", "cursor", "pageSize"],
        "Original Practice-local businessDate; UTC archivedAtUtc"));

    // P18.2-33
    [HttpGet("doctors/me/archives/practices/options"), ProducesResponseType<OptionsPage<PracticeOption>>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint33([FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await archives.PracticesAsync(ReportActor.Doctor, search, pageNumber, pageSize, ct)).ToIActionResult(ct);

    // P18.2-34
    [HttpGet("admin/archives/practices/options"), ProducesResponseType<OptionsPage<PracticeOption>>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint34([FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await archives.PracticesAsync(ReportActor.SuperAdmin, search, pageNumber, pageSize, ct)).ToIActionResult(ct);

    // P18.2-35
    [HttpGet("admin/archives/operations/filter-options"), ProducesResponseType<ArchiveOperationsOptions>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint35(CancellationToken ct)
        => (await operations.OptionsAsync(ct)).ToIActionResult(ct);

    // P18.2-36
    [HttpGet("doctors/me/practices/{practiceId:guid}/audit/filter-options"), ProducesResponseType<AuditOptions>(200)]
    [Authorize(Roles = SystemRoleNames.Doctor)]
    public async Task<IActionResult> Endpoint36(Guid practiceId, [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await audit.OptionsAsync(ReportActor.Doctor, practiceId, search, pageNumber, pageSize, ct)).ToIActionResult(ct);

    // P18.2-37
    [HttpGet("reception/practices/{practiceId:guid}/audit/filter-options"), ProducesResponseType<AuditOptions>(200)]
    [Authorize(Roles = SystemRoleNames.Reception)]
    public async Task<IActionResult> Endpoint37(Guid practiceId, [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await audit.OptionsAsync(ReportActor.Reception, practiceId, search, pageNumber, pageSize, ct)).ToIActionResult(ct);

    // P18.2-38
    [HttpGet("admin/audit/filter-options"), ProducesResponseType<AuditOptions>(200)]
    [Authorize(Roles = SystemRoleNames.SuperAdmin)]
    public async Task<IActionResult> Endpoint38([FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await audit.OptionsAsync(ReportActor.SuperAdmin, null, search, pageNumber, pageSize, ct)).ToIActionResult(ct);

}
