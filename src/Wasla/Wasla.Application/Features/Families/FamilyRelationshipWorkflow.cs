using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Features.Patients;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Families;

public sealed record FamilyEvidenceUpload(FamilyRelationshipDocumentType DocumentType, MediaUpload File);

public sealed record SubmitFamilyRelationshipRequestCommand(
    FamilyRelationshipRequestType RequestType,
    Guid? FamilyId,
    Guid TargetPatientId,
    FamilyMemberRole RequesterClaimedRole,
    FamilyMemberRole TargetClaimedRole,
    IReadOnlyList<FamilyEvidenceUpload> Evidence)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record SubmitAssistedFamilyRelationshipRequestCommand(
    FamilyRelationshipRequestType RequestType,
    Guid? FamilyId,
    Guid RequesterPatientId,
    Guid TargetPatientId,
    FamilyMemberRole RequesterClaimedRole,
    FamilyMemberRole TargetClaimedRole,
    IReadOnlyList<FamilyEvidenceUpload> Evidence)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ResubmitFamilyRelationshipRequestCommand(Guid RequestId, IReadOnlyList<FamilyEvidenceUpload> Evidence, string RowVersion)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ResubmitAssistedFamilyRelationshipRequestCommand(Guid RequestId, IReadOnlyList<FamilyEvidenceUpload> Evidence, string RowVersion)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ApproveFamilyRelationshipRequestCommand(Guid RequestId, string RowVersion)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RejectFamilyRelationshipRequestCommand(Guid RequestId, string Reason, string RowVersion)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RequestFamilyRelationshipModificationCommand(Guid RequestId, string Message, string RowVersion)
    : ICommand<FamilyRelationshipRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record GetMyFamilyQuery : IQuery<MyFamilyResponse>;
public sealed record MyFamilyResponse(Guid FamilyId, FamilyStatus Status, IReadOnlyList<FamilyMemberResponse> Members);
public sealed record FamilyMemberResponse(Guid PatientId, string NameAr, string? NameEn, FamilyMemberRole Role, Gender Gender);

