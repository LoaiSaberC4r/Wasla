using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Email;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Practices;

public sealed record ReceptionAssignmentPermissionResponse(Guid Id, string Code);
public sealed record ReceptionAssignmentResponse(
    Guid Id,
    Guid DoctorPracticeId,
    bool IsActive,
    IReadOnlyList<ReceptionAssignmentPermissionResponse> Permissions,
    string RowVersion);
public sealed record ReceptionResponse(
    Guid Id,
    Guid ApplicationUserId,
    string NameAr,
    string? NameEn,
    string UserName,
    string Email,
    string? PhoneNumber,
    IReadOnlyList<ReceptionAssignmentResponse> Assignments,
    string RowVersion);
public sealed record ReceptionPracticeContextResponse(
    Guid DoctorPracticeId,
    string NameAr,
    string? NameEn,
    IReadOnlyList<string> PermissionCodes);

public sealed record ListDoctorReceptionsQuery : IQuery<IReadOnlyList<ReceptionResponse>>;
public sealed record GetDoctorReceptionQuery(Guid ReceptionId) : IQuery<ReceptionResponse>;
public sealed record CreateDoctorReceptionCommand(
    string UserName,
    string Email,
    string? PhoneNumber,
    string? TemporaryPassword,
    string NameAr,
    string? NameEn)
    : ICommand<ReceptionResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CreateReceptionAssignmentCommand(
    Guid ReceptionId,
    Guid DoctorPracticeId,
    IReadOnlyCollection<Guid> PermissionIds)
    : ICommand<ReceptionAssignmentResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateReceptionAssignmentCommand(
    Guid ReceptionId,
    Guid AssignmentId,
    IReadOnlyCollection<Guid> PermissionIds,
    string RowVersion)
    : ICommand<ReceptionAssignmentResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ActivateReceptionAssignmentCommand(Guid ReceptionId, Guid AssignmentId, string RowVersion)
    : ICommand<ReceptionAssignmentResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DeactivateReceptionAssignmentCommand(Guid ReceptionId, Guid AssignmentId, string RowVersion)
    : ICommand<ReceptionAssignmentResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListMyReceptionPracticesQuery : IQuery<IReadOnlyList<ReceptionPracticeContextResponse>>;

public interface IReceptionPracticeAuthorizationService
{
    Task<Result> AuthorizeAsync(Guid doctorPracticeId, string permissionCode, CancellationToken cancellationToken);
}

