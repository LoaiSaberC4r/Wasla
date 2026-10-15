using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reporting;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using Wasla.Application.Features.Practices;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Tickets.Common;

public sealed record TicketAttemptResponse(
    int CallCycle,
    int AttemptNumber,
    TicketCallAttemptOutcome Outcome,
    DateTime CalledOnUtc,
    DateTime? OutcomeRecordedOnUtc);

public sealed record TicketDetailsResponse(
    Guid TicketId,
    int TicketNumber,
    TicketStatus Status,
    TicketSource Source,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    Guid? ReservationId,
    DateOnly BusinessDate,
    Guid SegmentId,
    string SegmentNameAr,
    string? SegmentNameEn,
    int SegmentPriority,
    Guid VisitTypeId,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    decimal PriceSnapshot,
    DateTime CheckInTimeUtc,
    DateTime QueueOrderTimeUtc,
    int PatientsAheadNow,
    bool IsFastTrack,
    DateTime LastUpdatedOnUtc,
    IReadOnlyList<TicketAttemptResponse> CallAttempts,
    string RowVersion,
    bool CanRefund,
    bool IsRefunded,
    decimal RefundableAmount,
    string CurrencyCode,
    Guid? PaymentId,
    string? PaymentTransactionNumber,
    Guid? RefundId,
    string? RefundTransactionNumber,
    Guid? MedicalEncounterId = null,
    Guid? FollowUpEligibilityId = null,
    string? MedicalEncounterRowVersion = null,
    Guid? PrescriptionId = null,
    bool IsArchived = false,
    DateTime? ArchivedAtUtc = null,
    TicketOperationalCapabilities? OperationalCapabilities = null,
    IReadOnlyList<TicketTimelineItem>? Timeline = null,
    bool PatientsAheadNowIsCurrent = true,
    bool TimelineHasMore = false);
public sealed record TicketOperationalCapabilities(bool CanCancel, bool CanRestore, bool CanQueueMutate, bool CanMutateOperationalRecord)
{
    // Admission creates the Ticket; an existing Ticket can never be checked in again.
    public bool CanCheckIn { get; }
}
public sealed record TicketTimelineItem(Guid EventId, string EventType, DateTime OccurredAtUtc, string? ReasonCode);

public sealed record PracticeQueueTicketResponse(
    Guid TicketId,
    int TicketNumber,
    TicketStatus Status,
    Guid PatientId,
    string PatientNameAr,
    string? PatientNameEn,
    string SegmentNameAr,
    string? SegmentNameEn,
    int SegmentPriority,
    string VisitTypeCode,
    string VisitTypeNameAr,
    string? VisitTypeNameEn,
    DateTime CheckInTimeUtc,
    DateTime QueueOrderTimeUtc,
    bool IsFastTrack,
    int CallCycle,
    int AttemptsInCurrentCycle,
    TicketCallAttemptOutcome? LatestAttemptOutcome,
    string RowVersion);

public sealed record PracticeQueueResponse(
    Guid DoctorPracticeId,
    DateOnly BusinessDate,
    PracticeQueueTicketResponse? InProgress,
    PracticeQueueTicketResponse? Called,
    IReadOnlyList<PracticeQueueTicketResponse> Waiting,
    IReadOnlyList<PracticeQueueTicketResponse> NoShow);

public sealed record MyActiveTicketResponse(
    Guid TicketId,
    int TicketNumber,
    TicketStatus Status,
    Guid DoctorPracticeId,
    string PracticeNameAr,
    string? PracticeNameEn,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    int PatientsAheadNow,
    DateTime LastUpdatedOnUtc);

public interface ITicketNumberAllocator
{
    Task<int> AllocateNextAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}

public interface ITicketQueueLock
{
    Task AcquirePracticeDayAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task AcquirePatientPracticeAsync(
        Guid patientId,
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default);