public sealed record GetMyFamilyRelationshipRequestsQuery(
    FamilyRelationshipRequestStatus? Status,
    FamilyRelationshipRequestType? RequestType,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<FamilyRelationshipRequestQueueResponse>>;
public sealed record ListAssistedFamilyRelationshipRequestsQuery(
    FamilyRelationshipRequestStatus? Status,
    FamilyRelationshipRequestType? RequestType,
    string? Search,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<FamilyRelationshipRequestQueueResponse>>;
public sealed record ListFamilyRelationshipRequestsQuery(
    FamilyRelationshipRequestStatus? Status,
    FamilyRelationshipRequestType? RequestType,
    string? Search,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<FamilyRelationshipRequestQueueResponse>>;

public enum FamilyRelationshipAccessMode { Self = 1, Assisted = 2, Admin = 3 }
public sealed record GetFamilyRelationshipRequestDetailsQuery(Guid RequestId, FamilyRelationshipAccessMode AccessMode)
    : IQuery<FamilyRelationshipRequestDetailsResponse>;
public sealed record GetFamilyRelationshipDocumentQuery(Guid RequestId, Guid DocumentId, FamilyRelationshipAccessMode AccessMode)
    : IQuery<PrivateMedia>;

public sealed record FamilyRelationshipRequestResponse(
    Guid RequestId,
    FamilyRelationshipRequestType RequestType,
    FamilyRelationshipRequestStatus Status,
    Guid? FamilyId,
    int CurrentRevisionNumber,
    string RowVersion);
public sealed record FamilyRelationshipRequestQueueResponse(
    Guid RequestId,
    FamilyRelationshipRequestType RequestType,
    FamilyRelationshipRequestStatus Status,
    Guid RequesterPatientId,
    string RequesterNameAr,
    Guid TargetPatientId,
    string TargetNameAr,
    FamilyMemberRole RequesterClaimedRole,
    FamilyMemberRole TargetClaimedRole,
    int CurrentRevisionNumber,
    DateTime SubmittedOnUtc,
    string RowVersion);
public sealed record FamilyRelationshipPatientResponse(Guid PatientId, string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender);
public sealed record FamilyRelationshipDocumentResponse(Guid DocumentId, int RevisionNumber, FamilyRelationshipDocumentType DocumentType, string OriginalFileName, string ContentType, long FileSize, DateTime UploadedOnUtc);
public sealed record FamilyRelationshipHistoryResponse(Guid HistoryId, int RevisionNumber, FamilyRelationshipRequestStatus? OldStatus, FamilyRelationshipRequestStatus NewStatus, FamilyRelationshipRequestAction Action, string? MessageOrReason, Guid PerformedByApplicationUserId, DateTime PerformedOnUtc);
public sealed record FamilyRelationshipRequestDetailsResponse(
    FamilyRelationshipRequestResponse Request,
    FamilyRelationshipPatientResponse Requester,
    FamilyRelationshipPatientResponse Target,
    FamilyMemberRole RequesterClaimedRole,
    FamilyMemberRole TargetClaimedRole,
    string? ModificationMessage,
    string? RejectionReason,
    IReadOnlyList<FamilyMemberResponse> FamilyMembers,
    IReadOnlyList<FamilyRelationshipDocumentResponse> Documents,
    IReadOnlyList<FamilyRelationshipHistoryResponse> History);

internal sealed class SubmitFamilyRelationshipRequestCommandValidator : AbstractValidator<SubmitFamilyRelationshipRequestCommand>
{
    public SubmitFamilyRelationshipRequestCommandValidator()
    {
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.TargetPatientId).NotEmpty();
        RuleFor(x => x.RequesterClaimedRole).IsInEnum().NotEqual(FamilyMemberRole.Child);
        RuleFor(x => x.TargetClaimedRole).IsInEnum();
        RuleFor(x => x.FamilyId).NotEmpty().When(x => x.RequestType == FamilyRelationshipRequestType.AddFamilyMember);
        RuleFor(x => x.FamilyId).Null().When(x => x.RequestType == FamilyRelationshipRequestType.CreateFamily);
        RuleFor(x => x.Evidence).NotNull().Must(files => files.Count > 0).WithMessage(ErrorMessage.FamilyRelationshipRequestEvidenceRequired);
        RuleForEach(x => x.Evidence).ChildRules(item => item.RuleFor(file => file.DocumentType).IsInEnum());
    }
}
internal sealed class SubmitAssistedFamilyRelationshipRequestCommandValidator : AbstractValidator<SubmitAssistedFamilyRelationshipRequestCommand>
{
    public SubmitAssistedFamilyRelationshipRequestCommandValidator()
    {
        RuleFor(x => x.RequesterPatientId).NotEmpty();
        RuleFor(x => x.TargetPatientId).NotEqual(x => x.RequesterPatientId);
        RuleFor(x => x.RequestType).IsInEnum();
        RuleFor(x => x.TargetPatientId).NotEmpty();
        RuleFor(x => x.RequesterClaimedRole).IsInEnum().NotEqual(FamilyMemberRole.Child);
        RuleFor(x => x.TargetClaimedRole).IsInEnum();
        RuleFor(x => x.FamilyId).NotEmpty().When(x => x.RequestType == FamilyRelationshipRequestType.AddFamilyMember);
        RuleFor(x => x.FamilyId).Null().When(x => x.RequestType == FamilyRelationshipRequestType.CreateFamily);
        RuleFor(x => x.Evidence).NotNull().Must(files => files.Count > 0).WithMessage(ErrorMessage.FamilyRelationshipRequestEvidenceRequired);
        RuleForEach(x => x.Evidence).ChildRules(item => item.RuleFor(file => file.DocumentType).IsInEnum());
    }
}

internal sealed class ResubmitFamilyRelationshipRequestCommandValidator : AbstractValidator<ResubmitFamilyRelationshipRequestCommand>
{
    public ResubmitFamilyRelationshipRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Evidence).NotNull().Must(files => files.Count > 0).WithMessage(ErrorMessage.FamilyRelationshipRequestEvidenceRequired);
        RuleForEach(x => x.Evidence).ChildRules(item => item.RuleFor(file => file.DocumentType).IsInEnum());
        RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}
internal sealed class ResubmitAssistedFamilyRelationshipRequestCommandValidator : AbstractValidator<ResubmitAssistedFamilyRelationshipRequestCommand>
{
    public ResubmitAssistedFamilyRelationshipRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Evidence).NotNull().Must(files => files.Count > 0).WithMessage(ErrorMessage.FamilyRelationshipRequestEvidenceRequired);
        RuleForEach(x => x.Evidence).ChildRules(item => item.RuleFor(file => file.DocumentType).IsInEnum());
        RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}
