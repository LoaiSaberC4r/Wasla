using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Doctors;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize);

public sealed record ListDoctorsQuery(
    DoctorApprovalStatus? ApprovalStatus,
    string? SearchText,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResponse<DoctorListItem>>;

public sealed record DoctorListItem(
    Guid DoctorId,
    string NameAr,
    string? NameEn,
    string Email,
    string? Phone,
    DateOnly DateOfBirth,
    int Age,
    Gender Gender,
    DoctorApprovalStatus ApprovalStatus,
    bool HasProfileImage,
    bool HasPersonalIdFront,
    bool HasPersonalIdBack,
    bool HasSyndicateFront,
    bool HasSyndicateBack,
    DateTime CreatedOnUtc);

public sealed record GetDoctorDetailsQuery(Guid DoctorId) : IQuery<DoctorDetailsResponse>;

public sealed record DoctorDetailsResponse(
    Guid DoctorId,
    Guid ApplicationUserId,
    string UserName,
    string NameAr,
    string? NameEn,
    string Email,
    string? Phone,
    DateOnly DateOfBirth,
    int Age,
    Gender Gender,
    DoctorApprovalStatus ApprovalStatus,
    string? NationalId,
    bool HasProfileImage,
    bool HasPersonalIdFront,
    bool HasPersonalIdBack,
    bool HasSyndicateFront,
    bool HasSyndicateBack,
    Guid? ApprovedByApplicationUserId,
    DateTime? ApprovedOnUtc,
    Guid? RejectedByApplicationUserId,
    DateTime? RejectedOnUtc,
    string? RejectionReason,
    Guid? SuspendedByApplicationUserId,
    DateTime? SuspendedOnUtc,
    string? SuspensionReason,
    Guid? ReactivatedByApplicationUserId,
    DateTime? ReactivatedOnUtc,
    string RowVersion);

public enum DoctorMediaType
{
    ProfileImage = 1,
    PersonalIdFront = 2,
    PersonalIdBack = 3,
    SyndicateCardFront = 4,
    SyndicateCardBack = 5
}

public sealed record GetDoctorMediaQuery(Guid DoctorId, DoctorMediaType MediaType) : IQuery<PrivateMedia>;

public sealed record GetDoctorOnboardingQuery : IQuery<DoctorOnboardingResponse>;

public sealed record DoctorOnboardingResponse(
    Guid DoctorId,
    DoctorApprovalStatus ApprovalStatus,
    string? RejectionReason,
    string? SuspensionReason,
    DateTime? ApprovedOnUtc,
    bool HasProfileImage,
    string RowVersion);

public sealed record ApproveDoctorCommand(Guid DoctorId, string NationalId, string RowVersion)
    : ICommand<DoctorLifecycleResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record RejectDoctorCommand(Guid DoctorId, string Reason, string RowVersion)
    : ICommand<DoctorLifecycleResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record SuspendDoctorCommand(Guid DoctorId, string Reason, string RowVersion)
    : ICommand<DoctorLifecycleResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ReactivateDoctorCommand(Guid DoctorId, string RowVersion)
    : ICommand<DoctorLifecycleResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DoctorLifecycleResponse(Guid DoctorId, DoctorApprovalStatus ApprovalStatus, string RowVersion);

internal sealed class ListDoctorsQueryValidator : AbstractValidator<ListDoctorsQuery>
{
    public ListDoctorsQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ApprovalStatus).IsInEnum().When(query => query.ApprovalStatus.HasValue);
        RuleFor(query => query.SearchText).MaximumLength(200);
    }
}

