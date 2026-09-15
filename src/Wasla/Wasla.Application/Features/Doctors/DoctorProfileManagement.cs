using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Features.Practices;
using Wasla.Application.Persistence;
using Wasla.Domain.Doctors;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.Doctors;

public sealed record DoctorPublicProfileManagementResponse(
    Guid DoctorId,
    string? Bio,
    string RowVersion);

public sealed record DoctorQualificationResponse(
    Guid Id,
    string NameAr,
    string? NameEn,
    int DisplayOrder,
    string RowVersion);

public sealed record GetMyDoctorPublicProfileQuery : IQuery<DoctorPublicProfileManagementResponse>;

public sealed record UpdateMyDoctorBioCommand(string? Bio, string RowVersion)
    : ICommand<DoctorPublicProfileManagementResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record ListMyDoctorQualificationsQuery : IQuery<IReadOnlyList<DoctorQualificationResponse>>;

public sealed record CreateMyDoctorQualificationCommand(string NameAr, string? NameEn, int DisplayOrder)
    : ICommand<DoctorQualificationResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record UpdateMyDoctorQualificationCommand(
    Guid QualificationId,
    string NameAr,
    string? NameEn,
    int DisplayOrder,
    string RowVersion)
    : ICommand<DoctorQualificationResponse>, ITransactionalCommand<WaslaWritePersistence>;

public sealed record DeleteMyDoctorQualificationCommand(Guid QualificationId, string RowVersion)
    : ICommand, ITransactionalCommand<WaslaWritePersistence>;

