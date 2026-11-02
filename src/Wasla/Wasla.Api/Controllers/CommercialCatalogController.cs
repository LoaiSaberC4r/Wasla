using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Application.Features.Subscriptions;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/public/commercial/catalog")]
public sealed class CommercialCatalogController(ISender sender) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    [ProducesResponseType<CommercialCatalogResponse>(200)]
    public async Task<IActionResult> Get(CancellationToken ct) => (await sender.Send(new CommercialCatalogQuery(), ct)).ToIActionResult(ct);
}
