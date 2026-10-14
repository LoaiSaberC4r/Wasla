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
[Route("api/v{version:apiVersion}/reception/practices/{practiceId:guid}/handovers")]
[Authorize(Roles = SystemRoleNames.Reception)]
public sealed class ReceptionHandoversController(IHandoverService service, ISender sender) : ControllerBase
{
    [HttpGet()]
    [Permission(Phase18PermissionNames.HandoverView)]
    [ProducesResponseType<ReportPage<HandoverResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid practiceId, [FromQuery] HandoverFilter filter, CancellationToken ct)
        => (await service.ListAsync(ReportActor.Reception, filter with { PracticeId = practiceId }, ct)).ToIActionResult(ct);
    [HttpGet("{handoverId:guid}")]
    [Permission(Phase18PermissionNames.HandoverView)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid practiceId, Guid handoverId, CancellationToken ct)
        => (await service.DetailAsync(ReportActor.Reception, practiceId, handoverId, ct)).ToIActionResult(ct);
    [HttpGet("{handoverId:guid}/history")]
    [Permission(Phase18PermissionNames.HandoverView)]
    [ProducesResponseType<ReportPage<HandoverHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid practiceId, Guid handoverId, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await service.HistoryAsync(ReportActor.Reception, practiceId, handoverId, pageNumber, pageSize, ct)).ToIActionResult(ct);
    [HttpPost()]
    [Permission(Phase18PermissionNames.HandoverCreate)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid practiceId, [FromBody] HandoverCreateRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Reception, practiceId, null, HandoverOperation.Create, key, Create: request), ct)).ToIActionResult(ct);
    [HttpPost("{handoverId:guid}/acknowledge")]
    [Permission(Phase18PermissionNames.HandoverAcknowledge)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(Guid practiceId, Guid handoverId, [FromBody] HandoverActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Reception, practiceId, handoverId, HandoverOperation.Acknowledge, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{handoverId:guid}/close")]
    [Permission(Phase18PermissionNames.HandoverClose)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid practiceId, Guid handoverId, [FromBody] HandoverActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Reception, practiceId, handoverId, HandoverOperation.Close, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("{handoverId:guid}/annotations")]
    [Permission(Phase18PermissionNames.HandoverAnnotate)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Annotate(Guid practiceId, Guid handoverId, [FromBody] HandoverActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Reception, practiceId, handoverId, HandoverOperation.Annotate, key, Action: request), ct)).ToIActionResult(ct);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/doctors/me")]
[Authorize(Roles = SystemRoleNames.Doctor)]
public sealed class DoctorHandoversController(IHandoverService service, ISender sender) : ControllerBase
{
    [HttpGet("handovers")]
    [Permission(Phase18PermissionNames.DoctorHandoversView)]
    [ProducesResponseType<ReportPage<HandoverResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] HandoverFilter filter, CancellationToken ct)
        => (await service.ListAsync(ReportActor.Doctor, filter, ct)).ToIActionResult(ct);
    [HttpGet("practices/{practiceId:guid}/handovers/{handoverId:guid}")]
    [Permission(Phase18PermissionNames.DoctorHandoversView)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid practiceId, Guid handoverId, CancellationToken ct)
        => (await service.DetailAsync(ReportActor.Doctor, practiceId, handoverId, ct)).ToIActionResult(ct);
    [HttpGet("practices/{practiceId:guid}/handovers/{handoverId:guid}/history")]
    [Permission(Phase18PermissionNames.DoctorHandoversView)]
    [ProducesResponseType<ReportPage<HandoverHistoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid practiceId, Guid handoverId, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await service.HistoryAsync(ReportActor.Doctor, practiceId, handoverId, pageNumber, pageSize, ct)).ToIActionResult(ct);
    [HttpPost("practices/{practiceId:guid}/handovers")]
    [Permission(Phase18PermissionNames.DoctorHandoversManage)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid practiceId, [FromBody] HandoverCreateRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Doctor, practiceId, null, HandoverOperation.Create, key, Create: request), ct)).ToIActionResult(ct);
    [HttpPost("practices/{practiceId:guid}/handovers/{handoverId:guid}/acknowledge")]
    [Permission(Phase18PermissionNames.DoctorHandoversManage)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(Guid practiceId, Guid handoverId, [FromBody] HandoverActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Doctor, practiceId, handoverId, HandoverOperation.Acknowledge, key, Action: request), ct)).ToIActionResult(ct);
    [HttpPost("practices/{practiceId:guid}/handovers/{handoverId:guid}/close")]
    [Permission(Phase18PermissionNames.DoctorHandoversManage)]
    [ProducesResponseType<HandoverResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(Guid practiceId, Guid handoverId, [FromBody] HandoverActionRequest request,
        [FromHeader(Name = "Idempotency-Key"), System.ComponentModel.DataAnnotations.Required, Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] string key, CancellationToken ct)
        => (await sender.Send(new WriteHandoverCommand(ReportActor.Doctor, practiceId, handoverId, HandoverOperation.Close, key, Action: request), ct)).ToIActionResult(ct);
}
