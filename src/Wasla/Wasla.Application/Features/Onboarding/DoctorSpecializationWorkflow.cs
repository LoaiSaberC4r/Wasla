using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Resources;
using static Wasla.Application.Features.Onboarding.DoctorSpecializationWorkflowHelpers;

namespace Wasla.Application.Features.Onboarding;

public sealed record SpecializationSelectionRequest(Guid MedicalSpecializationId, bool IsPrimary);
public sealed record MedicalSpecializationOptionResponse(Guid Id, string NameAr, string? NameEn);
public sealed record DoctorSpecializationResponse(Guid MedicalSpecializationId, string NameAr, string? NameEn, bool IsPrimary);
public sealed record DoctorSpecializationsResponse(IReadOnlyList<DoctorSpecializationResponse> Items);
public sealed record DoctorSpecializationRequestItemResponse(Guid MedicalSpecializationId, string NameAr, string? NameEn, bool IsPrimary);
public sealed record DoctorSpecializationHistoryResponse(
    Guid Id,
    DoctorSpecializationRequestAction Action,
    int RevisionNumber,
    int? PreviousRevisionNumber,
    string? Message,
    Guid PerformedByApplicationUserId,
    DateTime PerformedOnUtc);
public sealed record DoctorSpecializationRevisionResponse(
    Guid Id,
    int RevisionNumber,
    Guid SubmittedByApplicationUserId,
    DateTime SubmittedOnUtc);
public sealed record DoctorSpecializationRequestResponse(
    Guid RequestId,
    DoctorSpecializationRequestType Type,
    DoctorSpecializationRequestStatus Status,
    int CurrentRevisionNumber,
    IReadOnlyList<DoctorSpecializationRequestItemResponse> LatestRevision,
    string? LatestModificationMessage,
    string RowVersion);
public sealed record DoctorSpecializationRequestQueueResponse(
    Guid RequestId,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    string Email,
    DoctorSpecializationRequestType Type,
    DoctorSpecializationRequestStatus Status,
    int CurrentRevisionNumber,
    DateTime SubmittedOnUtc,
    string RowVersion);
public sealed record DoctorSpecializationRequestDetailsResponse(
    DoctorSpecializationRequestResponse Request,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    string Email,
    IReadOnlyList<DoctorSpecializationResponse> CurrentEffectiveSpecializations,
    IReadOnlyList<DoctorSpecializationRevisionResponse> Revisions,
    IReadOnlyList<DoctorSpecializationHistoryResponse> History);

public sealed record GetDoctorSpecializationOptionsQuery
    : ICacheableQuery<IReadOnlyList<MedicalSpecializationOptionResponse>>
{
    public string? CacheKey => "doctor-specialization-options:v1";
    public TimeSpan? TimeToLive => TimeSpan.FromHours(1);
    public IEnumerable<string> Tags => [OnboardingCacheTags.MedicalSpecializations];
}

