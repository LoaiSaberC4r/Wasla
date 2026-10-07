
using System.Security.Cryptography;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Medications;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;

namespace Wasla.Application.Features.Diagnostics;

internal sealed class DiagnosticResultHandler(DiagnosticAccess access, IUnitOfWork<WaslaWritePersistence> work,
    IDiagnosticReadService reader, IDiagnosticCoverageReader coverageReader, MedicationIdempotency idem,
    IMediaService media, IDateTimeProvider clock, DiagnosticMediaCompensation compensation) : ICommandHandler<DiagnosticResultCommand, DiagnosticResultMutationResponse>
{
    public async Task<Result<DiagnosticResultMutationResponse>> Handle(DiagnosticResultCommand r, CancellationToken ct)
    {
        var patient = r.Mutation is DiagnosticResultMutation.Submit or DiagnosticResultMutation.Withdraw;
        var permission = r.Kind + (r.Mutation switch
        {
            DiagnosticResultMutation.Submit => "ResultSubmissions.CreateOwn",
            DiagnosticResultMutation.Withdraw => "ResultSubmissions.WithdrawOwn",
            DiagnosticResultMutation.Accept or DiagnosticResultMutation.Reject => "ResultSubmissions.ReviewOwn",
            DiagnosticResultMutation.Correct => "Results.CorrectOwn",
            DiagnosticResultMutation.Void => "Results.VoidOwn",
            _ => "Results.UploadOwn"
        });
        var actor = await access.ActorAsync(permission, patient ? UserType.Patient : UserType.Doctor, ct);
        if (actor.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(actor.Errors);
        var payload = new
        {
            r.Kind,
            r.Mutation,
            r.RequestId,
            r.ResultId,
            r.SubmissionId,
            r.RowVersion,
            r.CoveredItemIds,
            r.ExternalProviderName,
            r.ExternalReportDate,
            r.PatientNote,
            r.Reason,
            Attachments = r.Attachments?.Select(a => new { a.FileName, a.ContentType, a.Kind, Hash = Convert.ToHexString(SHA256.HashData(a.Content)) }).ToArray()
        };
        Task<Result<DiagnosticResultMutationResponse>> Run() => r.Kind == DiagnosticKind.Lab ? MutateLabAsync(r, actor.Value, patient, ct) : MutateRadiologyAsync(r, actor.Value, patient, ct);
        var sensitive = r.Mutation is DiagnosticResultMutation.Submit or DiagnosticResultMutation.Accept or DiagnosticResultMutation.Upload or DiagnosticResultMutation.Correct or DiagnosticResultMutation.Void;
        return sensitive ? await idem.ExecuteAsync(actor.Value.UserId, "Phase15:" + r.Kind + "Result:" + r.Mutation, r.IdempotencyKey, payload, Run, ct,
            async original =>
            {
                var state = await reader.RequestAsync(r.Kind, original.Request.RequestId!.Value, actor.Value.OwnerId, patient, actor.Value.Permissions, ct);
                if (state is null) return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.NotFound(r.Kind + "Request.NotFound"));
                return Result<DiagnosticResultMutationResponse>.Ok(new(
                    original.Submission is null ? null : await reader.SubmissionAsync(r.Kind, original.Submission.SubmissionId, actor.Value.OwnerId, patient, actor.Value.Permissions, ct),
                    original.Result is null ? null : await reader.ResultAsync(r.Kind, original.Result.ResultId, actor.Value.OwnerId, patient, actor.Value.Permissions, ct), state));
            }) : await Run();
    }
    private async Task<IReadOnlyList<DiagnosticAttachmentData>> SaveFilesAsync(DiagnosticResultCommand r, Guid request, CancellationToken ct)
    {
        var files = r.Attachments ?? [];
        if (files.Count is < 1 or > 20 || files.Sum(f => (long)f.Content.Length) > 100L * 1024 * 1024)
            return [];
        var output = new List<DiagnosticAttachmentData>();
        foreach (var file in files)
        {
            // BuildingBlock performs name, extension, MIME, signature, bounded-size and malware validation.
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".pdf" or ".jpg" or ".jpeg" or ".png") || file.Content.Length > 10 * 1024 * 1024 || !Enum.IsDefined(file.Kind)) return [];
            await using var content = new MemoryStream(file.Content, writable: false);
            var stored = await media.SaveAsync(new MediaUpload(content, file.FileName, file.ContentType, file.Content.Length),
                new($"Diagnostics/{r.Kind}/{request:N}"), ct);
            compensation.Track(stored.Key);
            output.Add(new(stored.Key, file.FileName, stored.ContentType, stored.Length,
                Convert.ToHexString(SHA256.HashData(file.Content)), clock.UtcNow, file.Kind));
        }
        return output;
    }

    private async Task<Result<DiagnosticResultMutationResponse>> MutateLabAsync(DiagnosticResultCommand r, DiagnosticActor actor, bool patient, CancellationToken ct)
    {
        var resultRepo = work.WriteRepository<LabResult>();
        var submissionRepo = work.WriteRepository<PatientLabResultSubmission>();
        var existingResult = r.ResultId is { } rid ? await resultRepo.FirstOrDefaultAsync(new DiagnosticForUpdate<LabResult>(x => x.Id == rid, x => x.Versions), ct) : null;
        var submission = r.SubmissionId is { } sid ? await submissionRepo.FirstOrDefaultAsync(new DiagnosticForUpdate<PatientLabResultSubmission>(x => x.Id == sid, x => x.Attachments), ct) : null;
        var requestId = existingResult?.RequestId ?? submission?.RequestId ?? r.RequestId.GetValueOrDefault();
        await idem.LockResourceAsync("Phase15:LabOrder", requestId, ct);
        var request = await work.WriteRepository<LabRequest>().FirstOrDefaultAsync(new DiagnosticForUpdate<LabRequest>(x => x.Id == requestId, x => x.Items), ct);
        if (request is null || (patient ? request.PatientId != actor.PatientId || request.Status == DiagnosticRequestStatus.Draft : request.DoctorId != actor.DoctorId ||
            !await access.OwnPracticeAsync(actor.DoctorId!.Value, request.DoctorPracticeId, ct)) || r.ResultId.HasValue && existingResult is null || r.SubmissionId.HasValue && submission is null)
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.NotFound("LabRequest.NotFound"));
        if (existingResult is not null && !TicketRowVersion.Matches(existingResult.RowVersion, r.RowVersion ?? "") ||
            submission is not null && !TicketRowVersion.Matches(submission.RowVersion, r.RowVersion ?? "") ||
            r.Mutation == DiagnosticResultMutation.Upload && !TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? ""))
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Conflict(existingResult is not null ? "LabResult.ConcurrencyConflict" : submission is not null ? "LabResultSubmission.ConcurrencyConflict" : "LabRequest.ConcurrencyConflict"));
        if (submission is not null && submission.Status != PatientSubmissionStatus.PendingReview)
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Conflict("LabResultSubmission.NotPending"));
        Result changed = Result.Ok();
        var attachments = r.Mutation is DiagnosticResultMutation.Upload or DiagnosticResultMutation.Correct or DiagnosticResultMutation.Submit ?
            await SaveFilesAsync(r, request.Id, ct) : Array.Empty<DiagnosticAttachmentData>();
        switch (r.Mutation)
        {
            case DiagnosticResultMutation.Submit:
                if (r.CoveredItemIds is { Count: > 0 }) return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Validation("LabResultSubmission.InvalidCoverage"));
                var createdSubmission = PatientLabResultSubmission.Submit(request, attachments, r.ExternalProviderName, r.ExternalReportDate, r.PatientNote, actor.PatientId!.Value, actor.UserId, clock.UtcNow);
                if (createdSubmission.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(createdSubmission.Errors);
                submission = createdSubmission.Value; await submissionRepo.AddAsync(submission, ct); break;
            case DiagnosticResultMutation.Upload:
            case DiagnosticResultMutation.Accept:
                if (r.Mutation == DiagnosticResultMutation.Accept && submission is null) return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.NotFound("LabResultSubmission.NotFound"));
                var created = LabResult.Record(request, r.CoveredItemIds ?? [], submission?.Attachments.Select(a => a.Data()).ToArray() ?? attachments,
                    submission?.ExternalProviderName ?? r.ExternalProviderName, submission?.ExternalReportDate ?? r.ExternalReportDate,
                    submission is null ? DiagnosticUploader.Doctor : DiagnosticUploader.Patient, submission?.UploadedByUserId ?? actor.UserId,
                    submission?.SubmittedAtUtc ?? clock.UtcNow, actor.DoctorId!.Value, actor.UserId, clock.UtcNow, submission?.Id);
                if (created.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(created.Errors);
                existingResult = created.Value; await resultRepo.AddAsync(existingResult, ct);
                if (submission is not null) changed = submission.Accept(existingResult.Id, actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Correct:
                changed = existingResult is null ? Result.Fail(DiagnosticErrors.NotFound("LabResult.NotFound")) : existingResult.Correct(request, r.CoveredItemIds ?? [], attachments, r.ExternalProviderName, r.ExternalReportDate, r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Void:
                changed = existingResult is null ? Result.Fail(DiagnosticErrors.NotFound("LabResult.NotFound")) : existingResult.Void(r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Reject:
                changed = submission is null ? Result.Fail(DiagnosticErrors.NotFound("LabResultSubmission.NotFound")) : submission.Reject(r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Withdraw:
                changed = submission is null ? Result.Fail(DiagnosticErrors.NotFound("LabResultSubmission.NotFound")) : submission.Withdraw(actor.UserId, clock.UtcNow); break;
        }
        if (changed.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(changed.Errors);
        await work.SaveChangesAsync(ct);
        if (r.Mutation is DiagnosticResultMutation.Upload or DiagnosticResultMutation.Accept or DiagnosticResultMutation.Correct or DiagnosticResultMutation.Void)
        {
            request.RecalculateCoverage(await coverageReader.CurrentCoverageAsync(r.Kind, request.Id, ct), actor.UserId, clock.UtcNow);
            await work.SaveChangesAsync(ct);
        }
        var response = new DiagnosticResultMutationResponse(
            submission is null ? null : await reader.SubmissionAsync(r.Kind, submission.Id, actor.OwnerId, patient, actor.Permissions, ct),
            existingResult is null ? null : await reader.ResultAsync(r.Kind, existingResult.Id, actor.OwnerId, patient, actor.Permissions, ct),
            (await reader.RequestAsync(r.Kind, request.Id, actor.OwnerId, patient, actor.Permissions, ct))!);
        return Result<DiagnosticResultMutationResponse>.Ok(response);
    }


    private async Task<Result<DiagnosticResultMutationResponse>> MutateRadiologyAsync(DiagnosticResultCommand r, DiagnosticActor actor, bool patient, CancellationToken ct)
    {
        var resultRepo = work.WriteRepository<RadiologyResult>();
        var submissionRepo = work.WriteRepository<PatientRadiologyResultSubmission>();
        var existingResult = r.ResultId is { } rid ? await resultRepo.FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyResult>(x => x.Id == rid, x => x.Versions), ct) : null;
        var submission = r.SubmissionId is { } sid ? await submissionRepo.FirstOrDefaultAsync(new DiagnosticForUpdate<PatientRadiologyResultSubmission>(x => x.Id == sid, x => x.Attachments), ct) : null;
        var requestId = existingResult?.RequestId ?? submission?.RequestId ?? r.RequestId.GetValueOrDefault();
        await idem.LockResourceAsync("Phase15:RadiologyOrder", requestId, ct);
        var request = await work.WriteRepository<RadiologyRequest>().FirstOrDefaultAsync(new DiagnosticForUpdate<RadiologyRequest>(x => x.Id == requestId, x => x.Items), ct);
        if (request is null || (patient ? request.PatientId != actor.PatientId || request.Status == DiagnosticRequestStatus.Draft : request.DoctorId != actor.DoctorId ||
            !await access.OwnPracticeAsync(actor.DoctorId!.Value, request.DoctorPracticeId, ct)) || r.ResultId.HasValue && existingResult is null || r.SubmissionId.HasValue && submission is null)
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.NotFound("RadiologyRequest.NotFound"));
        if (existingResult is not null && !TicketRowVersion.Matches(existingResult.RowVersion, r.RowVersion ?? "") ||
            submission is not null && !TicketRowVersion.Matches(submission.RowVersion, r.RowVersion ?? "") ||
            r.Mutation == DiagnosticResultMutation.Upload && !TicketRowVersion.Matches(request.RowVersion, r.RowVersion ?? ""))
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Conflict(existingResult is not null ? "RadiologyResult.ConcurrencyConflict" : submission is not null ? "RadiologyResultSubmission.ConcurrencyConflict" : "RadiologyRequest.ConcurrencyConflict"));
        if (submission is not null && submission.Status != PatientSubmissionStatus.PendingReview)
            return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Conflict("RadiologyResultSubmission.NotPending"));
        Result changed = Result.Ok();
        var attachments = r.Mutation is DiagnosticResultMutation.Upload or DiagnosticResultMutation.Correct or DiagnosticResultMutation.Submit ?
            await SaveFilesAsync(r, request.Id, ct) : Array.Empty<DiagnosticAttachmentData>();
        switch (r.Mutation)
        {
            case DiagnosticResultMutation.Submit:
                if (r.CoveredItemIds is { Count: > 0 }) return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.Validation("RadiologyResultSubmission.InvalidCoverage"));
                var createdSubmission = PatientRadiologyResultSubmission.Submit(request, attachments, r.ExternalProviderName, r.ExternalReportDate, r.PatientNote, actor.PatientId!.Value, actor.UserId, clock.UtcNow);
                if (createdSubmission.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(createdSubmission.Errors);
                submission = createdSubmission.Value; await submissionRepo.AddAsync(submission, ct); break;
            case DiagnosticResultMutation.Upload:
            case DiagnosticResultMutation.Accept:
                if (r.Mutation == DiagnosticResultMutation.Accept && submission is null) return Result<DiagnosticResultMutationResponse>.Fail(DiagnosticErrors.NotFound("RadiologyResultSubmission.NotFound"));
                var created = RadiologyResult.Record(request, r.CoveredItemIds ?? [], submission?.Attachments.Select(a => a.Data()).ToArray() ?? attachments,
                    submission?.ExternalProviderName ?? r.ExternalProviderName, submission?.ExternalReportDate ?? r.ExternalReportDate,
                    submission is null ? DiagnosticUploader.Doctor : DiagnosticUploader.Patient, submission?.UploadedByUserId ?? actor.UserId,
                    submission?.SubmittedAtUtc ?? clock.UtcNow, actor.DoctorId!.Value, actor.UserId, clock.UtcNow, submission?.Id);
                if (created.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(created.Errors);
                existingResult = created.Value; await resultRepo.AddAsync(existingResult, ct);
                if (submission is not null) changed = submission.Accept(existingResult.Id, actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Correct:
                changed = existingResult is null ? Result.Fail(DiagnosticErrors.NotFound("RadiologyResult.NotFound")) : existingResult.Correct(request, r.CoveredItemIds ?? [], attachments, r.ExternalProviderName, r.ExternalReportDate, r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Void:
                changed = existingResult is null ? Result.Fail(DiagnosticErrors.NotFound("RadiologyResult.NotFound")) : existingResult.Void(r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Reject:
                changed = submission is null ? Result.Fail(DiagnosticErrors.NotFound("RadiologyResultSubmission.NotFound")) : submission.Reject(r.Reason ?? "", actor.UserId, clock.UtcNow); break;
            case DiagnosticResultMutation.Withdraw:
                changed = submission is null ? Result.Fail(DiagnosticErrors.NotFound("RadiologyResultSubmission.NotFound")) : submission.Withdraw(actor.UserId, clock.UtcNow); break;
        }
        if (changed.IsFailure) return Result<DiagnosticResultMutationResponse>.Fail(changed.Errors);
        await work.SaveChangesAsync(ct);
        if (r.Mutation is DiagnosticResultMutation.Upload or DiagnosticResultMutation.Accept or DiagnosticResultMutation.Correct or DiagnosticResultMutation.Void)
        {
            request.RecalculateCoverage(await coverageReader.CurrentCoverageAsync(r.Kind, request.Id, ct), actor.UserId, clock.UtcNow);
            await work.SaveChangesAsync(ct);
        }
        var response = new DiagnosticResultMutationResponse(
            submission is null ? null : await reader.SubmissionAsync(r.Kind, submission.Id, actor.OwnerId, patient, actor.Permissions, ct),
            existingResult is null ? null : await reader.ResultAsync(r.Kind, existingResult.Id, actor.OwnerId, patient, actor.Permissions, ct),
            (await reader.RequestAsync(r.Kind, request.Id, actor.OwnerId, patient, actor.Permissions, ct))!);
        return Result<DiagnosticResultMutationResponse>.Ok(response);
    }

}
