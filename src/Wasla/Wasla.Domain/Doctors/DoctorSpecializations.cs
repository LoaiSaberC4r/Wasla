using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Doctors;

public enum DoctorSpecializationRequestType
{
    Initial = 1,
    Change = 2
}

public enum DoctorSpecializationRequestStatus
{
    PendingReview = 1,
    ModificationRequested = 2,
    Approved = 3,
    Rejected = 4
}

public enum DoctorSpecializationRequestAction
{
    Submitted = 1,
    Resubmitted = 2,
    SpecializationsAdjustedBySuperAdmin = 3,
    ModificationRequested = 4,
    Approved = 5,
    Rejected = 6
}

public sealed record DoctorSpecializationSelection(Guid MedicalSpecializationId, bool IsPrimary);

public static class DoctorSpecializationSet
{
    public static Result Validate(IReadOnlyCollection<DoctorSpecializationSelection>? selections)
    {
        if (selections is null || selections.Count == 0)
        {
            return Result.Fail(DoctorSpecializationErrors.NoSpecializations);
        }

        if (selections.Any(item => item.MedicalSpecializationId == Guid.Empty))
        {
            return Result.Fail(DoctorSpecializationErrors.SpecializationNotAvailable);
        }

        if (selections.Select(item => item.MedicalSpecializationId).Distinct().Count() != selections.Count)
        {
            return Result.Fail(DoctorSpecializationErrors.DuplicateSpecialization);
        }

        var primaryCount = selections.Count(item => item.IsPrimary);
        return primaryCount switch
        {
            0 => Result.Fail(DoctorSpecializationErrors.PrimaryRequired),
            > 1 => Result.Fail(DoctorSpecializationErrors.MultiplePrimary),
            _ => Result.Ok()
        };
    }
}

public sealed class DoctorSpecialization : Entity<Guid>
{
    private DoctorSpecialization()
    {
    }

    public DoctorSpecialization(
        Guid id,
        Guid doctorId,
        Guid medicalSpecializationId,
        bool isPrimary,
        Guid createdByApplicationUserId,
        DateTime createdOnUtc)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(doctorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(medicalSpecializationId, Guid.Empty);
        DoctorId = doctorId;
        MedicalSpecializationId = medicalSpecializationId;
        IsPrimary = isPrimary;
        CreatedByApplicationUserId = createdByApplicationUserId;
        CreatedOnUtc = RequireUtc(createdOnUtc);
    }