public sealed record GetMyDoctorSpecializationsQuery : IQuery<DoctorSpecializationsResponse>;
public sealed record GetMyDoctorSpecializationRequestQuery : IQuery<DoctorSpecializationRequestResponse>;
public sealed record GetMyDoctorSpecializationRequestHistoryQuery : IQuery<IReadOnlyList<DoctorSpecializationHistoryResponse>>;
public sealed record SubmitDoctorSpecializationRequestCommand(IReadOnlyList<SpecializationSelectionRequest> Specializations)
    : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ResubmitDoctorSpecializationRequestCommand(
    IReadOnlyList<SpecializationSelectionRequest> Specializations,
    string RowVersion) : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListDoctorSpecializationRequestsQuery(
    DoctorSpecializationRequestStatus? Status,
    DoctorSpecializationRequestType? Type,
    string? Search,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<DoctorSpecializationRequestQueueResponse>>;
public sealed record GetDoctorSpecializationRequestDetailsQuery(Guid RequestId)
    : IQuery<DoctorSpecializationRequestDetailsResponse>;
public sealed record GetDoctorSpecializationRequestHistoryQuery(Guid RequestId)
    : IQuery<IReadOnlyList<DoctorSpecializationHistoryResponse>>;
public sealed record AdjustDoctorSpecializationRequestCommand(
    Guid RequestId,
    IReadOnlyList<SpecializationSelectionRequest> Specializations,
    string Reason,
    string RowVersion) : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ApproveDoctorSpecializationRequestCommand(Guid RequestId, string RowVersion)
    : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RequestDoctorSpecializationModificationCommand(Guid RequestId, string Message, string RowVersion)
    : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RejectDoctorSpecializationRequestCommand(Guid RequestId, string Reason, string RowVersion)
    : ICommand<DoctorSpecializationRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class SubmitDoctorSpecializationRequestCommandValidator : AbstractValidator<SubmitDoctorSpecializationRequestCommand>
{
    public SubmitDoctorSpecializationRequestCommandValidator()
        => RuleFor(command => command.Specializations).NotNull().NotEmpty();
}

internal sealed class ResubmitDoctorSpecializationRequestCommandValidator : AbstractValidator<ResubmitDoctorSpecializationRequestCommand>
{
    public ResubmitDoctorSpecializationRequestCommandValidator()
    {
        RuleFor(command => command.Specializations).NotNull().NotEmpty();
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListDoctorSpecializationRequestsQueryValidator : AbstractValidator<ListDoctorSpecializationRequestsQuery>
{
    public ListDoctorSpecializationRequestsQueryValidator()
    {
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.Type).IsInEnum().When(query => query.Type.HasValue);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class AdjustDoctorSpecializationRequestCommandValidator : AbstractValidator<AdjustDoctorSpecializationRequestCommand>
{
    public AdjustDoctorSpecializationRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Specializations).NotNull().NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ApproveDoctorSpecializationRequestCommandValidator : AbstractValidator<ApproveDoctorSpecializationRequestCommand>
{
    public ApproveDoctorSpecializationRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class RequestDoctorSpecializationModificationCommandValidator : AbstractValidator<RequestDoctorSpecializationModificationCommand>
{
    public RequestDoctorSpecializationModificationCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Message).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class RejectDoctorSpecializationRequestCommandValidator : AbstractValidator<RejectDoctorSpecializationRequestCommand>
{
    public RejectDoctorSpecializationRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
    }
}

internal sealed class GetDoctorSpecializationOptionsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetDoctorSpecializationOptionsQuery, IReadOnlyList<MedicalSpecializationOptionResponse>>
{
    public async Task<Result<IReadOnlyList<MedicalSpecializationOptionResponse>>> Handle(
        GetDoctorSpecializationOptionsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await dataStore.ListSelectableMedicalSpecializationsAsync(cancellationToken);
        return Result<IReadOnlyList<MedicalSpecializationOptionResponse>>.Ok(
            items.Select(item => new MedicalSpecializationOptionResponse(item.Id, item.NameAr, item.NameEn)).ToArray());
    }
}

internal sealed class GetMyDoctorSpecializationsQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorSpecializationsQuery, DoctorSpecializationsResponse>
{
    public async Task<Result<DoctorSpecializationsResponse>> Handle(
        GetMyDoctorSpecializationsQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorSpecializationsResponse>.Fail(doctor.Errors);
        }

        var items = await dataStore.ListDoctorSpecializationsAsync(doctor.Value.Id, cancellationToken);
        return Result<DoctorSpecializationsResponse>.Ok(new(items.Select(MapEffective).ToArray()));
    }
}

internal sealed class GetMyDoctorSpecializationRequestQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorSpecializationRequestQuery, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        GetMyDoctorSpecializationRequestQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(doctor.Errors);
        }

        var open = await dataStore.FindOpenDoctorSpecializationRequestAsync(doctor.Value.Id, cancellationToken);
        return open is null
            ? Result<DoctorSpecializationRequestResponse>.Fail(DoctorSpecializationErrors.RequestNotFound)
            : Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, open, cancellationToken));
    }
}

internal sealed class GetMyDoctorSpecializationRequestHistoryQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorSpecializationRequestHistoryQuery, IReadOnlyList<DoctorSpecializationHistoryResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>> Handle(
        GetMyDoctorSpecializationRequestHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>.Fail(doctor.Errors);
        }

        var latest = await dataStore.FindLatestDoctorSpecializationRequestAsync(doctor.Value.Id, cancellationToken);
        if (latest is null)
        {
            return Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>.Fail(DoctorSpecializationErrors.RequestNotFound);
        }

        return Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>.Ok(
            (await dataStore.ListDoctorSpecializationRequestHistoryAsync(latest.Id, cancellationToken)).Select(MapHistory).ToArray());
    }
}