internal sealed class CreateDoctorReceptionCommandValidator : AbstractValidator<CreateDoctorReceptionCommand>
{
    public CreateDoctorReceptionCommandValidator()
    {
        RuleFor(command => command.UserName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(command => command.PhoneNumber).MaximumLength(30);
        RuleFor(command => command.TemporaryPassword).MaximumLength(4096);
        RuleFor(command => command.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(command => command.NameEn).MaximumLength(200);
    }
}

internal sealed class CreateReceptionAssignmentCommandValidator
    : AbstractValidator<CreateReceptionAssignmentCommand>
{
    public CreateReceptionAssignmentCommandValidator()
    {
        RuleFor(command => command.ReceptionId).NotEmpty();
        RuleFor(command => command.DoctorPracticeId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotEmpty();
        RuleForEach(command => command.PermissionIds).NotEmpty();
    }
}

internal sealed class UpdateReceptionAssignmentCommandValidator
    : AbstractValidator<UpdateReceptionAssignmentCommand>
{
    public UpdateReceptionAssignmentCommandValidator()
    {
        RuleFor(command => command.ReceptionId).NotEmpty();
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.PermissionIds).NotEmpty();
        RuleForEach(command => command.PermissionIds).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ActivateReceptionAssignmentCommandValidator
    : AbstractValidator<ActivateReceptionAssignmentCommand>
{
    public ActivateReceptionAssignmentCommandValidator()
    {
        RuleFor(command => command.ReceptionId).NotEmpty();
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class DeactivateReceptionAssignmentCommandValidator
    : AbstractValidator<DeactivateReceptionAssignmentCommand>
{
    public DeactivateReceptionAssignmentCommandValidator()
    {
        RuleFor(command => command.ReceptionId).NotEmpty();
        RuleFor(command => command.AssignmentId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(RowVersionCodec.IsValid);
    }
}

internal sealed class ListDoctorReceptionsQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListDoctorReceptionsQuery, IReadOnlyList<ReceptionResponse>>
{
    public async Task<Result<IReadOnlyList<ReceptionResponse>>> Handle(
        ListDoctorReceptionsQuery request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<IReadOnlyList<ReceptionResponse>>.Fail(doctor.Errors);
        }

        var records = await dataStore.ListDoctorReceptionsAsync(doctor.Value.Doctor.Id, cancellationToken);
        var responses = new List<ReceptionResponse>(records.Count);
        foreach (var record in records)
        {
            responses.Add(await ReceptionMapper.MapAsync(dataStore, record, cancellationToken));
        }

        return Result<IReadOnlyList<ReceptionResponse>>.Ok(responses);
    }
}

internal sealed class GetDoctorReceptionQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<GetDoctorReceptionQuery, ReceptionResponse>
{
    public async Task<Result<ReceptionResponse>> Handle(
        GetDoctorReceptionQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ReceptionManagementAccess.LoadOwnedReceptionAsync(
            dataStore, currentUser, request.ReceptionId, cancellationToken);
        return access.IsFailure
            ? Result<ReceptionResponse>.Fail(access.Errors)
            : Result<ReceptionResponse>.Ok(await ReceptionMapper.MapAsync(
                dataStore, access.Value.Record, cancellationToken));
    }
}

internal sealed class CreateDoctorReceptionCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    IEmailNotificationFactory emailFactory,
    IEmailOutbox emailOutbox)
    : ICommandHandler<CreateDoctorReceptionCommand, ReceptionResponse>
{
    public async Task<Result<ReceptionResponse>> Handle(
        CreateDoctorReceptionCommand request,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<ReceptionResponse>.Fail(doctor.Errors);
        }

        var role = await dataStore.FindRoleByNameAsync(SystemRoleNames.Reception, cancellationToken);
        if (role is null)
        {
            return Result<ReceptionResponse>.Fail(ReceptionErrors.IdentityConflict);
        }

        var byEmail = await dataStore.FindUserByIdentifierAsync(request.Email.Trim(), cancellationToken);
        var byUserName = await dataStore.FindUserByIdentifierAsync(request.UserName.Trim(), cancellationToken);
        if (byEmail is not null && byUserName is not null && byEmail.Id != byUserName.Id)
        {
            return Result<ReceptionResponse>.Fail(ReceptionErrors.IdentityConflict);
        }

        var user = byEmail ?? byUserName;
        var isNewUser = user is null;
        if (user is not null)
        {
            if (user.UserType != UserType.Reception || !user.IsActive ||
                !string.Equals(user.UserName, request.UserName.Trim(), StringComparison.Ordinal) ||
                !string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase) ||
                await dataStore.FindReceptionByApplicationUserIdAsync(user.Id, cancellationToken) is not null)
            {
                return Result<ReceptionResponse>.Fail(ReceptionErrors.IdentityConflict);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.TemporaryPassword) ||
                !passwordService.IsStrongPassword(request.TemporaryPassword))
            {
                return Result<ReceptionResponse>.Fail(Error.Validation(
                    "Auth.PasswordInvalid", ErrorMessage.PasswordInvalid));
            }

            var userResult = ApplicationUser.Create(
                Guid.NewGuid(),
                request.UserName,
                request.Email,
                request.PhoneNumber,
                await passwordService.HashAsync(request.TemporaryPassword, cancellationToken),
                UserType.Reception,
                isFirstLogin: true,
                clock.UtcNow,
                doctor.Value.ActorId);
            if (userResult.IsFailure)
            {
                return Result<ReceptionResponse>.Fail(userResult.Errors);
            }

            user = userResult.Value;
            dataStore.Add(user);
        }

        if (!await dataStore.HasUserRoleAsync(user.Id, role.Id, cancellationToken))
        {
            dataStore.Add(new UserRole(Guid.NewGuid(), user.Id, role.Id, doctor.Value.ActorId));
        }

        var reception = Reception.Create(
            Guid.NewGuid(), user.Id, doctor.Value.Doctor.Id, request.NameAr, request.NameEn, doctor.Value.ActorId);
        if (reception.IsFailure)
        {
            return Result<ReceptionResponse>.Fail(reception.Errors);
        }

        dataStore.Add(reception.Value);
        if (isNewUser)
        {
            var email = emailFactory.ReceptionAccountCreated(reception.Value.NameAr, user.UserName);
            await emailOutbox.QueueAsync(new QueueEmailMessage(
                $"reception-account-created:{reception.Value.Id}",
                user.Email,
                email.Subject,
                email.HtmlBody,
                email.TextBody), cancellationToken);
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<ReceptionResponse>.Ok(await ReceptionMapper.MapAsync(
            dataStore, new ReceptionViewRecord(reception.Value, user), cancellationToken));
    }
}

internal sealed class CreateReceptionAssignmentCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<CreateReceptionAssignmentCommand, ReceptionAssignmentResponse>
{
    public async Task<Result<ReceptionAssignmentResponse>> Handle(
        CreateReceptionAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var access = await ReceptionManagementAccess.LoadOwnedReceptionAndPracticeAsync(
            dataStore, currentUser, request.ReceptionId, request.DoctorPracticeId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(access.Errors);
        }

        if (await dataStore.ReceptionAssignmentExistsAsync(
                request.ReceptionId, request.DoctorPracticeId, cancellationToken))
        {
            return Result<ReceptionAssignmentResponse>.Fail(ReceptionErrors.DuplicateAssignment);
        }

        var permissions = await ReceptionManagementAccess.ValidatePermissionsAsync(
            dataStore, request.PermissionIds, cancellationToken);
        if (permissions.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(permissions.Errors);
        }

        var assignment = ReceptionPracticeAssignment.Create(
            Guid.NewGuid(), request.ReceptionId, request.DoctorPracticeId, access.Value.ActorId);
        if (assignment.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(assignment.Errors);
        }

        dataStore.Add(assignment.Value);
        var now = clock.UtcNow;
        foreach (var permission in permissions.Value)
        {
            dataStore.Add(new ReceptionPracticeAssignmentPermission(
                Guid.NewGuid(), assignment.Value.Id, permission.Id, access.Value.ActorId, now));
        }

        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<ReceptionAssignmentResponse>.Ok(ReceptionMapper.Map(
            assignment.Value, permissions.Value));
    }
}

internal sealed class UpdateReceptionAssignmentCommandHandler(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateReceptionAssignmentCommand, ReceptionAssignmentResponse>
{
    public async Task<Result<ReceptionAssignmentResponse>> Handle(
        UpdateReceptionAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await ReceptionManagementAccess.LoadOwnedAssignmentAsync(
            dataStore, currentUser, request.ReceptionId, request.AssignmentId, request.RowVersion,
            cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(loaded.Errors);
        }

        var permissions = await ReceptionManagementAccess.ValidatePermissionsAsync(
            dataStore, request.PermissionIds, cancellationToken);
        if (permissions.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(permissions.Errors);
        }

        var replacements = permissions.Value.Select(permission => new ReceptionPracticeAssignmentPermission(
            Guid.NewGuid(), request.AssignmentId, permission.Id, loaded.Value.ActorId, clock.UtcNow)).ToArray();
        var changed = loaded.Value.Assignment.MarkPermissionsChanged(loaded.Value.ActorId);
        if (changed.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(changed.Errors);
        }

        await dataStore.ReplaceReceptionAssignmentPermissionsAsync(
            request.AssignmentId, replacements, cancellationToken);
        dataStore.SetOriginalRowVersion(loaded.Value.Assignment, loaded.Value.Supplied);
        await dataStore.SaveChangesAsync(cancellationToken);
        return Result<ReceptionAssignmentResponse>.Ok(ReceptionMapper.Map(
            loaded.Value.Assignment, permissions.Value));
    }
}

internal sealed class ActivateReceptionAssignmentCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<ActivateReceptionAssignmentCommand, ReceptionAssignmentResponse>
{
    public async Task<Result<ReceptionAssignmentResponse>> Handle(
        ActivateReceptionAssignmentCommand request,
        CancellationToken cancellationToken)
        => await ChangeStateAsync(dataStore, currentUser, request.ReceptionId, request.AssignmentId,
            request.RowVersion, true, cancellationToken);

    internal static async Task<Result<ReceptionAssignmentResponse>> ChangeStateAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        Guid receptionId,
        Guid assignmentId,
        string rowVersion,
        bool activate,
        CancellationToken cancellationToken)
    {
        var loaded = await ReceptionManagementAccess.LoadOwnedAssignmentAsync(
            dataStore, currentUser, receptionId, assignmentId, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(loaded.Errors);
        }

        var changed = activate
            ? loaded.Value.Assignment.Activate(loaded.Value.ActorId)
            : loaded.Value.Assignment.Deactivate(loaded.Value.ActorId);
        if (changed.IsFailure)
        {
            return Result<ReceptionAssignmentResponse>.Fail(changed.Errors);
        }

        dataStore.SetOriginalRowVersion(loaded.Value.Assignment, loaded.Value.Supplied);
        await dataStore.SaveChangesAsync(cancellationToken);
        var permissionLinks = await dataStore.ListReceptionAssignmentPermissionsAsync(assignmentId, cancellationToken);
        var permissions = await dataStore.ListPermissionsByIdsAsync(
            permissionLinks.Select(item => item.PermissionId).ToArray(), cancellationToken);
        return Result<ReceptionAssignmentResponse>.Ok(ReceptionMapper.Map(loaded.Value.Assignment, permissions));
    }
}

internal sealed class DeactivateReceptionAssignmentCommandHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : ICommandHandler<DeactivateReceptionAssignmentCommand, ReceptionAssignmentResponse>
{
    public async Task<Result<ReceptionAssignmentResponse>> Handle(
        DeactivateReceptionAssignmentCommand request,
        CancellationToken cancellationToken)
        => await ActivateReceptionAssignmentCommandHandler.ChangeStateAsync(
            dataStore, currentUser, request.ReceptionId, request.AssignmentId,
            request.RowVersion, false, cancellationToken);
}

internal sealed class ListMyReceptionPracticesQueryHandler(IWaslaDataStore dataStore, ICurrentUser currentUser)
    : IQueryHandler<ListMyReceptionPracticesQuery, IReadOnlyList<ReceptionPracticeContextResponse>>
{
    public async Task<Result<IReadOnlyList<ReceptionPracticeContextResponse>>> Handle(
        ListMyReceptionPracticesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId ||
            !currentUser.Roles.Contains(SystemRoleNames.Reception, StringComparer.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<ReceptionPracticeContextResponse>>.Fail(ReceptionErrors.AccessDenied);
        }

        var reception = await dataStore.FindReceptionByApplicationUserIdAsync(userId, cancellationToken);
        if (reception is null)
        {
            return Result<IReadOnlyList<ReceptionPracticeContextResponse>>.Fail(ReceptionErrors.NotFound);
        }

        var assignments = (await dataStore.ListReceptionAssignmentsAsync(reception.Id, cancellationToken))
            .Where(item => item.IsActive)
            .ToArray();
        var result = new List<ReceptionPracticeContextResponse>(assignments.Length);
        foreach (var assignment in assignments)
        {
            var practice = await dataStore.GetDoctorPracticeAsync(assignment.DoctorPracticeId, cancellationToken);
            if (practice is null)
            {
                continue;
            }

            var links = await dataStore.ListReceptionAssignmentPermissionsAsync(assignment.Id, cancellationToken);
            var permissions = await dataStore.ListPermissionsByIdsAsync(
                links.Select(item => item.PermissionId).ToArray(), cancellationToken);
            result.Add(new ReceptionPracticeContextResponse(
                practice.Practice.Id,
                practice.Practice.NameAr,
                practice.Practice.NameEn,
                permissions.Select(item => item.Name).Order(StringComparer.Ordinal).ToArray()));
        }

        return Result<IReadOnlyList<ReceptionPracticeContextResponse>>.Ok(result);
    }
}

internal sealed class ReceptionPracticeAuthorizationService(
    IWaslaDataStore dataStore,
    ICurrentUser currentUser) : IReceptionPracticeAuthorizationService
{
    public async Task<Result> AuthorizeAsync(
        Guid doctorPracticeId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId ||
            !currentUser.Roles.Contains(SystemRoleNames.Reception, StringComparer.OrdinalIgnoreCase) ||
            !currentUser.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase) ||
            !PermissionNames.ReceptionAssignmentScoped.Contains(permissionCode))
        {
            return Result.Fail(ReceptionErrors.AccessDenied);
        }

        return await dataStore.HasReceptionPracticePermissionAsync(
            userId, doctorPracticeId, permissionCode, cancellationToken)
            ? Result.Ok()
            : Result.Fail(ReceptionErrors.AccessDenied);
    }
}

internal static class ReceptionManagementAccess
{
    public static async Task<Result<(ReceptionViewRecord Record, Guid ActorId)>> LoadOwnedReceptionAsync(
        IWaslaDataStore dataStore,
        ICurrentUser currentUser,
        Guid receptionId,
        CancellationToken cancellationToken)
    {
        var doctor = await DoctorPracticeAccess.ResolveDoctorAsync(dataStore, currentUser, cancellationToken);
        if (doctor.IsFailure)
        {
            return Result<(ReceptionViewRecord, Guid)>.Fail(doctor.Errors);
        }

        var reception = await dataStore.FindDoctorReceptionAsync(receptionId, cancellationToken);
        if (reception is null || reception.Reception.OwnerDoctorId != doctor.Value.Doctor.Id)
        {
            return Result<(ReceptionViewRecord, Guid)>.Fail(ReceptionErrors.NotFound);
        }

        return Result<(ReceptionViewRecord, Guid)>.Ok((reception, doctor.Value.ActorId));
    }

