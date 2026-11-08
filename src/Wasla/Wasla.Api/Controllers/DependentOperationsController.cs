using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}")]
public sealed class DependentOperationsController(ISender sender) : ControllerBase
{
    [HttpGet("profile"), Permission(FamilySelfServicePermissionNames.Profile)]
    public async Task<IActionResult> Profile(Guid childPatientId, CancellationToken ct)
        => (await sender.Send(new GetDependentProfileQuery(childPatientId), ct)).ToIActionResult(ct);
}
[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/tickets/dependents/{childPatientId:guid}")]
public sealed class DependentTicketsController(ISender sender) : ControllerBase
{
    [HttpGet("active"), Permission(FamilySelfServicePermissionNames.Tickets)]
    public async Task<IActionResult> Active(Guid childPatientId, CancellationToken ct)
        => (await sender.Send(new GetDependentActiveTicketsQuery(childPatientId), ct)).ToIActionResult(ct);
    [HttpGet("{ticketId:guid}"), Permission(FamilySelfServicePermissionNames.Tickets)]
    public async Task<IActionResult> Details(Guid childPatientId, Guid ticketId, CancellationToken ct)
        => (await sender.Send(new GetDependentTicketQuery(childPatientId, ticketId), ct)).ToIActionResult(ct);
}