    public Guid DoctorId { get; private set; }
    public Guid MedicalSpecializationId { get; private set; }
    public bool IsPrimary { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class DoctorSpecializationRequest : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorSpecializationRequest()
    {
    }

    private DoctorSpecializationRequest(
        Guid id,
        Guid doctorId,
        DoctorSpecializationRequestType type,
        Guid submittedByApplicationUserId,
        DateTime submittedOnUtc)
        : base(id)
    {
        DoctorId = doctorId;
        Type = type;
        Status = DoctorSpecializationRequestStatus.PendingReview;
        CurrentRevisionNumber = 1;
        SubmittedByApplicationUserId = submittedByApplicationUserId;
        SubmittedOnUtc = RequireUtc(submittedOnUtc);
    }

    public Guid DoctorId { get; private set; }
    public DoctorSpecializationRequestType Type { get; private set; }
    public DoctorSpecializationRequestStatus Status { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public Guid SubmittedByApplicationUserId { get; private set; }
    public DateTime SubmittedOnUtc { get; private set; }
    public Guid? ReviewedByApplicationUserId { get; private set; }
    public DateTime? ReviewedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorSpecializationRequest> Create(
        Guid id,
        Guid doctorId,
        DoctorSpecializationRequestType type,
        Guid submittedByApplicationUserId,
        DateTime submittedOnUtc)
    {
        if (id == Guid.Empty || doctorId == Guid.Empty || submittedByApplicationUserId == Guid.Empty || !Enum.IsDefined(type))
        {
            return Result<DoctorSpecializationRequest>.Fail(DoctorSpecializationErrors.InvalidRequestStatus);
        }

        return Result<DoctorSpecializationRequest>.Ok(new DoctorSpecializationRequest(
            id,
            doctorId,
            type,
            submittedByApplicationUserId,
            submittedOnUtc));
    }

    public Result Resubmit(Guid actorId, DateTime occurredOnUtc)
    {
        if (Status != DoctorSpecializationRequestStatus.ModificationRequested)
        {
            return Result.Fail(DoctorSpecializationErrors.InvalidRequestStatus);
        }

        CurrentRevisionNumber++;
        Status = DoctorSpecializationRequestStatus.PendingReview;
        SubmittedByApplicationUserId = actorId;
        SubmittedOnUtc = RequireUtc(occurredOnUtc);
        ReviewedByApplicationUserId = null;
        ReviewedOnUtc = null;
        return Result.Ok();
    }

    public Result Adjust(Guid actorId, DateTime occurredOnUtc)
    {
        if (Status != DoctorSpecializationRequestStatus.PendingReview)
        {
            return Result.Fail(DoctorSpecializationErrors.InvalidRequestStatus);
        }

        CurrentRevisionNumber++;
        Status = DoctorSpecializationRequestStatus.PendingReview;
        SubmittedByApplicationUserId = actorId;
        SubmittedOnUtc = RequireUtc(occurredOnUtc);
        ReviewedByApplicationUserId = null;
        ReviewedOnUtc = null;
        return Result.Ok();
    }

    public Result RequestModification(Guid actorId, DateTime occurredOnUtc)
        => ReviewTransition(DoctorSpecializationRequestStatus.ModificationRequested, actorId, occurredOnUtc);

    public Result Approve(Guid actorId, DateTime occurredOnUtc)
        => ReviewTransition(DoctorSpecializationRequestStatus.Approved, actorId, occurredOnUtc);

    public Result Reject(Guid actorId, DateTime occurredOnUtc)
        => ReviewTransition(DoctorSpecializationRequestStatus.Rejected, actorId, occurredOnUtc);

    private Result ReviewTransition(
        DoctorSpecializationRequestStatus status,
        Guid actorId,
        DateTime occurredOnUtc)
    {
        if (Status != DoctorSpecializationRequestStatus.PendingReview)
        {
            return Result.Fail(DoctorSpecializationErrors.InvalidRequestStatus);
        }

        Status = status;
        ReviewedByApplicationUserId = actorId;
        ReviewedOnUtc = RequireUtc(occurredOnUtc);
        return Result.Ok();
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed class DoctorSpecializationRequestRevision : Entity<Guid>
{
    private DoctorSpecializationRequestRevision()
    {
    }

    public DoctorSpecializationRequestRevision(
        Guid id,
        Guid doctorSpecializationRequestId,
        int revisionNumber,
        Guid submittedByApplicationUserId,
        DateTime submittedOnUtc)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(doctorSpecializationRequestId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(revisionNumber, 1);
        DoctorSpecializationRequestId = doctorSpecializationRequestId;
        RevisionNumber = revisionNumber;
        SubmittedByApplicationUserId = submittedByApplicationUserId;
        SubmittedOnUtc = submittedOnUtc.Kind == DateTimeKind.Utc ? submittedOnUtc : submittedOnUtc.ToUniversalTime();
    }

    public Guid DoctorSpecializationRequestId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid SubmittedByApplicationUserId { get; private set; }
    public DateTime SubmittedOnUtc { get; private set; }
}

public sealed class DoctorSpecializationRequestItem : Entity<Guid>
{
    private DoctorSpecializationRequestItem()
    {
    }

    public DoctorSpecializationRequestItem(Guid id, Guid revisionId, Guid medicalSpecializationId, bool isPrimary)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(revisionId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(medicalSpecializationId, Guid.Empty);
        RevisionId = revisionId;
        MedicalSpecializationId = medicalSpecializationId;
        IsPrimary = isPrimary;
    }

    public Guid RevisionId { get; private set; }
    public Guid MedicalSpecializationId { get; private set; }
    public bool IsPrimary { get; private set; }
}

public sealed class DoctorSpecializationRequestHistory : Entity<Guid>
{
    private DoctorSpecializationRequestHistory()
    {
    }

    public DoctorSpecializationRequestHistory(
        Guid id,
        Guid doctorSpecializationRequestId,
        DoctorSpecializationRequestAction action,
        int revisionNumber,
        string? message,
        Guid performedByApplicationUserId,
        DateTime performedOnUtc,
        int? previousRevisionNumber = null)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(doctorSpecializationRequestId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(revisionNumber, 1);
        var normalizedMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (normalizedMessage?.Length > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(message));
        }
        if (previousRevisionNumber is < 1 || previousRevisionNumber >= revisionNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(previousRevisionNumber));
        }

        DoctorSpecializationRequestId = doctorSpecializationRequestId;
        Action = action;
        RevisionNumber = revisionNumber;
        PreviousRevisionNumber = previousRevisionNumber;
        Message = normalizedMessage;
        PerformedByApplicationUserId = performedByApplicationUserId;
        PerformedOnUtc = performedOnUtc.Kind == DateTimeKind.Utc ? performedOnUtc : performedOnUtc.ToUniversalTime();
    }

    public Guid DoctorSpecializationRequestId { get; private set; }
    public DoctorSpecializationRequestAction Action { get; private set; }
    public int RevisionNumber { get; private set; }
    public int? PreviousRevisionNumber { get; private set; }
    public string? Message { get; private set; }
    public Guid PerformedByApplicationUserId { get; private set; }
    public DateTime PerformedOnUtc { get; private set; }
}

public static class DoctorSpecializationErrors
{
    public static Error OpenRequestAlreadyExists => Error.Conflict("DoctorSpecializations.OpenRequestAlreadyExists", ErrorMessage.DoctorSpecializationsOpenRequestAlreadyExists);
    public static Error RequestNotFound => Error.NotFound("DoctorSpecializations.RequestNotFound", ErrorMessage.DoctorSpecializationsRequestNotFound);
    public static Error NoSpecializations => Error.Validation("DoctorSpecializations.NoSpecializations", ErrorMessage.DoctorSpecializationsNoSpecializations);
    public static Error PrimaryRequired => Error.Validation("DoctorSpecializations.PrimaryRequired", ErrorMessage.DoctorSpecializationsPrimaryRequired);
    public static Error MultiplePrimary => Error.Validation("DoctorSpecializations.MultiplePrimary", ErrorMessage.DoctorSpecializationsMultiplePrimary);
    public static Error DuplicateSpecialization => Error.Validation("DoctorSpecializations.DuplicateSpecialization", ErrorMessage.DoctorSpecializationsDuplicateSpecialization);
    public static Error SpecializationNotAvailable => Error.Validation("DoctorSpecializations.SpecializationNotAvailable", ErrorMessage.DoctorSpecializationsSpecializationNotAvailable);
    public static Error SpecializationNoLongerAvailable => Error.Conflict("DoctorSpecializations.SpecializationNoLongerAvailable", ErrorMessage.DoctorSpecializationsSpecializationNoLongerAvailable);
    public static Error InvalidRequestStatus => Error.Conflict("DoctorSpecializations.InvalidRequestStatus", ErrorMessage.DoctorSpecializationsInvalidRequestStatus);
    public static Error ConcurrencyConflict => Error.Conflict("DoctorSpecializations.ConcurrencyConflict", ErrorMessage.DoctorSpecializationsConcurrencyConflict);
}
