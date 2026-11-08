using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Diagnostics;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Security;
namespace Wasla.Api.Controllers;
[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}")]
public sealed class DependentDiagnosticsController(ISender sender) : ControllerBase
{
    [HttpPost("lab-requests/{requestId:guid}/submissions"), Permission(DependentDiagnosticPermissionNames.LabCreate), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> LabSubmit(Guid childPatientId, Guid requestId, [FromForm] DependentDiagnosticForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Submit(childPatientId, requestId, DiagnosticKind.Lab, form, key, ct);
    [HttpPost("radiology-requests/{requestId:guid}/submissions"), Permission(DependentDiagnosticPermissionNames.RadiologyCreate), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> RadiologySubmit(Guid childPatientId, Guid requestId, [FromForm] DependentDiagnosticForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Submit(childPatientId, requestId, DiagnosticKind.Radiology, form, key, ct);
    private async Task<IActionResult> Submit(Guid child, Guid request, DiagnosticKind kind, DependentDiagnosticForm form, string key, CancellationToken ct)
    {
        if (form.Attachments.Count is < 1 or > 5 || form.Attachments.Any(f => f.Length is <= 0 or > 10485760) || Request.Form.Keys.Any(k => k.Contains("covered", StringComparison.OrdinalIgnoreCase)))
            return new[] { DiagnosticErrors.Validation("DependentDiagnostic.Invalid") }.ToActionProblem(ct);
        return (await sender.Send(new DependentDiagnosticMutation(child, kind, request, false, key, Attachments: await DiagnosticForms.ReadAsync(form.Attachments, [], ct),
            ExternalProviderName: form.ExternalProviderName, ExternalReportDate: form.ExternalReportDate, PatientNote: form.PatientNote), ct)).ToIActionResult(ct);
    }
    [HttpGet("lab-requests/{requestId:guid}/submissions"), Permission(DependentDiagnosticPermissionNames.LabView)]
    public async Task<IActionResult> LabList(Guid childPatientId, Guid requestId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new DependentDiagnosticSubmissionsQuery(childPatientId, DiagnosticKind.Lab, requestId, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("radiology-requests/{requestId:guid}/submissions"), Permission(DependentDiagnosticPermissionNames.RadiologyView)]
    public async Task<IActionResult> RadiologyList(Guid childPatientId, Guid requestId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new DependentDiagnosticSubmissionsQuery(childPatientId, DiagnosticKind.Radiology, requestId, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpPost("lab-requests/{requestId:guid}/submissions/{submissionId:guid}/withdraw"), Permission(DependentDiagnosticPermissionNames.LabWithdraw)]
    public async Task<IActionResult> LabWithdraw(Guid childPatientId, Guid requestId, Guid submissionId, [FromBody] DependentVersionForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new DependentDiagnosticMutation(childPatientId, DiagnosticKind.Lab, requestId, true, key, submissionId, form.RowVersion), ct)).ToIActionResult(ct);
    [HttpPost("radiology-requests/{requestId:guid}/submissions/{submissionId:guid}/withdraw"), Permission(DependentDiagnosticPermissionNames.RadiologyWithdraw)]
    public async Task<IActionResult> RadiologyWithdraw(Guid childPatientId, Guid requestId, Guid submissionId, [FromBody] DependentVersionForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new DependentDiagnosticMutation(childPatientId, DiagnosticKind.Radiology, requestId, true, key, submissionId, form.RowVersion), ct)).ToIActionResult(ct);
}
public sealed class DependentDiagnosticForm
{
    public List<IFormFile> Attachments { get; set; } = [];
    public string? ExternalProviderName { get; set; }
    public DateOnly? ExternalReportDate { get; set; }
    public string? PatientNote { get; set; }
}
