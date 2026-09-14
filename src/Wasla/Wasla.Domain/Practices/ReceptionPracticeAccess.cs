using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public sealed class Reception : AggregateRoot<Guid>, IAuditableEntity
{
    private Reception()
    {
    }

    private Reception(
        Guid id,
        Guid applicationUserId,
        Guid ownerDoctorId,
        string nameAr,
        string? nameEn,
        Guid actorId)
        : base(id)
    {
        ApplicationUserId = applicationUserId;
        OwnerDoctorId = ownerDoctorId;
        NameAr = nameAr;
        NameEn = nameEn;
        CreatedByApplicationUserId = actorId;
    }

    public Guid ApplicationUserId { get; private set; }
    public Guid OwnerDoctorId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Reception> Create(
        Guid id,
        Guid applicationUserId,
        Guid ownerDoctorId,
        string nameAr,
        string? nameEn,
        Guid actorId)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return id == Guid.Empty || applicationUserId == Guid.Empty || ownerDoctorId == Guid.Empty ||
               actorId == Guid.Empty || normalizedAr.Length is 0 or > 200 || normalizedEn?.Length > 200
            ? Result<Reception>.Fail(ReceptionErrors.Invalid)
            : Result<Reception>.Ok(new Reception(
                id, applicationUserId, ownerDoctorId, normalizedAr, normalizedEn, actorId));
    }
}

public sealed class ReceptionPracticeAssignment : AggregateRoot<Guid>, IAuditableEntity
{
    private ReceptionPracticeAssignment()
    {
    }

    private ReceptionPracticeAssignment(
        Guid id,
        Guid receptionId,
        Guid doctorPracticeId,
        Guid actorId)
        : base(id)
    {
        ReceptionId = receptionId;
        DoctorPracticeId = doctorPracticeId;
        IsActive = true;
        CreatedByApplicationUserId = actorId;
    }

    public Guid ReceptionId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<ReceptionPracticeAssignment> Create(
        Guid id,
        Guid receptionId,
        Guid doctorPracticeId,
        Guid actorId)
        => id == Guid.Empty || receptionId == Guid.Empty || doctorPracticeId == Guid.Empty || actorId == Guid.Empty
            ? Result<ReceptionPracticeAssignment>.Fail(ReceptionErrors.InvalidAssignment)
            : Result<ReceptionPracticeAssignment>.Ok(new ReceptionPracticeAssignment(
                id, receptionId, doctorPracticeId, actorId));

    public Result Activate(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Fail(ReceptionErrors.InvalidAssignment);
        }

        IsActive = true;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Deactivate(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Fail(ReceptionErrors.InvalidAssignment);
        }

        IsActive = false;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result MarkPermissionsChanged(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Fail(ReceptionErrors.InvalidAssignment);
        }

        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }
}

public sealed class ReceptionPracticeAssignmentPermission : Entity<Guid>
{
    private ReceptionPracticeAssignmentPermission()
    {
    }

    public ReceptionPracticeAssignmentPermission(
        Guid id,
        Guid assignmentId,
        Guid permissionId,
        Guid createdByApplicationUserId,
        DateTime createdOnUtc)
        : base(id)
    {
        if (id == Guid.Empty || assignmentId == Guid.Empty || permissionId == Guid.Empty ||
            createdByApplicationUserId == Guid.Empty || createdOnUtc == default)
        {
            throw new ArgumentException("Reception assignment permission data is invalid.", nameof(id));
        }

        AssignmentId = assignmentId;
        PermissionId = permissionId;
        CreatedByApplicationUserId = createdByApplicationUserId;
        CreatedOnUtc = RequireUtc(createdOnUtc);
    }

    public Guid AssignmentId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public static class ReceptionErrors
{
    public static Error Invalid => Error.Validation(
        "Reception.Invalid",
        DoctorPracticeErrors.Text("ReceptionInvalid"));
    public static Error NotFound => Error.NotFound(
        "Reception.NotFound",
        DoctorPracticeErrors.Text("ReceptionNotFound"));
    public static Error IdentityConflict => Error.Conflict(
        "Reception.IdentityConflict",
        DoctorPracticeErrors.Text("ReceptionIdentityConflict"));
    public static Error InvalidAssignment => Error.Validation(
        "ReceptionAssignment.Invalid",
        DoctorPracticeErrors.Text("ReceptionAssignmentInvalid"));
    public static Error AssignmentNotFound => Error.NotFound(
        "ReceptionAssignment.NotFound",
        DoctorPracticeErrors.Text("ReceptionAssignmentNotFound"));
    public static Error DuplicateAssignment => Error.Conflict(
        "ReceptionAssignment.Duplicate",
        DoctorPracticeErrors.Text("ReceptionAssignmentDuplicate"));
    public static Error CrossDoctorAssignmentNotSupported => Error.Conflict(
        "ReceptionAssignment.CrossDoctorNotSupported",
        DoctorPracticeErrors.Text("ReceptionAssignmentCrossDoctorNotSupported"));
    public static Error AccessDenied => Error.Security(
        "ReceptionAssignment.AccessDenied",
        DoctorPracticeErrors.Text("ReceptionAssignmentAccessDenied"));
    public static Error InvalidPermission => Error.Validation(
        "ReceptionAssignment.InvalidPermission",
        DoctorPracticeErrors.Text("ReceptionAssignmentInvalidPermission"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "ReceptionAssignment.ConcurrencyConflict",
        DoctorPracticeErrors.Text("ReceptionAssignmentConcurrencyConflict"));
}