    public static async Task<Result<(Reception Reception, DoctorPractice Practice, Guid ActorId)>>
        LoadOwnedReceptionAndPracticeAsync(
            IWaslaDataStore dataStore,
            ICurrentUser currentUser,
            Guid receptionId,
            Guid practiceId,
            CancellationToken cancellationToken)
    {
        var reception = await LoadOwnedReceptionAsync(dataStore, currentUser, receptionId, cancellationToken);
        if (reception.IsFailure)
        {
            return Result<(Reception, DoctorPractice, Guid)>.Fail(reception.Errors);
        }

        var practice = await DoctorPracticeAccess.LoadOwnedPracticeAsync(
            dataStore, currentUser, practiceId, cancellationToken);
        if (practice.IsFailure)
        {
            return Result<(Reception, DoctorPractice, Guid)>.Fail(practice.Errors);
        }

        if (reception.Value.Record.Reception.OwnerDoctorId != practice.Value.Doctor.Id)
        {
            return Result<(Reception, DoctorPractice, Guid)>.Fail(ReceptionErrors.CrossDoctorAssignmentNotSupported);
        }

        return Result<(Reception, DoctorPractice, Guid)>.Ok((
            reception.Value.Record.Reception, practice.Value.Practice, reception.Value.ActorId));
    }