internal sealed class ApproveFamilyRelationshipRequestCommandValidator : AbstractValidator<ApproveFamilyRelationshipRequestCommand>
{
    public ApproveFamilyRelationshipRequestCommandValidator() { RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid); }
}
internal sealed class RejectFamilyRelationshipRequestCommandValidator : AbstractValidator<RejectFamilyRelationshipRequestCommand>
{
    public RejectFamilyRelationshipRequestCommandValidator() { RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000); RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid); }
}
internal sealed class RequestFamilyRelationshipModificationCommandValidator : AbstractValidator<RequestFamilyRelationshipModificationCommand>
{
    public RequestFamilyRelationshipModificationCommandValidator() { RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.Message).NotEmpty().MaximumLength(2000); RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid); }
}
internal sealed class GetMyFamilyRelationshipRequestsQueryValidator : AbstractValidator<GetMyFamilyRelationshipRequestsQuery>
{
    public GetMyFamilyRelationshipRequestsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.RequestType).IsInEnum().When(x => x.RequestType.HasValue);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
internal sealed class ListAssistedFamilyRelationshipRequestsQueryValidator : AbstractValidator<ListAssistedFamilyRelationshipRequestsQuery>
{
    public ListAssistedFamilyRelationshipRequestsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.RequestType).IsInEnum().When(x => x.RequestType.HasValue);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
internal sealed class ListFamilyRelationshipRequestsQueryValidator : AbstractValidator<ListFamilyRelationshipRequestsQuery>
{
    public ListFamilyRelationshipRequestsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.RequestType).IsInEnum().When(x => x.RequestType.HasValue);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class SubmitFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow, IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<SubmitFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public async Task<Result<FamilyRelationshipRequestResponse>> Handle(SubmitFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(patient.Errors);
        return await workflow.SubmitAsync(request.RequestType, request.FamilyId, patient.Value.Id, request.TargetPatientId,
            request.RequesterClaimedRole, request.TargetClaimedRole, request.Evidence, currentUser.UserId!.Value, cancellationToken);
    }
}

internal sealed class SubmitAssistedFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow, ICurrentUser currentUser)
    : ICommandHandler<SubmitAssistedFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public Task<Result<FamilyRelationshipRequestResponse>> Handle(SubmitAssistedFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
        => !currentUser.IsAuthenticated || currentUser.UserId is not { } actorId
            ? Task.FromResult(Result<FamilyRelationshipRequestResponse>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired)))
            : workflow.SubmitAsync(request.RequestType, request.FamilyId, request.RequesterPatientId, request.TargetPatientId,
                request.RequesterClaimedRole, request.TargetClaimedRole, request.Evidence, actorId, cancellationToken);
}

internal sealed class ResubmitFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow, IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<ResubmitFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public async Task<Result<FamilyRelationshipRequestResponse>> Handle(ResubmitFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(patient.Errors);
        return await workflow.ResubmitAsync(request.RequestId, request.Evidence, request.RowVersion, currentUser.UserId!.Value, patient.Value.Id, false, cancellationToken);
    }
}

internal sealed class ResubmitAssistedFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow, ICurrentUser currentUser)
    : ICommandHandler<ResubmitAssistedFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public Task<Result<FamilyRelationshipRequestResponse>> Handle(ResubmitAssistedFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
        => !currentUser.IsAuthenticated || currentUser.UserId is not { } actorId
            ? Task.FromResult(Result<FamilyRelationshipRequestResponse>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired)))
            : workflow.ResubmitAsync(request.RequestId, request.Evidence, request.RowVersion, actorId, null, true, cancellationToken);
}

internal sealed class ApproveFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow)
    : ICommandHandler<ApproveFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public Task<Result<FamilyRelationshipRequestResponse>> Handle(ApproveFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
        => workflow.ApproveAsync(request.RequestId, request.RowVersion, cancellationToken);
}
internal sealed class RejectFamilyRelationshipRequestCommandHandler(FamilyRelationshipWorkflowService workflow)
    : ICommandHandler<RejectFamilyRelationshipRequestCommand, FamilyRelationshipRequestResponse>
{
    public Task<Result<FamilyRelationshipRequestResponse>> Handle(RejectFamilyRelationshipRequestCommand request, CancellationToken cancellationToken)
        => workflow.ReviewAsync(request.RequestId, request.RowVersion, request.Reason, false, cancellationToken);
}
internal sealed class RequestFamilyRelationshipModificationCommandHandler(FamilyRelationshipWorkflowService workflow)
    : ICommandHandler<RequestFamilyRelationshipModificationCommand, FamilyRelationshipRequestResponse>
{
    public Task<Result<FamilyRelationshipRequestResponse>> Handle(RequestFamilyRelationshipModificationCommand request, CancellationToken cancellationToken)
        => workflow.ReviewAsync(request.RequestId, request.RowVersion, request.Message, true, cancellationToken);
}

