using Wasla.Api.OpenApi;
using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Api.Security;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Api.Controllers;
[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/admin/patient-duplicates"), Authorize]
[EnableRateLimiting("patient-identity")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientDuplicatesAdminController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(PermissionNames.PatientDuplicatesView)]
    [ProducesResponseType<PatientIdentityPage<PatientDuplicateListItem>>(200)]
    public async Task<IActionResult> List([FromQuery] PatientDuplicateStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new PatientIdentityQuery(PatientIdentityRead.Duplicates, new() { DuplicateStatus = status, PageNumber = pageNumber, PageSize = pageSize }), ct)).ToIActionResult(ct);
    [HttpGet("{id:guid}"), Permission(PermissionNames.PatientDuplicatesView)]
    [ProducesResponseType<PatientDuplicateDetailsResponse>(200)]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => (await sender.Send(new PatientIdentityQuery(PatientIdentityRead.DuplicateDetails, new(), Id: id), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/resolve"), Permission(PermissionNames.PatientDuplicatesResolve)]
    [ProducesResponseType<PatientDuplicateResolvedResponse>(200)]
    public async Task<IActionResult> Resolve(Guid id, ResolvePatientDuplicateRequest p, CancellationToken ct) => (await sender.Send(new PatientIdentityCommand(PatientIdentityMutation.ResolveDuplicate,
        new() { Resolution = p.Resolution, Reason = p.Reason, RowVersion = p.RowVersion }, Request.Headers["Idempotency-Key"].ToString(), Id: id), ct)).ToIActionResult(ct);
}
public sealed record ResolvePatientDuplicateRequest(PatientDuplicateStatus Resolution, string Reason, string RowVersion);