    public static async Task<Result<(ReceptionPracticeAssignment Assignment, Guid ActorId, byte[] Supplied)>>
        LoadOwnedAssignmentAsync(
            IWaslaDataStore dataStore,
            ICurrentUser currentUser,
            Guid receptionId,
            Guid assignmentId,
            string rowVersion,
            CancellationToken cancellationToken)
    {
        var reception = await LoadOwnedReceptionAsync(dataStore, currentUser, receptionId, cancellationToken);
        if (reception.IsFailure)
        {
            return Result<(ReceptionPracticeAssignment, Guid, byte[])>.Fail(reception.Errors);
        }

        var assignment = await dataStore.FindReceptionAssignmentAsync(assignmentId, cancellationToken);
        if (assignment is null || assignment.ReceptionId != receptionId)
        {
            return Result<(ReceptionPracticeAssignment, Guid, byte[])>.Fail(ReceptionErrors.AssignmentNotFound);
        }

        var practice = await dataStore.FindDoctorPracticeAsync(assignment.DoctorPracticeId, cancellationToken);
        if (practice is null || practice.DoctorId != reception.Value.Record.Reception.OwnerDoctorId)
        {
            return Result<(ReceptionPracticeAssignment, Guid, byte[])>.Fail(ReceptionErrors.AssignmentNotFound);
        }

        var supplied = DoctorPracticeAccess.VerifyRowVersion(
            assignment.RowVersion, rowVersion, ReceptionErrors.ConcurrencyConflict);
        return supplied.IsFailure
            ? Result<(ReceptionPracticeAssignment, Guid, byte[])>.Fail(supplied.Errors)
            : Result<(ReceptionPracticeAssignment, Guid, byte[])>.Ok((
                assignment, reception.Value.ActorId, supplied.Value));
    }

