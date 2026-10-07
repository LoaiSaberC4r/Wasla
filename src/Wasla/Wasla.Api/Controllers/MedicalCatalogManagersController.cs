using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Governance;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.SuperAdmin)]
[Route("api/v{version:apiVersion}/admin/medical-catalog-managers")]
[ProducesResponseType<MedicalCatalogManagerResponse>(StatusCodes.Status200OK)]
public sealed class MedicalCatalogManagersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClinicalPage<MedicalCatalogManagerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListMedicalCatalogManagersQuery(search, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => (await sender.Send(new GetMedicalCatalogManagerQuery(id), ct)).ToIActionResult(ct);
    [HttpPost]
    [ProducesResponseType<MedicalCatalogManagerResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateMedicalCatalogManagerCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ManagerContactRequest request, CancellationToken ct)
        => (await sender.Send(new UpdateMedicalCatalogManagerCommand(id, request.Email, request.PhoneNumber), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => (await sender.Send(new SetMedicalCatalogManagerActiveCommand(id, true), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => (await sender.Send(new SetMedicalCatalogManagerActiveCommand(id, false), ct)).ToIActionResult(ct);
}
