using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Patients;
using Wasla.Domain.Security;
namespace Wasla.Api.Controllers;
[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}")]
public sealed class DependentProfileController(ISender sender) : ControllerBase
{
    [HttpGet("contacts"), Permission(FamilySelfServicePermissionNames.Profile)]
    public async Task<IActionResult> Contacts(Guid childPatientId, CancellationToken ct)
    {
        var result = await sender.Send(new GetDependentProfileQuery(childPatientId), ct);
        return result.IsSuccess ? Ok(new { result.Value.Contacts, result.Value.RowVersion }) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("profile-image"), Permission(FamilySelfServicePermissionNames.Profile)]
    public async Task<IActionResult> Image(Guid childPatientId, CancellationToken ct)
    {
        var result = await sender.Send(new GetDependentProfileImageQuery(childPatientId), ct); Response.Headers.XContentTypeOptions = "nosniff";
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("contacts"), Permission(FamilyExtensionPermissionNames.ManageContacts)]
    public Task<IActionResult> Add(Guid childPatientId, [FromBody] DependentContactForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Change(new(childPatientId, "add", form.RowVersion, key, Contact: form.Contact), ct);
    [HttpPut("contacts/{contactId:guid}"), Permission(FamilyExtensionPermissionNames.ManageContacts)]
    public Task<IActionResult> Update(Guid childPatientId, Guid contactId, [FromBody] DependentContactForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Change(new(childPatientId, "update", form.RowVersion, key, contactId, form.Contact), ct);
    [HttpDelete("contacts/{contactId:guid}"), Permission(FamilyExtensionPermissionNames.ManageContacts)]
    public Task<IActionResult> Delete(Guid childPatientId, Guid contactId, [FromBody] DependentVersionForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Change(new(childPatientId, "delete", form.RowVersion, key, contactId), ct);
    [HttpPatch("profile"), Permission(FamilyExtensionPermissionNames.ManagePhoto), Consumes("multipart/form-data"), RequestSizeLimit(10500000)]
    public async Task<IActionResult> Photo(Guid childPatientId, [FromForm] DependentPhotoForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        await using var stream = form.Photo.OpenReadStream();
        return await Change(new(childPatientId, "photo", form.RowVersion, key, Photo: new MediaUpload(stream, form.Photo.FileName, form.Photo.ContentType, form.Photo.Length)), ct);
    }
    private async Task<IActionResult> Change(ManageDependentProfileCommand command, CancellationToken ct) => (await sender.Send(command, ct)).ToIActionResult(ct);
}
public sealed record DependentContactForm(string RowVersion, PatientContactInput Contact);
public sealed record DependentVersionForm(string RowVersion);
public sealed class DependentPhotoForm
{
    public string RowVersion { get; set; } = "";
    public IFormFile Photo { get; set; } = null!;
}