internal sealed class SubmitDoctorSpecializationRequestCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<SubmitDoctorSpecializationRequestCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        SubmitDoctorSpecializationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(doctor.Errors);
        }

        if (await dataStore.FindOpenDoctorSpecializationRequestAsync(doctor.Value.Id, cancellationToken) is not null)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(DoctorSpecializationErrors.OpenRequestAlreadyExists);
        }

        var selections = request.Specializations.Select(ToDomain).ToArray();
        var validation = await ValidateSelectionsAsync(dataStore, selections, false, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(validation.Errors);
        }

        var current = await dataStore.ListDoctorSpecializationsAsync(doctor.Value.Id, cancellationToken);
        var type = current.Count == 0 ? DoctorSpecializationRequestType.Initial : DoctorSpecializationRequestType.Change;
        var now = clock.UtcNow;
        var created = DoctorSpecializationRequest.Create(Guid.NewGuid(), doctor.Value.Id, type, doctor.Value.ApplicationUserId, now);
        if (created.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(created.Errors);
        }

        dataStore.Add(created.Value);
        AddRevision(dataStore, created.Value, selections, doctor.Value.ApplicationUserId, now, DoctorSpecializationRequestAction.Submitted, null);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, created.Value, cancellationToken));
    }
}

internal sealed class ResubmitDoctorSpecializationRequestCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<ResubmitDoctorSpecializationRequestCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        ResubmitDoctorSpecializationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorOnboardingAccess.ResolveAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(doctor.Errors);
        }

        var open = await dataStore.FindOpenDoctorSpecializationRequestAsync(doctor.Value.Id, cancellationToken);
        if (open is null)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(DoctorSpecializationErrors.RequestNotFound);
        }

        var rowVersion = VerifyRequestRowVersion(open, request.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(rowVersion.Errors);
        }

        var selections = request.Specializations.Select(ToDomain).ToArray();
        var validation = await ValidateSelectionsAsync(dataStore, selections, false, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(validation.Errors);
        }

        var now = clock.UtcNow;
        var transition = open.Resubmit(doctor.Value.ApplicationUserId, now);
        if (transition.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(open, rowVersion.Value);
        AddRevision(dataStore, open, selections, doctor.Value.ApplicationUserId, now, DoctorSpecializationRequestAction.Resubmitted, null);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, open, cancellationToken));
    }
}

internal sealed class ListDoctorSpecializationRequestsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<ListDoctorSpecializationRequestsQuery, PagedResponse<DoctorSpecializationRequestQueueResponse>>
{
    public async Task<Result<PagedResponse<DoctorSpecializationRequestQueueResponse>>> Handle(
        ListDoctorSpecializationRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await dataStore.ListDoctorSpecializationRequestsAsync(
            request.Status, request.Type, request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return Result<PagedResponse<DoctorSpecializationRequestQueueResponse>>.Ok(new(
            items.Select(item => new DoctorSpecializationRequestQueueResponse(
                item.RequestId,
                item.DoctorId,
                item.DoctorNameAr,
                item.DoctorNameEn,
                item.Email,
                item.Type,
                item.Status,
                item.CurrentRevisionNumber,
                item.SubmittedOnUtc,
                RowVersionCodec.Encode(item.RowVersion))).ToArray(),
            total,
            request.PageNumber,
            request.PageSize));
    }
}

internal sealed class GetDoctorSpecializationRequestDetailsQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetDoctorSpecializationRequestDetailsQuery, DoctorSpecializationRequestDetailsResponse>
{
    public async Task<Result<DoctorSpecializationRequestDetailsResponse>> Handle(
        GetDoctorSpecializationRequestDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var owner = await dataStore.FindDoctorSpecializationRequestAsync(request.RequestId, cancellationToken);
        if (owner is null)
        {
            return Result<DoctorSpecializationRequestDetailsResponse>.Fail(DoctorSpecializationErrors.RequestNotFound);
        }

        var effective = await dataStore.ListDoctorSpecializationsAsync(owner.Doctor.Id, cancellationToken);
        var revisions = await dataStore.ListDoctorSpecializationRequestRevisionsAsync(owner.Request.Id, cancellationToken);
        var history = await dataStore.ListDoctorSpecializationRequestHistoryAsync(owner.Request.Id, cancellationToken);
        return Result<DoctorSpecializationRequestDetailsResponse>.Ok(new(
            await WorkflowMapper.MapRequestAsync(dataStore, owner.Request, cancellationToken),
            owner.Doctor.Id,
            owner.Doctor.NameAr,
            owner.Doctor.NameEn,
            owner.User.Email,
            effective.Select(MapEffective).ToArray(),
            revisions.Select(MapRevision).ToArray(),
            history.Select(MapHistory).ToArray()));
    }
}

internal sealed class GetDoctorSpecializationRequestHistoryQueryHandler(IWaslaDataStore dataStore)
    : IQueryHandler<GetDoctorSpecializationRequestHistoryQuery, IReadOnlyList<DoctorSpecializationHistoryResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>> Handle(
        GetDoctorSpecializationRequestHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await dataStore.FindDoctorSpecializationRequestAsync(request.RequestId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>.Fail(DoctorSpecializationErrors.RequestNotFound);
        }

        return Result<IReadOnlyList<DoctorSpecializationHistoryResponse>>.Ok(
            (await dataStore.ListDoctorSpecializationRequestHistoryAsync(request.RequestId, cancellationToken)).Select(MapHistory).ToArray());
    }
}