    Task AcquireIdempotencyAsync(
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public interface ITicketQueueReader
{
    Task<bool> HasOpenTicketAsync(
        Guid patientId,
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasCalledOrInProgressAsync(
        Guid doctorPracticeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasInProgressAsync(
        Guid doctorPracticeId,
        Guid? exceptTicketId,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindNextWaitingTicketIdAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<int> CountInProgressTransitionsAfterAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        DateTime afterUtc,
        CancellationToken cancellationToken = default);

    Task<TicketDetailsResponse?> GetDetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<PracticeQueueResponse> GetPracticeQueueAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MyActiveTicketResponse>> ListPatientActiveAsync(
        Guid patientId,
        CancellationToken cancellationToken = default);
}

public interface IReservationNoShowRuntimeReader
{
    Task<bool> HasSameDayNoShowAsync(
        Guid patientId,
        Guid doctorPracticeId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> FindEligibleReservationIdsAsync(
        Guid doctorPracticeId,
        DateOnly businessDate,
        DateTime nowUtc,
        int checkInGracePeriodMinutes,
        int passedPatientsThreshold,
        CancellationToken cancellationToken = default);
}

internal sealed record TicketActor(Guid ApplicationUserId, bool IsDoctor, bool IsReception);

internal sealed class TicketAccessService(
    ICurrentUser currentUser,
    IReceptionPracticeAuthorizationService receptionAuthorization,
    IReadRepository<ApplicationUser, WaslaReadPersistence> users,
    IReadRepository<Doctor, WaslaReadPersistence> doctors,
    IReadRepository<DoctorPractice, WaslaReadPersistence> practices,
    IReportingAccessGuard archiveGuard)
{
    public async Task<Result<TicketActor>> AuthorizeReceptionAsync(
        Guid practiceId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (permission.StartsWith("Archives.", StringComparison.Ordinal))
        {
            var scopedArchive = await archiveGuard.AuthorizeAsync(ReportActor.Reception, permission, practiceId, null, cancellationToken);
            return scopedArchive.IsFailure ? Result<TicketActor>.Fail(scopedArchive.Errors) : Result<TicketActor>.Ok(new(scopedArchive.Value.ActorId, false, true));
        }
        if (!TryGetActor(out var actorId) || !HasRole(SystemRoleNames.Reception))
        {
            return Result<TicketActor>.Fail(TicketErrors.AccessDenied);
        }

        var authorized = await receptionAuthorization.AuthorizeAsync(
            practiceId, permission, cancellationToken);
        return authorized.IsFailure
            ? Result<TicketActor>.Fail(TicketErrors.AccessDenied)
            : Result<TicketActor>.Ok(new TicketActor(actorId, false, true));
    }

    public async Task<Result<TicketActor>> AuthorizeDoctorAsync(
        Guid practiceId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (permission.StartsWith("Archives.", StringComparison.Ordinal))
        {
            var archiveAccess = await archiveGuard.AuthorizeAsync(ReportActor.Doctor, permission, practiceId, null, cancellationToken);
            return archiveAccess.IsFailure ? Result<TicketActor>.Fail(archiveAccess.Errors) : Result<TicketActor>.Ok(new(archiveAccess.Value.ActorId, true, false));
        }
        if (!TryGetActor(out var actorId) || !HasRole(SystemRoleNames.Doctor) ||
            !currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
        {
            return Result<TicketActor>.Fail(TicketErrors.AccessDenied);
        }

        var user = await users.GetByIdAsync(actorId, cancellationToken);
        if (user is null || !user.IsActive || user.IsFirstLogin)
        {
            return Result<TicketActor>.Fail(TicketErrors.AccessDenied);
        }

        var doctor = await doctors.GetByPropertyAsync(
            item => item.ApplicationUserId == actorId, cancellationToken);
        if ((await archiveGuard.AuthorizeAsync(ReportActor.Doctor, permission, practiceId, null, cancellationToken)).IsFailure)
            return Result<TicketActor>.Fail(TicketErrors.AccessDenied);
        var practice = await practices.GetByIdAsync(practiceId, cancellationToken);
        return doctor is not null && doctor.ApprovalStatus == DoctorApprovalStatus.Approved &&
               practice is not null && practice.IsActive && practice.DoctorId == doctor.Id
            ? Result<TicketActor>.Ok(new TicketActor(actorId, true, false))
            : Result<TicketActor>.Fail(TicketErrors.AccessDenied);
    }

    public async Task<Result<TicketActor>> AuthorizeDoctorOrReceptionAsync(
        Guid practiceId,
        string doctorPermission,
        string receptionPermission,
        CancellationToken cancellationToken)
    {
        if (HasRole(SystemRoleNames.Doctor))
        {
            return await AuthorizeDoctorAsync(practiceId, doctorPermission, cancellationToken);
        }

        return await AuthorizeReceptionAsync(practiceId, receptionPermission, cancellationToken);
    }

    public async Task<Result<Guid>> ResolveOwnPatientIdAsync(CancellationToken cancellationToken, bool archived = false)
    {
        var scope = await archiveGuard.AuthorizeAsync(ReportActor.Patient, archived ? Phase18PartTwoPermissionNames.TicketsOwn : PermissionNames.TicketsViewOwn, null, null, cancellationToken);
        return scope.IsFailure || scope.Value.PatientId is not { } own ? Result<Guid>.Fail(TicketErrors.AccessDenied) : Result<Guid>.Ok(own);
    }

    private bool TryGetActor(out Guid actorId)
    {
        actorId = currentUser.UserId.GetValueOrDefault();
        return currentUser.IsAuthenticated && actorId != Guid.Empty;
    }

    private bool HasRole(string role)
        => currentUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

internal sealed class TicketByIdForUpdateSpecification : Specification<Ticket>
{
    public TicketByIdForUpdateSpecification(Guid ticketId)
    {
        AddCriteria(item => item.Id == ticketId);
        AddInclude(item => item.History);
        AddInclude(item => item.CallAttempts);
        UseTracking();
    }
}

internal sealed class ReservationForTicketSpecification : Specification<Reservation>
{
    public ReservationForTicketSpecification(Guid reservationId)
    {
        AddCriteria(item => item.Id == reservationId);
        AddInclude(item => item.History);
        UseTracking();
    }
}

internal static class TicketBusinessClock
{
    public static DateOnly CurrentBusinessDate(DateTime nowUtc, string timeZoneId)
    {
        var utc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}

internal static class TicketRowVersion
{
    public static string Encode(byte[] value) => Convert.ToBase64String(value);

    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value) is { Length: > 0 };
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool Matches(byte[] current, string supplied)
    {
        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
