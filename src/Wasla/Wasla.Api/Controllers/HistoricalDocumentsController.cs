using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wasla.Api.Security;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/patients/me/historical-documents")]
public sealed class HistoricalDocumentsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(MedicalDocumentPermissionNames.ViewOwn)]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] bool includeWithdrawn = false, CancellationToken ct = default)
        => (await sender.Send(new HistoricalDocumentQuery(PageNumber: pageNumber, PageSize: pageSize, IncludeWithdrawn: includeWithdrawn), ct)).ToIActionResult(ct);
    [HttpGet("{documentId:guid}"), Permission(MedicalDocumentPermissionNames.ViewOwn)]
    public async Task<IActionResult> Detail(Guid documentId, CancellationToken ct)
        => (await sender.Send(new HistoricalDocumentQuery(DocumentId: documentId), ct)).ToIActionResult(ct);
    [HttpGet("{documentId:guid}/versions"), Permission(MedicalDocumentPermissionNames.ViewOwn)]
    public async Task<IActionResult> Versions(Guid documentId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new HistoricalDocumentQuery(DocumentId: documentId, Versions: true, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpPost, Permission(MedicalDocumentPermissionNames.CreateOwn), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> Create([FromForm] HistoricalDocumentForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Upload(sender, this, null, null, form, key, ct);
    [HttpPost("{documentId:guid}/versions"), Permission(MedicalDocumentPermissionNames.ReviseOwn), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> Revise(Guid documentId, [FromForm] HistoricalDocumentForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => Upload(sender, this, null, documentId, form, key, ct);
    [HttpPost("{documentId:guid}/withdraw"), Permission(MedicalDocumentPermissionNames.WithdrawOwn)]
    public async Task<IActionResult> Withdraw(Guid documentId, [FromBody] HistoricalDocumentWithdrawal form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new HistoricalDocumentMutation("withdraw", null, documentId, key, form.RowVersion, Reason: form.Reason), ct)).ToIActionResult(ct);
    [HttpGet("{documentId:guid}/attachments/{attachmentId:guid}/content"), Permission(MedicalDocumentPermissionNames.ViewOwn)]
    public Task<IActionResult> Content(Guid documentId, Guid attachmentId, CancellationToken ct) => Download(sender, this, null, documentId, attachmentId, ct);
    internal static async Task<IActionResult> Upload(ISender sender, ControllerBase controller, Guid? child, Guid? doc, HistoricalDocumentForm form, string key, CancellationToken ct)
    {
        var streams = new List<Stream>();
        try
        {
            var files = form.Files.Select(f => { var stream = f.OpenReadStream(); streams.Add(stream); return new MediaUpload(stream, f.FileName, f.ContentType, f.Length); }).ToArray();
            var response = await sender.Send(new HistoricalDocumentMutation(doc.HasValue ? "revise" : "create", child, doc, key, form.RowVersion,
                new(form.Category, form.DocumentDate, form.DateUnknown, form.ProviderName, form.Title, form.PatientNote), files, form.Reason), ct);
            return response.IsSuccess ? controller.StatusCode(response.Value.ValidationState is "RejectedUnsafe" or "Quarantined" ? 202 : doc.HasValue ? 200 : 201, response.Value) : response.Errors.ToActionProblem(ct);
        }
        finally { foreach (var stream in streams) await stream.DisposeAsync(); }
    }
    internal static async Task<IActionResult> Download(ISender sender, ControllerBase controller, Guid? child, Guid doc, Guid attachment, CancellationToken ct)
    {
        var response = await sender.Send(new HistoricalDocumentMediaQuery(child, doc, attachment), ct);
        controller.Response.Headers.XContentTypeOptions = "nosniff"; controller.Response.Headers.CacheControl = "no-store";
        return response.IsSuccess ? controller.File(response.Value.Content, response.Value.ContentType, response.Value.FileName) : response.Errors.ToActionProblem(ct);
    }
}
[ApiController, ApiVersion("1.0"), Authorize, ResponseCache(NoStore = true)]
[Route("api/v{version:apiVersion}/families/mine/dependents/{childPatientId:guid}/historical-documents")]
public sealed class DependentHistoricalDocumentsController(ISender sender) : ControllerBase
{
    [HttpGet, Permission(MedicalDocumentPermissionNames.ViewDependent)]
    public async Task<IActionResult> List(Guid childPatientId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => (await sender.Send(new HistoricalDocumentQuery(childPatientId, PageNumber: pageNumber, PageSize: pageSize), ct)).ToIActionResult(ct);
    [HttpGet("{documentId:guid}"), Permission(MedicalDocumentPermissionNames.ViewDependent)]
    public async Task<IActionResult> Detail(Guid childPatientId, Guid documentId, CancellationToken ct)
        => (await sender.Send(new HistoricalDocumentQuery(childPatientId, documentId), ct)).ToIActionResult(ct);
    [HttpPost, Permission(MedicalDocumentPermissionNames.CreateDependent), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> Create(Guid childPatientId, [FromForm] HistoricalDocumentForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => HistoricalDocumentsController.Upload(sender, this, childPatientId, null, form, key, ct);
    [HttpPost("{documentId:guid}/versions"), Permission(MedicalDocumentPermissionNames.ReviseDependent), Consumes("multipart/form-data"), RequestSizeLimit(52440000)]
    public Task<IActionResult> Revise(Guid childPatientId, Guid documentId, [FromForm] HistoricalDocumentForm form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => HistoricalDocumentsController.Upload(sender, this, childPatientId, documentId, form, key, ct);
    [HttpPost("{documentId:guid}/withdraw"), Permission(MedicalDocumentPermissionNames.WithdrawDependent)]
    public async Task<IActionResult> Withdraw(Guid childPatientId, Guid documentId, [FromBody] HistoricalDocumentWithdrawal form, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
        => (await sender.Send(new HistoricalDocumentMutation("withdraw", childPatientId, documentId, key, form.RowVersion, Reason: form.Reason), ct)).ToIActionResult(ct);
    [HttpGet("{documentId:guid}/attachments/{attachmentId:guid}/content"), Permission(MedicalDocumentPermissionNames.ViewDependent)]
    public Task<IActionResult> Content(Guid childPatientId, Guid documentId, Guid attachmentId, CancellationToken ct) => HistoricalDocumentsController.Download(sender, this, childPatientId, documentId, attachmentId, ct);
}
public sealed class HistoricalDocumentForm
{
    public HistoricalDocumentCategory Category { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public bool DateUnknown { get; set; }
    public string? ProviderName { get; set; }
    public string? Title { get; set; }
    public string? PatientNote { get; set; }
    public string? RowVersion { get; set; }
    public string? Reason { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}
public sealed record HistoricalDocumentWithdrawal(string RowVersion, string Reason);