internal sealed class AdjustDoctorSpecializationRequestCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<AdjustDoctorSpecializationRequestCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        AdjustDoctorSpecializationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var context = await LoadAdminMutationAsync(dataStore, currentUser, request.RequestId, request.RowVersion, cancellationToken);
        if (context.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(context.Errors);
        }

        var selections = request.Specializations.Select(ToDomain).ToArray();
        var validation = await ValidateSelectionsAsync(dataStore, selections, false, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(validation.Errors);
        }

        var (owner, actorId, supplied) = context.Value;
        var now = clock.UtcNow;
        var transition = owner.Request.Adjust(actorId, now);
        if (transition.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(owner.Request, supplied);
        AddRevision(dataStore, owner.Request, selections, actorId, now,
            DoctorSpecializationRequestAction.SpecializationsAdjustedBySuperAdmin, request.Reason.Trim());
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, owner.Request, cancellationToken));
    }
}

internal sealed class ApproveDoctorSpecializationRequestCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<ApproveDoctorSpecializationRequestCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        ApproveDoctorSpecializationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var context = await LoadAdminMutationAsync(dataStore, currentUser, request.RequestId, request.RowVersion, cancellationToken);
        if (context.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(context.Errors);
        }

        var (owner, actorId, supplied) = context.Value;
        var items = await dataStore.ListDoctorSpecializationRequestItemsAsync(
            owner.Request.Id, owner.Request.CurrentRevisionNumber, cancellationToken);
        var selections = items.Select(item => new DoctorSpecializationSelection(item.MedicalSpecializationId, item.IsPrimary)).ToArray();
        var validation = await ValidateSelectionsAsync(dataStore, selections, true, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(validation.Errors);
        }

        var now = clock.UtcNow;
        var transition = owner.Request.Approve(actorId, now);
        if (transition.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(transition.Errors);
        }

        var replacements = selections.Select(item => new DoctorSpecialization(
            Guid.NewGuid(), owner.Doctor.Id, item.MedicalSpecializationId, item.IsPrimary, actorId, now)).ToArray();
        await dataStore.ReplaceDoctorSpecializationsAsync(owner.Doctor.Id, replacements, cancellationToken);
        dataStore.SetOriginalRowVersion(owner.Request, supplied);
        dataStore.Add(new DoctorSpecializationRequestHistory(
            Guid.NewGuid(), owner.Request.Id, DoctorSpecializationRequestAction.Approved,
            owner.Request.CurrentRevisionNumber, null, actorId, now));
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, owner.Request, cancellationToken));
    }
}

internal sealed class RequestDoctorSpecializationModificationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox)
    : ICommandHandler<RequestDoctorSpecializationModificationCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        RequestDoctorSpecializationModificationCommand request,
        CancellationToken cancellationToken)
    {
        var context = await LoadAdminMutationAsync(dataStore, currentUser, request.RequestId, request.RowVersion, cancellationToken);
        if (context.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(context.Errors);
        }

        var (owner, actorId, supplied) = context.Value;
        var now = clock.UtcNow;
        var message = request.Message.Trim();
        var transition = owner.Request.RequestModification(actorId, now);
        if (transition.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(owner.Request, supplied);
        dataStore.Add(new DoctorSpecializationRequestHistory(
            Guid.NewGuid(), owner.Request.Id, DoctorSpecializationRequestAction.ModificationRequested,
            owner.Request.CurrentRevisionNumber, message, actorId, now));
        var email = emailFactory.DoctorSpecializationModificationRequested(
            owner.Doctor.NameAr, message, owner.Request.CurrentRevisionNumber);
        await emailOutbox.QueueAsync(new QueueEmailMessage(
            $"doctor-specialization-modification:{owner.Request.Id}:revision-{owner.Request.CurrentRevisionNumber}",
            owner.User.Email,
            email.Subject,
            email.HtmlBody,
            email.TextBody), cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, owner.Request, cancellationToken));
    }
}

internal sealed class RejectDoctorSpecializationRequestCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<RejectDoctorSpecializationRequestCommand, DoctorSpecializationRequestResponse>
{
    public async Task<Result<DoctorSpecializationRequestResponse>> Handle(
        RejectDoctorSpecializationRequestCommand request,
        CancellationToken cancellationToken)
    {
        var context = await LoadAdminMutationAsync(dataStore, currentUser, request.RequestId, request.RowVersion, cancellationToken);
        if (context.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(context.Errors);
        }

        var (owner, actorId, supplied) = context.Value;
        var now = clock.UtcNow;
        var transition = owner.Request.Reject(actorId, now);
        if (transition.IsFailure)
        {
            return Result<DoctorSpecializationRequestResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(owner.Request, supplied);
        dataStore.Add(new DoctorSpecializationRequestHistory(
            Guid.NewGuid(), owner.Request.Id, DoctorSpecializationRequestAction.Rejected,
            owner.Request.CurrentRevisionNumber, request.Reason.Trim(), actorId, now));
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorSpecializationRequestResponse>.Ok(await WorkflowMapper.MapRequestAsync(dataStore, owner.Request, cancellationToken));
    }
}

internal static class DoctorOnboardingAccess
{
    public static async Task<Result<Doctor>> ResolveAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<Doctor>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        }

        var doctor = await dataStore.FindDoctorByUserIdAsync(userId, cancellationToken);
        if (doctor is null)
        {
            return Result<Doctor>.Fail(DoctorErrors.NotFound);
        }

        return doctor.ApprovalStatus is DoctorApprovalStatus.Pending or DoctorApprovalStatus.Approved
            ? Result<Doctor>.Ok(doctor)
            : Result<Doctor>.Fail(Error.Security("Doctor.OnboardingUnavailable", ErrorMessage.DoctorOnboardingUnavailable));
    }
}

internal static class WorkflowMapper
{
    public static async Task<DoctorSpecializationRequestResponse> MapRequestAsync(
        IWaslaDataStore dataStore,
        DoctorSpecializationRequest request,
        CancellationToken cancellationToken)
    {
        var items = await dataStore.ListDoctorSpecializationRequestItemsAsync(
            request.Id, request.CurrentRevisionNumber, cancellationToken);
        var histories = await dataStore.ListDoctorSpecializationRequestHistoryAsync(request.Id, cancellationToken);
        var latestMessage = histories.LastOrDefault(item =>
            item.Action == DoctorSpecializationRequestAction.ModificationRequested &&
            item.RevisionNumber == request.CurrentRevisionNumber)?.Message;
        return new DoctorSpecializationRequestResponse(
            request.Id,
            request.Type,
            request.Status,
            request.CurrentRevisionNumber,
            items.Select(item => new DoctorSpecializationRequestItemResponse(
                item.MedicalSpecializationId, item.NameAr, item.NameEn, item.IsPrimary)).ToArray(),
            latestMessage,
            RowVersionCodec.Encode(request.RowVersion));
    }
}