internal sealed class UpdateMyDoctorBioCommandValidator : AbstractValidator<UpdateMyDoctorBioCommand>
{
    public UpdateMyDoctorBioCommandValidator()
    {
        RuleFor(command => command.Bio).MaximumLength(2000);
        RuleFor(command => command.Bio).Must(value => value is null ||
            (!value.Contains('<', StringComparison.Ordinal) &&
             !value.Contains('>', StringComparison.Ordinal)))
            .WithMessage(ErrorMessage.DoctorInvalidBio);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class CreateMyDoctorQualificationCommandValidator
    : AbstractValidator<CreateMyDoctorQualificationCommand>
{
    public CreateMyDoctorQualificationCommandValidator()
    {
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(300);
        RuleFor(command => command.NameEn).MaximumLength(300);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(0, 10000);
    }
}

internal sealed class UpdateMyDoctorQualificationCommandValidator
    : AbstractValidator<UpdateMyDoctorQualificationCommand>
{
    public UpdateMyDoctorQualificationCommandValidator()
    {
        RuleFor(command => command.QualificationId).NotEmpty();
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(300);
        RuleFor(command => command.NameEn).MaximumLength(300);
        RuleFor(command => command.DisplayOrder).InclusiveBetween(0, 10000);
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeleteMyDoctorQualificationCommandValidator
    : AbstractValidator<DeleteMyDoctorQualificationCommand>
{
    public DeleteMyDoctorQualificationCommandValidator()
    {
        RuleFor(command => command.QualificationId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class GetMyDoctorPublicProfileQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<GetMyDoctorPublicProfileQuery, DoctorPublicProfileManagementResponse>
{
    public async Task<Result<DoctorPublicProfileManagementResponse>> Handle(
        GetMyDoctorPublicProfileQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        return doctor.IsFailure
            ? Result<DoctorPublicProfileManagementResponse>.Fail(doctor.Errors)
            : Result<DoctorPublicProfileManagementResponse>.Ok(Map(doctor.Value.Doctor));
    }

    internal static DoctorPublicProfileManagementResponse Map(Doctor doctor)
        => new(doctor.Id, doctor.Bio, RowVersionCodec.Encode(doctor.RowVersion));
}

internal sealed class UpdateMyDoctorBioCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateMyDoctorBioCommand, DoctorPublicProfileManagementResponse>
{
    public async Task<Result<DoctorPublicProfileManagementResponse>> Handle(
        UpdateMyDoctorBioCommand request,
        CancellationToken cancellationToken)
    {
        var resolved = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<DoctorPublicProfileManagementResponse>.Fail(resolved.Errors);
        }

        var doctor = await dataStore.FindDoctorByIdAsync(resolved.Value.Doctor.Id, cancellationToken)
            ?? throw new InvalidOperationException("The current Doctor could not be reloaded.");
        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            doctor.RowVersion, request.RowVersion, DoctorErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorPublicProfileManagementResponse>.Fail(supplied.Errors);
        }

        var updated = doctor.UpdateBio(request.Bio, resolved.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorPublicProfileManagementResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(doctor, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorPublicProfileManagementResponse>.Ok(
            GetMyDoctorPublicProfileQueryHandler.Map(doctor));
    }
}

internal sealed class ListMyDoctorQualificationsQueryHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : IQueryHandler<ListMyDoctorQualificationsQuery, IReadOnlyList<DoctorQualificationResponse>>
{
    public async Task<Result<IReadOnlyList<DoctorQualificationResponse>>> Handle(
        ListMyDoctorQualificationsQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<IReadOnlyList<DoctorQualificationResponse>>.Fail(doctor.Errors);
        }

        var items = await dataStore.ListDoctorQualificationsAsync(doctor.Value.Doctor.Id, cancellationToken);
        return Result<IReadOnlyList<DoctorQualificationResponse>>.Ok(items.Select(Map).ToArray());
    }

    internal static DoctorQualificationResponse Map(DoctorQualification item)
        => new(item.Id, item.NameAr, item.NameEn, item.DisplayOrder, RowVersionCodec.Encode(item.RowVersion));
}

internal sealed class CreateMyDoctorQualificationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<CreateMyDoctorQualificationCommand, DoctorQualificationResponse>
{
    public async Task<Result<DoctorQualificationResponse>> Handle(
        CreateMyDoctorQualificationCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorQualificationResponse>.Fail(doctor.Errors);
        }

        var created = DoctorQualification.Create(
            Guid.NewGuid(), doctor.Value.Doctor.Id, request.NameAr, request.NameEn,
            request.DisplayOrder, doctor.Value.ActorId);
        if (created.IsFailure)
        {
            return Result<DoctorQualificationResponse>.Fail(created.Errors);
        }

        dataStore.Add(created.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorQualificationResponse>.Ok(
            ListMyDoctorQualificationsQueryHandler.Map(created.Value));
    }
}

internal sealed class UpdateMyDoctorQualificationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateMyDoctorQualificationCommand, DoctorQualificationResponse>
{
    public async Task<Result<DoctorQualificationResponse>> Handle(
        UpdateMyDoctorQualificationCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<DoctorQualificationResponse>.Fail(doctor.Errors);
        }

        var item = await dataStore.FindDoctorQualificationAsync(request.QualificationId, cancellationToken);
        if (item is null || item.DoctorId != doctor.Value.Doctor.Id)
        {
            return Result<DoctorQualificationResponse>.Fail(DoctorQualificationErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            item.RowVersion, request.RowVersion, DoctorQualificationErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result<DoctorQualificationResponse>.Fail(supplied.Errors);
        }

        var updated = item.Update(
            request.NameAr, request.NameEn, request.DisplayOrder, doctor.Value.ActorId);
        if (updated.IsFailure)
        {
            return Result<DoctorQualificationResponse>.Fail(updated.Errors);
        }

        dataStore.SetOriginalRowVersion(item, supplied.Value);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<DoctorQualificationResponse>.Ok(ListMyDoctorQualificationsQueryHandler.Map(item));
    }
}

internal sealed class DeleteMyDoctorQualificationCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser)
    : ICommandHandler<DeleteMyDoctorQualificationCommand>
{
    public async Task<Result> Handle(
        DeleteMyDoctorQualificationCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result.Fail(doctor.Errors);
        }

        var item = await dataStore.FindDoctorQualificationAsync(request.QualificationId, cancellationToken);
        if (item is null || item.DoctorId != doctor.Value.Doctor.Id)
        {
            return Result.Fail(DoctorQualificationErrors.NotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            item.RowVersion, request.RowVersion, DoctorQualificationErrors.ConcurrencyConflict);
        if (supplied.IsFailure)
        {
            return Result.Fail(supplied.Errors);
        }

        dataStore.SetOriginalRowVersion(item, supplied.Value);
        dataStore.Remove(item);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
