using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Patients;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/patients")]
[Authorize]
public sealed class PatientsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.PatientsRegister)]
    public async Task<IActionResult> Create([FromForm] CreateReceptionPatientRequest request, CancellationToken cancellationToken)
    {
        PatientContactInput? contact = request.PrimaryContactNameAr is null && request.PrimaryContactPhoneNumber is null
            ? null
            : new(request.PrimaryContactNameAr ?? string.Empty, request.PrimaryContactNameEn,
                request.PrimaryContactPhoneNumber ?? string.Empty, request.PrimaryContactRelationshipType,
                request.PrimaryContactLinkedPatientId, request.PrimaryContactIsPrimary);
        var result = await sender.Send(new CreateReceptionPatientCommand(
            request.NameAr, request.NameEn, request.DateOfBirth, request.Gender, request.PhoneNumber,
            request.Email, ToUpload(request.ProfileImage), contact), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpGet("search")]
    [Permission(PermissionNames.PatientsSearchBasic)]
    public async Task<IActionResult> Search([FromQuery] string? phoneNumber, [FromQuery] string? name,
        [FromQuery] DateOnly? dateOfBirth, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new SearchPatientsQuery(phoneNumber, name, dateOfBirth, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("me")]
    [Permission(PermissionNames.PatientProfileViewOwn)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyPatientProfileQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("me/profile-image")]
    [Permission(PermissionNames.PatientProfileViewOwn)]
    public async Task<IActionResult> ProfileImage(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyPatientProfileImageQuery(), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("me")]
    [Consumes("multipart/form-data")]
    [Permission(PermissionNames.PatientProfileUpdateOwn)]
    public async Task<IActionResult> UpdateMe([FromForm] UpdateMyPatientProfileRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpdateMyPatientProfileCommand(
            request.NameAr, request.NameEn, request.PhoneNumber, request.Email, ToUpload(request.ProfileImage), request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("me/contacts")]
    [Permission(PermissionNames.PatientContactsViewOwn)]
    public async Task<IActionResult> Contacts(CancellationToken cancellationToken)
        => (await sender.Send(new GetMyPatientContactsQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("me/contacts")]
    [Permission(PermissionNames.PatientContactsManageOwn)]
    public async Task<IActionResult> AddContact(PatientContactRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddMyPatientContactCommand(ToInput(request)), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPut("me/contacts/{contactId:guid}")]
    [Permission(PermissionNames.PatientContactsManageOwn)]
    public async Task<IActionResult> UpdateContact(Guid contactId, PatientContactRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpdateMyPatientContactCommand(contactId, ToInput(request)), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpDelete("me/contacts/{contactId:guid}")]
    [Permission(PermissionNames.PatientContactsManageOwn)]
    public async Task<IActionResult> DeleteContact(Guid contactId, CancellationToken cancellationToken)
        => (await sender.Send(new DeactivateMyPatientContactCommand(contactId), cancellationToken)).ToIActionResult(cancellationToken);

    private static PatientContactInput ToInput(PatientContactRequest request)
        => new(request.NameAr, request.NameEn, request.PhoneNumber, request.RelationshipType, request.LinkedPatientId, request.IsPrimary);
    private static MediaUpload? ToUpload(IFormFile? file)
        => file is null ? null : new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}

public sealed class CreateReceptionPatientRequest
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public IFormFile? ProfileImage { get; set; }
    public string? PrimaryContactNameAr { get; set; }
    public string? PrimaryContactNameEn { get; set; }
    public string? PrimaryContactPhoneNumber { get; set; }
    public PatientContactRelationshipType PrimaryContactRelationshipType { get; set; }
    public Guid? PrimaryContactLinkedPatientId { get; set; }
    public bool PrimaryContactIsPrimary { get; set; } = true;
}

public sealed class UpdateMyPatientProfileRequest
{
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public IFormFile? ProfileImage { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed record PatientContactRequest(string NameAr, string? NameEn, string PhoneNumber,
    PatientContactRelationshipType RelationshipType, Guid? LinkedPatientId, bool IsPrimary);
