using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Clinical;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, EnableRateLimiting("medical-record")]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(400)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(401)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(403)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(404)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(409)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(422)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(429)]
[ProducesResponseType<Microsoft.AspNetCore.Mvc.ProblemDetails>(500)]
[Route("api/v{version:apiVersion}/families/mine/medical-record-dependents")]
public sealed class MedicalRecordDependentsController(ISender sender) : ControllerBase
{
    /// <summary>P16-24: Current eligible dependents under configured approved policy and guardian sharing gate.</summary>
    [HttpGet, ProducesResponseType<RecordPage<Wasla.Application.Features.Families.FamilyMedicalDependent>>(200)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await sender.Send(new ReadMedicalRecordQuery(MedicalRecordRead.Dependents, MedicalRecordMode.Dependent), ct)).ToIActionResult(ct);
}