internal static class DoctorSpecializationWorkflowHelpers
{
    public static DoctorSpecializationSelection ToDomain(SpecializationSelectionRequest item)
        => new(item.MedicalSpecializationId, item.IsPrimary);

    public static DoctorSpecializationResponse MapEffective(DoctorSpecializationViewRecord item)
        => new(item.MedicalSpecializationId, item.NameAr, item.NameEn, item.IsPrimary);

    public static DoctorSpecializationHistoryResponse MapHistory(DoctorSpecializationRequestHistory item)
        => new(item.Id, item.Action, item.RevisionNumber, item.PreviousRevisionNumber, item.Message, item.PerformedByApplicationUserId, item.PerformedOnUtc);

    public static DoctorSpecializationRevisionResponse MapRevision(DoctorSpecializationRequestRevision item)
        => new(item.Id, item.RevisionNumber, item.SubmittedByApplicationUserId, item.SubmittedOnUtc);

    public static async Task<Result> ValidateSelectionsAsync(
        IWaslaDataStore dataStore,
        IReadOnlyCollection<DoctorSpecializationSelection> selections,
        bool approval,
        CancellationToken cancellationToken)
    {
        var domain = DoctorSpecializationSet.Validate(selections);
        if (domain.IsFailure)
        {
            return domain;
        }

        var ids = selections.Select(item => item.MedicalSpecializationId).ToArray();
        var available = await dataStore.ListAvailableMedicalSpecializationIdsAsync(ids, cancellationToken);
        return available.Count == ids.Length
            ? Result.Ok()
            : Result.Fail(approval
                ? DoctorSpecializationErrors.SpecializationNoLongerAvailable
                : DoctorSpecializationErrors.SpecializationNotAvailable);
    }

    public static Result<byte[]> VerifyRequestRowVersion(DoctorSpecializationRequest request, string encoded)
    {
        var supplied = RowVersionCodec.Decode(encoded);
        return supplied is not null && request.RowVersion.AsSpan().SequenceEqual(supplied)
            ? Result<byte[]>.Ok(supplied)
            : Result<byte[]>.Fail(DoctorSpecializationErrors.ConcurrencyConflict);
    }

    public static void AddRevision(
        IWaslaDataStore dataStore,
        DoctorSpecializationRequest request,
        IReadOnlyCollection<DoctorSpecializationSelection> selections,
        Guid actorId,
        DateTime occurredOnUtc,
        DoctorSpecializationRequestAction action,
        string? message)
    {
        var revisionId = Guid.NewGuid();
        dataStore.Add(new DoctorSpecializationRequestRevision(
            revisionId, request.Id, request.CurrentRevisionNumber, actorId, occurredOnUtc));
        foreach (var selection in selections)
        {
            dataStore.Add(new DoctorSpecializationRequestItem(
                Guid.NewGuid(), revisionId, selection.MedicalSpecializationId, selection.IsPrimary));
        }
        dataStore.Add(new DoctorSpecializationRequestHistory(
            Guid.NewGuid(), request.Id, action, request.CurrentRevisionNumber, message, actorId, occurredOnUtc,
            action == DoctorSpecializationRequestAction.SpecializationsAdjustedBySuperAdmin
                ? request.CurrentRevisionNumber - 1
                : null));
    }

    public static async Task<Result<(DoctorSpecializationRequestOwnerRecord Owner, Guid ActorId, byte[] Supplied)>> LoadAdminMutationAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        Guid requestId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<(DoctorSpecializationRequestOwnerRecord, Guid, byte[])>.Fail(
                Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
        }

        var owner = await dataStore.FindDoctorSpecializationRequestAsync(requestId, cancellationToken);
        if (owner is null)
        {
            return Result<(DoctorSpecializationRequestOwnerRecord, Guid, byte[])>.Fail(DoctorSpecializationErrors.RequestNotFound);
        }

        var supplied = VerifyRequestRowVersion(owner.Request, rowVersion);
        return supplied.IsFailure
            ? Result<(DoctorSpecializationRequestOwnerRecord, Guid, byte[])>.Fail(supplied.Errors)
            : Result<(DoctorSpecializationRequestOwnerRecord, Guid, byte[])>.Ok((owner, actorId, supplied.Value));
    }
}