internal sealed class GetMyFamilyQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetMyFamilyQuery, MyFamilyResponse>
{
    public async Task<Result<MyFamilyResponse>> Handle(GetMyFamilyQuery request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<MyFamilyResponse>.Fail(patient.Errors);
        var member = await dataStore.FindActiveFamilyMemberByPatientIdAsync(patient.Value.Id, cancellationToken);
        if (member is null) return Result<MyFamilyResponse>.Fail(FamilyErrors.NotFound);
        var family = await dataStore.FindFamilyByIdAsync(member.FamilyId, cancellationToken);
        if (family is null) return Result<MyFamilyResponse>.Fail(FamilyErrors.NotFound);
        var members = await dataStore.ListActiveFamilyMembersAsync(family.Id, cancellationToken);
        return Result<MyFamilyResponse>.Ok(new(family.Id, family.Status, members.Select(FamilyWorkflowMapper.Member).ToArray()));
    }
}

internal sealed class GetMyFamilyRelationshipRequestsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetMyFamilyRelationshipRequestsQuery, PagedResponse<FamilyRelationshipRequestQueueResponse>>
{
    public async Task<Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>> Handle(GetMyFamilyRelationshipRequestsQuery request, CancellationToken cancellationToken)
    {
        var patient = await PatientIdentity.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (patient.IsFailure) return Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>.Fail(patient.Errors);
        return Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>.Ok(await FamilyWorkflowMapper.ListAsync(
            dataStore, request.Status, request.RequestType, null, null, patient.Value.Id, request.PageNumber, request.PageSize, cancellationToken));
    }
}

internal sealed class ListAssistedFamilyRelationshipRequestsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListAssistedFamilyRelationshipRequestsQuery, PagedResponse<FamilyRelationshipRequestQueueResponse>>
{
    public async Task<Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>> Handle(ListAssistedFamilyRelationshipRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
            return Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        return Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>.Ok(await FamilyWorkflowMapper.ListAsync(
            dataStore, request.Status, request.RequestType, request.Search, actorId, null, request.PageNumber, request.PageSize, cancellationToken));
    }
}

internal sealed class ListFamilyRelationshipRequestsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<ListFamilyRelationshipRequestsQuery, PagedResponse<FamilyRelationshipRequestQueueResponse>>
{
    public async Task<Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>> Handle(ListFamilyRelationshipRequestsQuery request, CancellationToken cancellationToken)
        => Result<PagedResponse<FamilyRelationshipRequestQueueResponse>>.Ok(await FamilyWorkflowMapper.ListAsync(
            dataStore, request.Status, request.RequestType, request.Search, null, null, request.PageNumber, request.PageSize, cancellationToken));
}

internal sealed class GetFamilyRelationshipRequestDetailsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetFamilyRelationshipRequestDetailsQuery, FamilyRelationshipRequestDetailsResponse>
{
    public async Task<Result<FamilyRelationshipRequestDetailsResponse>> Handle(GetFamilyRelationshipRequestDetailsQuery request, CancellationToken cancellationToken)
    {
        var record = await dataStore.FindFamilyRelationshipRequestAsync(request.RequestId, cancellationToken);
        if (record is null) return Result<FamilyRelationshipRequestDetailsResponse>.Fail(FamilyErrors.RequestNotFound);
        var access = await FamilyRequestAccess.AuthorizeAsync(dataStore, currentUser, record, request.AccessMode, cancellationToken);
        if (access.IsFailure) return Result<FamilyRelationshipRequestDetailsResponse>.Fail(access.Errors);
        var documents = await dataStore.ListFamilyRelationshipDocumentsAsync(request.RequestId, cancellationToken);
        var history = await dataStore.ListFamilyRelationshipRequestHistoryAsync(request.RequestId, cancellationToken);
        var members = record.Request.FamilyId is { } familyId
            ? await dataStore.ListActiveFamilyMembersAsync(familyId, cancellationToken)
            : [];
        return Result<FamilyRelationshipRequestDetailsResponse>.Ok(new(
            FamilyWorkflowMapper.Request(record.Request), FamilyWorkflowMapper.Patient(record.Requester), FamilyWorkflowMapper.Patient(record.Target),
            record.Request.RequesterClaimedRole, record.Request.TargetClaimedRole, record.Request.ModificationMessage, record.Request.RejectionReason,
            members.Select(FamilyWorkflowMapper.Member).ToArray(),
            documents.Select(FamilyWorkflowMapper.Document).ToArray(), history.Select(FamilyWorkflowMapper.History).ToArray()));
    }
}