    public static async Task<Result<IReadOnlyList<Permission>>> ValidatePermissionsAsync(
        IWaslaDataStore dataStore,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var distinct = permissionIds.Distinct().ToArray();
        var permissions = await dataStore.ListPermissionsByIdsAsync(distinct, cancellationToken);
        return distinct.Length > 0 && permissions.Count == distinct.Length &&
               permissions.All(permission => PermissionNames.ReceptionAssignmentScoped.Contains(permission.Name))
            ? Result<IReadOnlyList<Permission>>.Ok(permissions)
            : Result<IReadOnlyList<Permission>>.Fail(ReceptionErrors.InvalidPermission);
    }
}

internal static class ReceptionMapper
{
    public static async Task<ReceptionResponse> MapAsync(
        IWaslaDataStore dataStore,
        ReceptionViewRecord record,
        CancellationToken cancellationToken)
    {
        var assignments = await dataStore.ListReceptionAssignmentsAsync(record.Reception.Id, cancellationToken);
        var mapped = new List<ReceptionAssignmentResponse>(assignments.Count);
        foreach (var assignment in assignments)
        {
            var links = await dataStore.ListReceptionAssignmentPermissionsAsync(assignment.Id, cancellationToken);
            var permissions = await dataStore.ListPermissionsByIdsAsync(
                links.Select(item => item.PermissionId).ToArray(), cancellationToken);
            mapped.Add(Map(assignment, permissions));
        }

        return new ReceptionResponse(
            record.Reception.Id,
            record.Reception.ApplicationUserId,
            record.Reception.NameAr,
            record.Reception.NameEn,
            record.User.UserName,
            record.User.Email,
            record.User.PhoneNumber,
            mapped,
            RowVersionCodec.Encode(record.Reception.RowVersion));
    }

    public static ReceptionAssignmentResponse Map(
        ReceptionPracticeAssignment assignment,
        IReadOnlyList<Permission> permissions)
        => new(
            assignment.Id,
            assignment.DoctorPracticeId,
            assignment.IsActive,
            permissions.Select(item => new ReceptionAssignmentPermissionResponse(item.Id, item.Name))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ToArray(),
            RowVersionCodec.Encode(assignment.RowVersion));
}
