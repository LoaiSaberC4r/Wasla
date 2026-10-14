using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Operations;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/investigation-cases")]
[Authorize(Roles = SystemRoleNames.SuperAdmin)]
public sealed class InvestigationsController(IInvestigationService service, ISender sender) : ControllerBase
{
    [HttpGet]
    [Permission(Phase18PermissionNames.InvestigationsView)]
    [ProducesResponseType<ReportPage<InvestigationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] InvestigationFilter filter, CancellationToken ct)
        => (await service.ListAsync(filter, ct)).ToIActionResult(ct);
    [HttpGet("filter-options")]
    [Permission(Phase18PermissionNames.InvestigationsView)]
    [ProducesResponseType<InvestigationFilterOptions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterOptions([FromQuery] ReportOptionsFilter filter, CancellationToken ct)
        => (await service.OptionsAsync(ct, filter)).ToIActionResult(ct);
    [HttpGet("{caseId:guid}")]
    [Permission(Phase18PermissionNames.InvestigationsView)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid caseId, CancellationToken ct)
        => (await service.DetailAsync(caseId, ct)).ToIActionResult(ct);
    [HttpGet("{caseId:guid}/history")]
    [Permission(Phase18PermissionNames.InvestigationsView)]
    [ProducesResponseType<ReportPage<InvestigationHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid caseId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await service.HistoryAsync(caseId, pageNumber, pageSize, ct)).ToIActionResult(ct);
    [HttpPost]
    [Permission(Phase18PermissionNames.InvestigationsCreate)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] InvestigationCreateRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(null, InvestigationOperation.Create, key, Create: request), ct)).ToIActionResult(ct);
    [HttpPost("{caseId:guid}/assign")]
    [Permission(Phase18PermissionNames.InvestigationsAssign)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid caseId, [FromBody] InvestigationActionRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(caseId, InvestigationOperation.Assign, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{caseId:guid}/start-review")]
    [Permission(Phase18PermissionNames.InvestigationsStartReview)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartReview(Guid caseId, [FromBody] InvestigationActionRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(caseId, InvestigationOperation.StartReview, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{caseId:guid}/resolve")]
    [Permission(Phase18PermissionNames.InvestigationsResolve)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(Guid caseId, [FromBody] InvestigationActionRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(caseId, InvestigationOperation.Resolve, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{caseId:guid}/close")]
    [Permission(Phase18PermissionNames.InvestigationsClose)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid caseId, [FromBody] InvestigationActionRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(caseId, InvestigationOperation.Close, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{caseId:guid}/reopen")]
    [Permission(Phase18PermissionNames.InvestigationsReopen)]
    [ProducesResponseType<InvestigationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(Guid caseId, [FromBody] InvestigationActionRequest request, [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteInvestigationCommand(caseId, InvestigationOperation.Reopen, key, Action: request), ct)).ToIActionResult(ct);
}
