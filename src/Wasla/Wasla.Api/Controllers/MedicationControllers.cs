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
[Route("api/v{version:apiVersion}/admin/drug-catalog-managers")]
[ProducesResponseType<DrugCatalogManagerResponse>(StatusCodes.Status200OK)]
public sealed class DrugCatalogManagersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClinicalPage<DrugCatalogManagerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListDrugCatalogManagersQuery(search, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => (await sender.Send(new GetDrugCatalogManagerQuery(id), ct)).ToIActionResult(ct);
    [HttpPost]
    [ProducesResponseType<DrugCatalogManagerResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateDrugCatalogManagerCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ManagerContactRequest request, CancellationToken ct)
        => (await sender.Send(new UpdateDrugCatalogManagerCommand(id, request.Email, request.PhoneNumber), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => (await sender.Send(new SetDrugCatalogManagerActiveCommand(id, true), ct)).ToIActionResult(ct);
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => (await sender.Send(new SetDrugCatalogManagerActiveCommand(id, false), ct)).ToIActionResult(ct);
}
public sealed record ManagerContactRequest(string Email, string? PhoneNumber);
public sealed record CatalogUpdateRequest(DrugData Data, string RowVersion, string? Reason);
public sealed record MedicationActionRequest(string RowVersion, string? Reason = null, Guid? TargetDrugCatalogId = null);
public sealed record DrugRequestUpdateRequest(MedicationRequestData Data, string RowVersion);
public sealed record DrugRequestReviewRequest(string RowVersion, string? Reason = null, DrugData? ApprovedData = null, Guid? DuplicateOfDrugCatalogId = null);

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.DrugCatalogManager)]
[Route("api/v{version:apiVersion}/admin/drug-catalog")]
[ProducesResponseType<DrugCatalogDetailsResponse>(StatusCodes.Status200OK)]
public sealed class DrugCatalogController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClinicalPage<DrugCatalogResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] DrugCatalogStatus? status = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new SearchDrugCatalogQuery(search, status, false, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{drugId:guid}")]
    public async Task<IActionResult> Get(Guid drugId, CancellationToken ct) => (await sender.Send(new GetDrugCatalogQuery(drugId), ct)).ToIActionResult(ct);
    [HttpPost]
    [ProducesResponseType<DrugCatalogDetailsResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(DrugData request, CancellationToken ct)
    {
        var result = await sender.Send(new ManageDrugCatalogCommand(DrugCatalogMutation.Create, null, request), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPut("{drugId:guid}")]
    public async Task<IActionResult> Update(Guid drugId, CatalogUpdateRequest request, CancellationToken ct)
        => (await sender.Send(new ManageDrugCatalogCommand(DrugCatalogMutation.Update, drugId, request.Data, request.RowVersion, request.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{drugId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid drugId, MedicationActionRequest r, CancellationToken ct)
        => (await sender.Send(new ManageDrugCatalogCommand(DrugCatalogMutation.Activate, drugId, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{drugId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid drugId, MedicationActionRequest r, CancellationToken ct)
        => (await sender.Send(new ManageDrugCatalogCommand(DrugCatalogMutation.Deactivate, drugId, RowVersion: r.RowVersion, Reason: r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{sourceDrugId:guid}/merge")]
    public async Task<IActionResult> Merge(Guid sourceDrugId, MedicationActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new ManageDrugCatalogCommand(DrugCatalogMutation.Merge, sourceDrugId, RowVersion: r.RowVersion, Reason: r.Reason, TargetId: r.TargetDrugCatalogId, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("imports/preview"), RequestSizeLimit(DrugImportParser.MaximumFileBytes + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<DrugImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview([FromForm] DrugImportUploadRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length is 0 or > DrugImportParser.MaximumFileBytes)
            return new[] { MedicationErrors.Validation("DrugCatalogImport.InvalidFile") }.ToActionProblem(ct);
        using var stream = new MemoryStream(); await request.File.CopyToAsync(stream, ct);
        return (await sender.Send(new PreviewDrugImportCommand(stream.ToArray(), request.SourceVersion, request.SourceCommitSha), ct)).ToIActionResult(ct);
    }
    [HttpGet("imports")]
    [ProducesResponseType<ClinicalPage<DrugImportBatchResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Imports([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListDrugImportsQuery(null, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("imports/{batchId:guid}")]
    [ProducesResponseType<DrugImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(Guid batchId, CancellationToken ct)
    {
        var result = await sender.Send(new ListDrugImportsQuery(batchId), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("imports/{batchId:guid}/changes")]
    [ProducesResponseType<ClinicalPage<DrugImportRecordResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Changes(Guid batchId, [FromQuery] DrugImportChangeType? changeType = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListDrugImportChangesQuery(batchId, changeType, pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpPost("imports/{batchId:guid}/apply")]
    [ProducesResponseType<DrugImportBatchResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply(Guid batchId, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new ApplyDrugImportCommand(batchId, key ?? string.Empty), ct)).ToIActionResult(ct);
}
public sealed class DrugImportUploadRequest
{
    public IFormFile? File { get; set; }
    public string? SourceVersion { get; set; }
    public string? SourceCommitSha { get; set; }
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Doctor)]
[Route("api/v{version:apiVersion}/doctors/me")]
[ProducesResponseType<DrugRequestResponse>(StatusCodes.Status200OK)]
public sealed class DoctorDrugCatalogController(ISender sender) : ControllerBase
{
    [HttpGet("drug-catalog")]
    [ProducesResponseType<ClinicalPage<DrugCatalogResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search = null, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new SearchDrugCatalogQuery(search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("drug-catalog-requests")]
    [ProducesResponseType<ClinicalPage<DrugRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Requests([FromQuery] DrugCatalogRequestStatus? status = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListDrugRequestsQuery(true, Status: status, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("drug-catalog-requests/{requestId:guid}")]
    public async Task<IActionResult> RequestDetails(Guid requestId, CancellationToken ct)
    {
        var result = await sender.Send(new ListDrugRequestsQuery(true, requestId), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("drug-catalog-requests")]
    [ProducesResponseType<DrugRequestResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(MedicationRequestData request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveDrugRequestCommand(request), ct);
        return result.IsSuccess ? StatusCode(201, result.Value) : result.Errors.ToActionProblem(ct);
    }
    [HttpPut("drug-catalog-requests/{requestId:guid}")]
    public async Task<IActionResult> Update(Guid requestId, DrugRequestUpdateRequest r, CancellationToken ct)
        => (await sender.Send(new SaveDrugRequestCommand(r.Data, requestId, r.RowVersion), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.DrugCatalogManager)]
[Route("api/v{version:apiVersion}/admin/drug-catalog-requests")]
[ProducesResponseType<DrugRequestResponse>(StatusCodes.Status200OK)]
public sealed class DrugCatalogRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClinicalPage<DrugRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DrugCatalogRequestStatus? status = null, [FromQuery] Guid? doctorId = null,
        [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListDrugRequestsQuery(false, Status: status, DoctorId: doctorId, Search: search, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
    {
        var result = await sender.Send(new ListDrugRequestsQuery(false, requestId), ct);
        return result.IsSuccess ? Ok(result.Value.Items.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpPost("{requestId:guid}/request-more-info")]
    public async Task<IActionResult> MoreInfo(Guid requestId, DrugRequestReviewRequest r, CancellationToken ct)
        => (await sender.Send(new ReviewDrugRequestCommand(requestId, DrugCatalogRequestStatus.NeedsMoreInfo, r.RowVersion, r.Reason), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid requestId, DrugRequestReviewRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new ReviewDrugRequestCommand(requestId, DrugCatalogRequestStatus.Approved, r.RowVersion, r.Reason, r.ApprovedData, IdempotencyKey: key), ct)).ToIActionResult(ct);
    [HttpPost("{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, DrugRequestReviewRequest r, CancellationToken ct)
        => (await sender.Send(new ReviewDrugRequestCommand(requestId, DrugCatalogRequestStatus.Rejected, r.RowVersion, r.Reason, DuplicateOfDrugCatalogId: r.DuplicateOfDrugCatalogId), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Doctor)]
[Route("api/v{version:apiVersion}/doctors/me/practices/{practiceId:guid}/encounters/{encounterId:guid}/prescription/items")]
[ProducesResponseType<PrescriptionStateResponse>(StatusCodes.Status200OK)]
public sealed class EncounterPrescriptionItemsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Add(Guid practiceId, Guid encounterId, PrescriptionItemRequest r,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(r.Command(PrescriptionMutation.AddItem, practiceId, encounterId, null, null, key, false), ct)).ToIActionResult(ct);
    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> Update(Guid practiceId, Guid encounterId, Guid itemId, PrescriptionItemRequest r, CancellationToken ct)
        => (await sender.Send(r.Command(PrescriptionMutation.UpdateItem, practiceId, encounterId, null, itemId, null, false), ct)).ToIActionResult(ct);
    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Remove(Guid practiceId, Guid encounterId, Guid itemId, [FromQuery] string rowVersion, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.RemoveItem, practiceId, encounterId, ItemId: itemId, RowVersion: rowVersion), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Doctor)]
[Route("api/v{version:apiVersion}/doctors/me/prescriptions/{prescriptionId:guid}")]
[ProducesResponseType<PrescriptionStateResponse>(StatusCodes.Status200OK)]
public sealed class DoctorPrescriptionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid prescriptionId, CancellationToken ct) => (await sender.Send(new GetDoctorPrescriptionQuery(prescriptionId), ct)).ToIActionResult(ct);
    [HttpGet("versions")]
    [ProducesResponseType<IReadOnlyList<PrescriptionVersionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Versions(Guid prescriptionId, CancellationToken ct) => (await sender.Send(new ListPrescriptionVersionsQuery(prescriptionId), ct)).ToIActionResult(ct);
    [HttpGet("versions/{versionNumber:int}")]
    [ProducesResponseType<PrescriptionVersionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Version(Guid prescriptionId, int versionNumber, CancellationToken ct)
    {
        var result = await sender.Send(new ListPrescriptionVersionsQuery(prescriptionId, versionNumber), ct);
        return result.IsSuccess ? Ok(result.Value.Single()) : result.Errors.ToActionProblem(ct);
    }
    [HttpGet("correction-draft")]
    public async Task<IActionResult> Draft(Guid prescriptionId, CancellationToken ct) => (await sender.Send(new GetDoctorPrescriptionQuery(prescriptionId, true), ct)).ToIActionResult(ct);
    [HttpPost("correction-draft")]
    public async Task<IActionResult> Start(Guid prescriptionId, MedicationActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.StartCorrection, PrescriptionId: prescriptionId, RowVersion: r.RowVersion, Reason: r.Reason, IdempotencyKey: key, Correction: true), ct)).ToIActionResult(ct);
    [HttpPost("correction-draft/items")]
    public async Task<IActionResult> Add(Guid prescriptionId, PrescriptionItemRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(r.Command(PrescriptionMutation.AddItem, null, null, prescriptionId, null, key, true), ct)).ToIActionResult(ct);
    [HttpPut("correction-draft/items/{itemId:guid}")]
    public async Task<IActionResult> Update(Guid prescriptionId, Guid itemId, PrescriptionItemRequest r, CancellationToken ct)
        => (await sender.Send(r.Command(PrescriptionMutation.UpdateItem, null, null, prescriptionId, itemId, null, true), ct)).ToIActionResult(ct);
    [HttpDelete("correction-draft/items/{itemId:guid}")]
    public async Task<IActionResult> Remove(Guid prescriptionId, Guid itemId, [FromQuery] string rowVersion, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.RemoveItem, PrescriptionId: prescriptionId, ItemId: itemId, RowVersion: rowVersion, Correction: true), ct)).ToIActionResult(ct);
    [HttpPost("correction-draft/finalize")]
    public async Task<IActionResult> Finalize(Guid prescriptionId, MedicationActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.FinalizeCorrection, PrescriptionId: prescriptionId, RowVersion: r.RowVersion, IdempotencyKey: key, Correction: true), ct)).ToIActionResult(ct);
    [HttpPost("correction-draft/discard")]
    public async Task<IActionResult> Discard(Guid prescriptionId, MedicationActionRequest r, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.DiscardCorrection, PrescriptionId: prescriptionId, RowVersion: r.RowVersion, Correction: true), ct)).ToIActionResult(ct);
    [HttpPost("void")]
    public async Task<IActionResult> Void(Guid prescriptionId, MedicationActionRequest r, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
        => (await sender.Send(new MutatePrescriptionCommand(PrescriptionMutation.Void, PrescriptionId: prescriptionId, RowVersion: r.RowVersion, Reason: r.Reason, IdempotencyKey: key), ct)).ToIActionResult(ct);
}

[ApiController, ApiVersion("1.0"), Authorize(Roles = SystemRoleNames.Patient)]
[Route("api/v{version:apiVersion}/prescriptions/mine")]
[ProducesResponseType<PatientPrescriptionResponse>(StatusCodes.Status200OK)]
public sealed class MyPrescriptionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ClinicalPage<PatientPrescriptionSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new ListMyPrescriptionsQuery(pageNumber, pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{prescriptionId:guid}")]
    public async Task<IActionResult> Get(Guid prescriptionId, CancellationToken ct) => (await sender.Send(new GetMyPrescriptionQuery(prescriptionId), ct)).ToIActionResult(ct);
}

public sealed record PrescriptionItemRequest(Guid? DrugCatalogId = null, MedicationRequestData? NewMedication = null,
    string? PrescriptionRowVersion = null, string? Strength = null, string? DosageForm = null, string? Route = null,
    string? Dose = null, MedicationFrequency? FrequencyCode = null, string? FrequencyText = null,
    MedicationDurationType? DurationType = null, int? DurationValue = null, MedicationDurationUnit? DurationUnit = null,
    bool AsNeeded = false, string? PrnReason = null, string? MinimumIntervalText = null, string? MaxPer24HoursText = null,
    decimal? QuantityValue = null, MedicationQuantityUnit? QuantityUnit = null, string? Instructions = null, string? RowVersion = null)
{
    internal MutatePrescriptionCommand Command(PrescriptionMutation mutation, Guid? practice, Guid? encounter,
        Guid? prescription, Guid? item, string? key, bool correction)
        => new(mutation, practice, encounter, prescription, item, DrugCatalogId, NewMedication,
            new(Strength, DosageForm, Route, Dose, FrequencyCode, FrequencyText, DurationType, DurationValue, DurationUnit,
                AsNeeded, PrnReason, MinimumIntervalText, MaxPer24HoursText, QuantityValue, QuantityUnit, Instructions),
            PrescriptionRowVersion ?? RowVersion, IdempotencyKey: key, Correction: correction);
}