internal sealed class ApproveDoctorCommandValidator : AbstractValidator<ApproveDoctorCommand>
{
    public ApproveDoctorCommandValidator()
    {
        RuleFor(command => command.DoctorId).NotEmpty();
        RuleFor(command => command.NationalId).NotEmpty().WithMessage(ErrorMessage.DoctorNationalIdRequired).MaximumLength(100);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class RejectDoctorCommandValidator : AbstractValidator<RejectDoctorCommand>
{
    public RejectDoctorCommandValidator()
    {
        RuleFor(command => command.Reason).NotEmpty().WithMessage(ErrorMessage.ReasonRequired).MaximumLength(1000);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class SuspendDoctorCommandValidator : AbstractValidator<SuspendDoctorCommand>
{
    public SuspendDoctorCommandValidator()
    {
        RuleFor(command => command.Reason).NotEmpty().WithMessage(ErrorMessage.ReasonRequired).MaximumLength(1000);
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class ReactivateDoctorCommandValidator : AbstractValidator<ReactivateDoctorCommand>
{
    public ReactivateDoctorCommandValidator()
    {
        RuleFor(command => command.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid).WithMessage(ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class ListDoctorsQueryHandler(IWaslaDataStore dataStore, IDateTimeProvider clock)
    : IQueryHandler<ListDoctorsQuery, PagedResponse<DoctorListItem>>
{
    public async Task<Result<PagedResponse<DoctorListItem>>> Handle(
        ListDoctorsQuery request,
        CancellationToken cancellationToken)
    {
        var (records, totalCount) = await dataStore.ListDoctorsAsync(
            request.ApprovalStatus,
            request.SearchText,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var items = records.Select(record => new DoctorListItem(
            record.Doctor.Id,
            record.Doctor.NameAr,
            record.Doctor.NameEn,
            record.User.Email,
            record.User.PhoneNumber,
            record.Doctor.DateOfBirth,
            record.Doctor.GetAge(today),
            record.Doctor.Gender,
            record.Doctor.ApprovalStatus,
            record.Doctor.ProfileImageMediaKey is not null,
            !string.IsNullOrWhiteSpace(record.Doctor.PersonalIdFrontMediaKey),
            !string.IsNullOrWhiteSpace(record.Doctor.PersonalIdBackMediaKey),
            !string.IsNullOrWhiteSpace(record.Doctor.SyndicateCardFrontMediaKey),
            record.Doctor.SyndicateCardBackMediaKey is not null,
            record.Doctor.CreatedOnUtc)).ToArray();
        return Result<PagedResponse<DoctorListItem>>.Ok(new PagedResponse<DoctorListItem>(
            items,
            totalCount,
            request.PageNumber,
            request.PageSize));
    }
}

internal sealed class GetDoctorDetailsQueryHandler(IWaslaDataStore dataStore, IDateTimeProvider clock)
    : IQueryHandler<GetDoctorDetailsQuery, DoctorDetailsResponse>
{
    public async Task<Result<DoctorDetailsResponse>> Handle(
        GetDoctorDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await dataStore.FindDoctorByIdAsync(request.DoctorId, cancellationToken);
        if (doctor is null)
        {
            return Result<DoctorDetailsResponse>.Fail(DoctorErrors.NotFound);
        }

        var user = await dataStore.FindUserByIdAsync(doctor.ApplicationUserId, cancellationToken);
        if (user is null)
        {
            return Result<DoctorDetailsResponse>.Fail(DoctorErrors.NotFound);
        }

        return Result<DoctorDetailsResponse>.Ok(DoctorMapper.ToDetails(
            doctor,
            user,
            DateOnly.FromDateTime(clock.UtcNow)));
    }
}

internal sealed class GetDoctorMediaQueryHandler(
    IWaslaDataStore dataStore,
    IPrivateMediaReader mediaReader)
    : IQueryHandler<GetDoctorMediaQuery, PrivateMedia>
{
    public async Task<Result<PrivateMedia>> Handle(
        GetDoctorMediaQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await dataStore.FindDoctorByIdAsync(request.DoctorId, cancellationToken);
        if (doctor is null)
        {
            return Result<PrivateMedia>.Fail(DoctorErrors.NotFound);
        }

        var key = request.MediaType switch
        {
            DoctorMediaType.ProfileImage => doctor.ProfileImageMediaKey,
            DoctorMediaType.PersonalIdFront => doctor.PersonalIdFrontMediaKey,
            DoctorMediaType.PersonalIdBack => doctor.PersonalIdBackMediaKey,
            DoctorMediaType.SyndicateCardFront => doctor.SyndicateCardFrontMediaKey,
            DoctorMediaType.SyndicateCardBack => doctor.SyndicateCardBackMediaKey,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<PrivateMedia>.Fail(DoctorErrors.MediaNotFound);
        }

        var media = await mediaReader.OpenAsync(key, cancellationToken);
        return media is null
            ? Result<PrivateMedia>.Fail(DoctorErrors.MediaNotFound)
            : Result<PrivateMedia>.Ok(media);
    }
}

internal sealed class GetDoctorOnboardingQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetDoctorOnboardingQuery, DoctorOnboardingResponse>
{
    public async Task<Result<DoctorOnboardingResponse>> Handle(
        GetDoctorOnboardingQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<DoctorOnboardingResponse>.Fail(Error.Unauthorized(
                "Auth.AuthenticationRequired",
                ErrorMessage.AuthenticationRequired));
        }

        var doctor = await dataStore.FindDoctorByUserIdAsync(userId, cancellationToken);
        if (doctor is null)
        {
            return Result<DoctorOnboardingResponse>.Fail(DoctorErrors.NotFound);
        }

        return Result<DoctorOnboardingResponse>.Ok(new DoctorOnboardingResponse(
            doctor.Id,
            doctor.ApprovalStatus,
            doctor.RejectionReason,
            doctor.SuspensionReason,
            doctor.ApprovedOnUtc,
            doctor.ProfileImageMediaKey is not null,
            RowVersionCodec.Encode(doctor.RowVersion)));
    }
}

internal sealed class ApproveDoctorCommandHandler(DoctorLifecycleService service)
    : ICommandHandler<ApproveDoctorCommand, DoctorLifecycleResponse>
{
    public Task<Result<DoctorLifecycleResponse>> Handle(ApproveDoctorCommand request, CancellationToken cancellationToken)
        => service.ExecuteAsync(
            request.DoctorId,
            request.RowVersion,
            DoctorEmailEvent.Approved,
            request.NationalId,
            null,
            cancellationToken);
}

internal sealed class RejectDoctorCommandHandler(DoctorLifecycleService service)
    : ICommandHandler<RejectDoctorCommand, DoctorLifecycleResponse>
{
    public Task<Result<DoctorLifecycleResponse>> Handle(RejectDoctorCommand request, CancellationToken cancellationToken)
        => service.ExecuteAsync(
            request.DoctorId,
            request.RowVersion,
            DoctorEmailEvent.Rejected,
            null,
            request.Reason,
            cancellationToken);
}

internal sealed class SuspendDoctorCommandHandler(DoctorLifecycleService service)
    : ICommandHandler<SuspendDoctorCommand, DoctorLifecycleResponse>
{
    public Task<Result<DoctorLifecycleResponse>> Handle(SuspendDoctorCommand request, CancellationToken cancellationToken)
        => service.ExecuteAsync(
            request.DoctorId,
            request.RowVersion,
            DoctorEmailEvent.Suspended,
            null,
            request.Reason,
            cancellationToken);
}

internal sealed class ReactivateDoctorCommandHandler(DoctorLifecycleService service)
    : ICommandHandler<ReactivateDoctorCommand, DoctorLifecycleResponse>
{
    public Task<Result<DoctorLifecycleResponse>> Handle(ReactivateDoctorCommand request, CancellationToken cancellationToken)
        => service.ExecuteAsync(
            request.DoctorId,
            request.RowVersion,
            DoctorEmailEvent.Reactivated,
            null,
            null,
            cancellationToken);
}

internal sealed class DoctorLifecycleService(
    IWaslaDataStore dataStore,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
{
    public async Task<Result<DoctorLifecycleResponse>> ExecuteAsync(
        Guid doctorId,
        string encodedRowVersion,
        DoctorEmailEvent emailEvent,
        string? nationalId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId)
        {
            return Result<DoctorLifecycleResponse>.Fail(Error.Unauthorized(
                "Auth.AuthenticationRequired",
                ErrorMessage.AuthenticationRequired));
        }

        var doctor = await dataStore.FindDoctorByIdAsync(doctorId, cancellationToken);
        if (doctor is null)
        {
            return Result<DoctorLifecycleResponse>.Fail(DoctorErrors.NotFound);
        }

        var suppliedRowVersion = RowVersionCodec.Decode(encodedRowVersion);
        if (suppliedRowVersion is null || !doctor.RowVersion.AsSpan().SequenceEqual(suppliedRowVersion))
        {
            return Result<DoctorLifecycleResponse>.Fail(DoctorErrors.ConcurrencyConflict);
        }

        if (emailEvent == DoctorEmailEvent.Approved &&
            await dataStore.NationalIdExistsAsync(nationalId!.Trim(), doctor.Id, cancellationToken))
        {
            return Result<DoctorLifecycleResponse>.Fail(DoctorErrors.NationalIdAlreadyExists);
        }

        var fromStatus = doctor.ApprovalStatus;
        var now = clock.UtcNow;
        var transition = emailEvent switch
        {
            DoctorEmailEvent.Approved => doctor.Approve(nationalId!, actorId, now),
            DoctorEmailEvent.Rejected => doctor.Reject(reason!, actorId, now),
            DoctorEmailEvent.Suspended => doctor.Suspend(reason!, actorId, now),
            DoctorEmailEvent.Reactivated => doctor.Reactivate(actorId, now),
            _ => Result.Fail(DoctorErrors.InvalidStatus)
        };
        if (transition.IsFailure)
        {
            return Result<DoctorLifecycleResponse>.Fail(transition.Errors);
        }

        dataStore.SetOriginalRowVersion(doctor, suppliedRowVersion);
        var historyId = Guid.NewGuid();
        dataStore.Add(new DoctorStatusHistory(
            historyId,
            doctor.Id,
            fromStatus,
            doctor.ApprovalStatus,
            reason,
            actorId,
            now));

        var user = await dataStore.FindUserByIdAsync(doctor.ApplicationUserId, cancellationToken);
        if (user is null)
        {
            return Result<DoctorLifecycleResponse>.Fail(DoctorErrors.NotFound);
        }

        var email = emailFactory.DoctorLifecycle(emailEvent, doctor.NameAr, reason);
        var eventName = emailEvent switch
        {
            DoctorEmailEvent.Approved => "approved",
            DoctorEmailEvent.Rejected => "rejected",
            DoctorEmailEvent.Suspended => "suspended",
            DoctorEmailEvent.Reactivated => "reactivated",
            _ => throw new InvalidOperationException("Unsupported doctor lifecycle email event.")
        };
        await emailOutbox.QueueAsync(new QueueEmailMessage(
            $"doctor-{eventName}:{doctor.Id}:{historyId}",
            user.Email,
            email.Subject,
            email.HtmlBody,
            email.TextBody), cancellationToken);
        await dataStore.SaveChangesAsync(cancellationToken);

        return Result<DoctorLifecycleResponse>.Ok(new DoctorLifecycleResponse(
            doctor.Id,
            doctor.ApprovalStatus,
            RowVersionCodec.Encode(doctor.RowVersion)));
    }
}

internal static class DoctorMapper
{
    public static DoctorDetailsResponse ToDetails(
        Doctor doctor,
        ApplicationUser user,
        DateOnly today)
        => new(
            doctor.Id,
            user.Id,
            user.UserName,
            doctor.NameAr,
            doctor.NameEn,
            user.Email,
            user.PhoneNumber,
            doctor.DateOfBirth,
            doctor.GetAge(today),
            doctor.Gender,
            doctor.ApprovalStatus,
            doctor.NationalId,
            doctor.ProfileImageMediaKey is not null,
            !string.IsNullOrWhiteSpace(doctor.PersonalIdFrontMediaKey),
            !string.IsNullOrWhiteSpace(doctor.PersonalIdBackMediaKey),
            !string.IsNullOrWhiteSpace(doctor.SyndicateCardFrontMediaKey),
            doctor.SyndicateCardBackMediaKey is not null,
            doctor.ApprovedByApplicationUserId,
            doctor.ApprovedOnUtc,
            doctor.RejectedByApplicationUserId,
            doctor.RejectedOnUtc,
            doctor.RejectionReason,
            doctor.SuspendedByApplicationUserId,
            doctor.SuspendedOnUtc,
            doctor.SuspensionReason,
            doctor.ReactivatedByApplicationUserId,
            doctor.ReactivatedOnUtc,
            RowVersionCodec.Encode(doctor.RowVersion));
}

internal static class RowVersionCodec
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[]? Decode(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static bool IsValid(string value) => Decode(value) is { Length: > 0 };
}