internal sealed class GetFamilyRelationshipDocumentQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser, IPrivateMediaReader mediaReader)
    : IQueryHandler<GetFamilyRelationshipDocumentQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(GetFamilyRelationshipDocumentQuery request, CancellationToken cancellationToken)
    {
        var record = await dataStore.FindFamilyRelationshipRequestAsync(request.RequestId, cancellationToken);
        if (record is null) return Result<PrivateMedia>.Fail(FamilyErrors.RequestNotFound);
        var access = await FamilyRequestAccess.AuthorizeAsync(dataStore, currentUser, record, request.AccessMode, cancellationToken);
        if (access.IsFailure) return Result<PrivateMedia>.Fail(access.Errors);
        var document = await dataStore.FindFamilyRelationshipDocumentAsync(request.RequestId, request.DocumentId, cancellationToken);
        if (document is null) return Result<PrivateMedia>.Fail(FamilyErrors.DocumentNotFound);
        var media = await mediaReader.OpenAsync(document.MediaKey, cancellationToken);
        return media is null ? Result<PrivateMedia>.Fail(FamilyErrors.DocumentNotFound) : Result<PrivateMedia>.Ok(media with { FileName = document.OriginalFileName, ContentType = document.ContentType });
    }
}

internal sealed class FamilyRelationshipWorkflowService(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IMediaService mediaService,
    IDateTimeProvider clock,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox)
{
    public async Task<Result<FamilyRelationshipRequestResponse>> SubmitAsync(
        FamilyRelationshipRequestType type, Guid? familyId, Guid requesterPatientId, Guid targetPatientId,
        FamilyMemberRole requesterRole, FamilyMemberRole targetRole, IReadOnlyList<FamilyEvidenceUpload> evidence,
        Guid actorId, CancellationToken cancellationToken)
    {
        if (evidence.Count == 0) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.EvidenceRequired);
        var requester = await dataStore.FindPatientByIdAsync(requesterPatientId, cancellationToken);
        var target = await dataStore.FindPatientByIdAsync(targetPatientId, cancellationToken);
        if (requester is null) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.RequesterInvalid);
        if (target is null || requesterPatientId == targetPatientId) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.TargetInvalid);

        var requesterMembership = await dataStore.FindActiveFamilyMemberByPatientIdAsync(requesterPatientId, cancellationToken);
        var targetMembership = await dataStore.FindActiveFamilyMemberByPatientIdAsync(targetPatientId, cancellationToken);
        if (targetMembership is not null) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.PatientAlreadyInAnotherFamily);
        if (type == FamilyRelationshipRequestType.CreateFamily && requesterMembership is not null)
            return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.PatientAlreadyInAnotherFamily);
        if (type == FamilyRelationshipRequestType.AddFamilyMember)
        {
            if (familyId is null || requesterMembership is null || requesterMembership.FamilyId != familyId ||
                !requesterMembership.CanManageFamily || requesterMembership.Role != requesterRole)
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.ManagementForbidden);
            var family = await dataStore.FindFamilyByIdAsync(familyId.Value, cancellationToken);
            if (family is null || family.Status != FamilyStatus.Active)
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.NotFound);
            if (targetRole == FamilyMemberRole.Father && family.Members.Any(member => member.IsActive && member.Role == FamilyMemberRole.Father))
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.ActiveFatherAlreadyExists);
            if (targetRole == FamilyMemberRole.Mother && family.Members.Any(member => member.IsActive && member.Role == FamilyMemberRole.Mother))
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.ActiveMotherAlreadyExists);
        }
        if (await dataStore.FindOpenFamilyRelationshipRequestAsync(requesterPatientId, targetPatientId, type, familyId, cancellationToken) is not null)
            return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.OpenRequestAlreadyExists);

        var now = clock.UtcNow;
        var created = FamilyRelationshipRequest.Create(Guid.NewGuid(), type, familyId, requesterPatientId, targetPatientId,
            requesterRole, targetRole, actorId, now);
        if (created.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(created.Errors);
        var stored = new List<StoredMedia>();
        try
        {
            await SaveEvidenceAsync(created.Value.Id, 1, evidence, stored, cancellationToken);
            dataStore.Add(created.Value);
            AddDocuments(created.Value, evidence, stored, actorId, now);
            dataStore.Add(new FamilyRelationshipRequestHistory(Guid.NewGuid(), created.Value.Id, 1, null,
                FamilyRelationshipRequestStatus.Pending, FamilyRelationshipRequestAction.Submitted, null, actorId, now));
            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<FamilyRelationshipRequestResponse>.Ok(FamilyWorkflowMapper.Request(created.Value));
        }
        catch
        {
            await CleanupAsync(stored);
            throw;
        }
    }

    public async Task<Result<FamilyRelationshipRequestResponse>> ResubmitAsync(Guid requestId, IReadOnlyList<FamilyEvidenceUpload> evidence,
        string encodedRowVersion, Guid actorId, Guid? requesterPatientId, bool assisted, CancellationToken cancellationToken)
    {
        if (evidence.Count == 0) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.EvidenceRequired);
        var record = await dataStore.FindFamilyRelationshipRequestAsync(requestId, cancellationToken);
        if (record is null) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.RequestNotFound);
        if (assisted ? record.Request.SubmittedByApplicationUserId != actorId : record.Request.RequesterPatientId != requesterPatientId)
            return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.AccessForbidden);
        var supplied = VerifyRowVersion(record.Request, encodedRowVersion);
        if (supplied.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(supplied.Errors);
        var now = clock.UtcNow;
        var transitioned = record.Request.Resubmit(actorId, now);
        if (transitioned.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(transitioned.Errors);
        var stored = new List<StoredMedia>();
        try
        {
            await SaveEvidenceAsync(requestId, record.Request.CurrentRevisionNumber, evidence, stored, cancellationToken);
            AddDocuments(record.Request, evidence, stored, actorId, now);
            dataStore.Add(new FamilyRelationshipRequestHistory(Guid.NewGuid(), requestId, record.Request.CurrentRevisionNumber,
                FamilyRelationshipRequestStatus.ModificationRequested, FamilyRelationshipRequestStatus.Pending,
                FamilyRelationshipRequestAction.Resubmitted, null, actorId, now));
            dataStore.SetOriginalRowVersion(record.Request, supplied.Value);
            await dataStore.SaveChangesAsync(cancellationToken);
            return Result<FamilyRelationshipRequestResponse>.Ok(FamilyWorkflowMapper.Request(record.Request));
        }
        catch
        {
            await CleanupAsync(stored);
            throw;
        }
    }

    public async Task<Result<FamilyRelationshipRequestResponse>> ApproveAsync(Guid requestId, string encodedRowVersion, CancellationToken cancellationToken)
    {
        var context = await LoadReviewAsync(requestId, encodedRowVersion, cancellationToken);
        if (context.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(context.Errors);
        var (record, actorId, supplied) = context.Value;
        var request = record.Request;
        var now = clock.UtcNow;
        var targetMembership = await dataStore.FindActiveFamilyMemberByPatientIdAsync(request.TargetPatientId, cancellationToken);
        if (targetMembership is not null) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.PatientAlreadyInAnotherFamily);

        if (request.RequestType == FamilyRelationshipRequestType.CreateFamily)
        {
            if (await dataStore.FindActiveFamilyMemberByPatientIdAsync(request.RequesterPatientId, cancellationToken) is not null)
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.PatientAlreadyInAnotherFamily);
            var familyResult = Family.Create(Guid.NewGuid(), actorId);
            if (familyResult.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(familyResult.Errors);
            var family = familyResult.Value;
            var first = family.AddMember(Guid.NewGuid(), request.RequesterPatientId, request.RequesterClaimedRole, now, actorId, request.Id);
            if (first.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(first.Errors);
            var second = family.AddMember(Guid.NewGuid(), request.TargetPatientId, request.TargetClaimedRole, now, actorId, request.Id);
            if (second.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(second.Errors);
            var assigned = request.AssignCreatedFamily(family.Id);
            if (assigned.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(assigned.Errors);
            dataStore.Add(family);
        }
        else
        {
            if (request.FamilyId is not { } familyId) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.RequestInvalidState);
            var requesterMembership = await dataStore.FindActiveFamilyMemberByPatientIdAsync(request.RequesterPatientId, cancellationToken);
            if (requesterMembership is null || requesterMembership.FamilyId != familyId || !requesterMembership.CanManageFamily ||
                requesterMembership.Role != request.RequesterClaimedRole)
                return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.ManagementForbidden);
            var family = await dataStore.FindFamilyByIdAsync(familyId, cancellationToken);
            if (family is null) return Result<FamilyRelationshipRequestResponse>.Fail(FamilyErrors.NotFound);
            var added = family.AddMember(Guid.NewGuid(), request.TargetPatientId, request.TargetClaimedRole, now, actorId, request.Id);
            if (added.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(added.Errors);
            dataStore.MarkFamilyMembershipChanged(family);
        }

        var transition = request.Approve(actorId, now);
        if (transition.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(transition.Errors);
        dataStore.SetOriginalRowVersion(request, supplied);
        dataStore.Add(new FamilyRelationshipRequestHistory(Guid.NewGuid(), request.Id, request.CurrentRevisionNumber,
            FamilyRelationshipRequestStatus.Pending, FamilyRelationshipRequestStatus.Approved,
            FamilyRelationshipRequestAction.Approved, null, actorId, now));
        await QueueNotificationAsync(record, FamilyRelationshipRequestAction.Approved, null, cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<FamilyRelationshipRequestResponse>.Ok(FamilyWorkflowMapper.Request(request));
    }

    public async Task<Result<FamilyRelationshipRequestResponse>> ReviewAsync(Guid requestId, string encodedRowVersion, string message, bool modification, CancellationToken cancellationToken)
    {
        var context = await LoadReviewAsync(requestId, encodedRowVersion, cancellationToken);
        if (context.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(context.Errors);
        var (record, actorId, supplied) = context.Value;
        var request = record.Request;
        var oldStatus = request.Status;
        var now = clock.UtcNow;
        var transition = modification ? request.RequestModification(message, actorId, now) : request.Reject(message, actorId, now);
        if (transition.IsFailure) return Result<FamilyRelationshipRequestResponse>.Fail(transition.Errors);
        var action = modification ? FamilyRelationshipRequestAction.ModificationRequested : FamilyRelationshipRequestAction.Rejected;
        dataStore.SetOriginalRowVersion(request, supplied);
        dataStore.Add(new FamilyRelationshipRequestHistory(Guid.NewGuid(), request.Id, request.CurrentRevisionNumber, oldStatus,
            request.Status, action, message.Trim(), actorId, now));
        await QueueNotificationAsync(record, action, message.Trim(), cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<FamilyRelationshipRequestResponse>.Ok(FamilyWorkflowMapper.Request(request));
    }

    private async Task<Result<(FamilyRelationshipRequestRecord Record, Guid ActorId, byte[] Supplied)>> LoadReviewAsync(Guid requestId, string encodedRowVersion, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
            return Result<(FamilyRelationshipRequestRecord, Guid, byte[])>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        var record = await dataStore.FindFamilyRelationshipRequestAsync(requestId, cancellationToken);
        if (record is null) return Result<(FamilyRelationshipRequestRecord, Guid, byte[])>.Fail(FamilyErrors.RequestNotFound);
        var supplied = VerifyRowVersion(record.Request, encodedRowVersion);
        return supplied.IsFailure
            ? Result<(FamilyRelationshipRequestRecord, Guid, byte[])>.Fail(supplied.Errors)
            : Result<(FamilyRelationshipRequestRecord, Guid, byte[])>.Ok((record, actorId, supplied.Value));
    }

    private static Result<byte[]> VerifyRowVersion(FamilyRelationshipRequest request, string encoded)
    {
        var supplied = RowVersionCodec.Decode(encoded);
        return supplied is not null && request.RowVersion.AsSpan().SequenceEqual(supplied)
            ? Result<byte[]>.Ok(supplied) : Result<byte[]>.Fail(FamilyErrors.ConcurrencyConflict);
    }

    private async Task SaveEvidenceAsync(Guid requestId, int revision, IReadOnlyList<FamilyEvidenceUpload> evidence, List<StoredMedia> stored, CancellationToken cancellationToken)
    {
        foreach (var item in evidence)
            stored.Add(await mediaService.SaveAsync(item.File, new MediaStorageRequest($"FamilyRelationshipRequests/{requestId}/Revision-{revision}"), cancellationToken));
    }

    private void AddDocuments(FamilyRelationshipRequest request, IReadOnlyList<FamilyEvidenceUpload> evidence, List<StoredMedia> stored, Guid actorId, DateTime now)
    {
        for (var i = 0; i < evidence.Count; i++)
            dataStore.Add(new FamilyRelationshipDocument(Guid.NewGuid(), request.Id, request.CurrentRevisionNumber,
                evidence[i].DocumentType, stored[i].Key, evidence[i].File.FileName, stored[i].ContentType, stored[i].Length, actorId, now));
    }

    private async Task CleanupAsync(IEnumerable<StoredMedia> stored)
    {
        try { await mediaService.DeleteRangeAsync(stored.Select(item => item.Key), CancellationToken.None); }
        catch { }
    }

    private async Task QueueNotificationAsync(FamilyRelationshipRequestRecord record, FamilyRelationshipRequestAction action, string? message, CancellationToken cancellationToken)
    {
        var content = emailFactory.FamilyRelationshipStatus(record.Requester.NameAr, action, record.Request.CurrentRevisionNumber, message);
        await emailOutbox.QueueAsync(new QueueEmailMessage(
            $"family-relationship:{record.Request.Id}:revision-{record.Request.CurrentRevisionNumber}:{action}",
            record.Submitter.Email, content.Subject, content.HtmlBody, content.TextBody), cancellationToken);
    }
}

internal static class FamilyRequestAccess
{
    public static async Task<Result> AuthorizeAsync(IWaslaDataStore dataStore, ICurrentUser currentUser, FamilyRelationshipRequestRecord record, FamilyRelationshipAccessMode mode, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
            return Result.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        if (mode == FamilyRelationshipAccessMode.Assisted)
            return actorId == record.Request.SubmittedByApplicationUserId ? Result.Ok() : Result.Fail(FamilyErrors.AccessForbidden);
        if (mode == FamilyRelationshipAccessMode.Admin)
        {
            var snapshot = await dataStore.GetAccessSnapshotAsync(actorId, cancellationToken);
            return snapshot?.Permissions.Contains(PermissionNames.FamilyRelationshipRequestsViewDetails, StringComparer.OrdinalIgnoreCase) == true
                ? Result.Ok() : Result.Fail(FamilyErrors.AccessForbidden);
        }
        var link = await dataStore.FindPatientAccountLinkAsync(actorId, cancellationToken);
        return link is not null && (link.PatientId == record.Request.RequesterPatientId || link.PatientId == record.Request.TargetPatientId)
            ? Result.Ok() : Result.Fail(FamilyErrors.AccessForbidden);
    }
}

internal static class FamilyWorkflowMapper
{
    public static FamilyRelationshipRequestResponse Request(FamilyRelationshipRequest item)
        => new(item.Id, item.RequestType, item.Status, item.FamilyId, item.CurrentRevisionNumber, RowVersionCodec.Encode(item.RowVersion));
    public static FamilyRelationshipPatientResponse Patient(Patient item)
        => new(item.Id, item.NameAr, item.NameEn, item.DateOfBirth, item.Gender);
    public static FamilyMemberResponse Member(FamilyMemberViewRecord item)
        => new(item.Patient.Id, item.Patient.NameAr, item.Patient.NameEn, item.Member.Role, item.Patient.Gender);
    public static FamilyRelationshipDocumentResponse Document(FamilyRelationshipDocument item)
        => new(item.Id, item.RevisionNumber, item.DocumentType, item.OriginalFileName, item.ContentType, item.FileSize, item.UploadedOnUtc);
    public static FamilyRelationshipHistoryResponse History(FamilyRelationshipRequestHistory item)
        => new(item.Id, item.RevisionNumber, item.OldStatus, item.NewStatus, item.Action, item.MessageOrReason, item.PerformedByApplicationUserId, item.PerformedOnUtc);

    public static async Task<PagedResponse<FamilyRelationshipRequestQueueResponse>> ListAsync(
        IWaslaDataStore dataStore, FamilyRelationshipRequestStatus? status, FamilyRelationshipRequestType? type,
        string? search, Guid? submittedBy, Guid? relatedPatient, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await dataStore.ListFamilyRelationshipRequestsAsync(status, type, search, submittedBy, relatedPatient, pageNumber, pageSize, cancellationToken);
        return new(items.Select(item => new FamilyRelationshipRequestQueueResponse(
            item.RequestId, item.RequestType, item.Status, item.RequesterPatientId, item.RequesterNameAr,
            item.TargetPatientId, item.TargetNameAr, item.RequesterClaimedRole, item.TargetClaimedRole,
            item.CurrentRevisionNumber, item.SubmittedOnUtc, RowVersionCodec.Encode(item.RowVersion))).ToArray(), total, pageNumber, pageSize);
    }
}
