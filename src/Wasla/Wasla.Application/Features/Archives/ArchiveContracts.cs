using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;

namespace Wasla.Application.Features.Archives;

public sealed record ArchivePage<T>(IReadOnlyList<T> Items, string? NextCursor, int PageSize, DateTime AsOfUtc)
{ public bool HasMore => NextCursor != null; }
public sealed record ArchiveFilter(DateOnly? FromBusinessDate = null, DateOnly? ToBusinessDate = null,
    string? Status = null, string? Reference = null, int? TicketNumber = null, Guid? PatientId = null,
    Guid? PracticeId = null, string? Search = null, string? Cursor = null, int PageSize = 20);
public sealed record ArchivedTicketItem(Guid TicketId, Guid DoctorPracticeId, int TicketNumber, DateOnly BusinessDate,
    string Status, DateTime ArchivedAtUtc, string DetailsRoute, bool IsArchived = true);
public sealed record ArchivedReservationItem(Guid ReservationId, Guid DoctorPracticeId, string ReservationReference,
    DateOnly BusinessDate, string Status, DateTime ArchivedAtUtc, string DetailsRoute, Guid? LinkedTicketId = null, bool IsArchived = true);
public sealed record PlatformTicketMetadata(Guid TicketId, Guid DoctorPracticeId, int TicketNumber,
    DateOnly BusinessDate, string Status, DateTime ArchivedAtUtc, string DetailsRoute, bool IsArchived = true);
public sealed record PlatformReservationMetadata(Guid ReservationId, Guid DoctorPracticeId, string ReservationReference,
    DateOnly BusinessDate, string Status, DateTime ArchivedAtUtc, string DetailsRoute, bool IsArchived = true);
public sealed record ArchiveOption(string Code, string NameAr, string NameEn);
public sealed record ArchiveMetadata(IReadOnlyList<ArchiveOption> TicketStatuses, IReadOnlyList<ArchiveOption> ReservationStatuses,
    IReadOnlyList<ArchiveOption> EntityTypes, IReadOnlyList<string> SupportedFilters, string DateBasis);
public sealed record PracticeOption(Guid PracticeId, string NameAr, string? NameEn);
public sealed record ActorOption(Guid ActorId, string NameAr, string? NameEn);
public sealed record ArchivePatientOption(Guid PatientId, string NameAr, string? NameEn, bool IsSelf);
public sealed record OptionsPage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long TotalCount);
public sealed record ArchiveControlBody(string Reason, string RowVersion);
public sealed record ArchiveControlResponse(string State, string RowVersion, bool CanPause, bool CanResume,
    DateTime? PausedAtUtc, Guid? PausedByActorId);
public sealed record ArchiveHealth(bool Enabled, bool Ready, bool Paused, string CurrentMode,
    DateTime? LastSuccessUtc, DateTime? LastAttemptUtc, int RunningBatchCount, long IntegrityExceptionCount,
    ArchiveControlResponse Control, Guid? LastRunId, Guid? CurrentRunId);
public sealed record ArchiveRunItem(Guid RunId, string Mode, string Outcome, DateTime StartedAtUtc, DateTime? EndedAtUtc,
    int Scanned, int ArchivedTickets, int ArchivedReservations, int Excluded, string CorrelationId);
public sealed record ArchiveExceptionItem(Guid ExceptionId, Guid EntityId, Guid DoctorPracticeId, Guid RunId,
    string EntityType, string FailureCode, DateTime OccurredAtUtc);
public sealed record ArchiveOperationsFilter(DateTime? FromUtc = null, DateTime? ToUtc = null, string? Mode = null,
    string? Outcome = null, string? FailureCode = null, string? EntityType = null, string? Type = null,
    Guid? PracticeId = null, string? Cursor = null, int PageSize = 20);
public sealed record ArchiveOperationsOptions(IReadOnlyList<ArchiveOption> Modes, IReadOnlyList<ArchiveOption> Outcomes,
    IReadOnlyList<ArchiveOption> FailureCodes, IReadOnlyList<ArchiveOption> EntityTypes);
public sealed record AuditFilter(DateTime? FromUtc = null, DateTime? ToUtc = null, string? Action = null,
    string? EntityType = null, Guid? EntityId = null, Guid? ActorId = null, string? Outcome = null,
    Guid? PracticeId = null, string? Cursor = null, int PageSize = 20);
public sealed record RestrictedAuditSearch(string Reason, AuditFilter Filters);
public sealed record SafeAuditEvent(string EventKey, DateTime OccurredAtUtc, string Action, string EntityType,
    Guid? EntityId, Guid? PracticeId, Guid? ActorId, string ActorType, string Outcome,
    string? FailureCode = null, string? CorrelationId = null, Guid? RunId = null);
public sealed record AuditOptions(IReadOnlyList<ArchiveOption> Actions, IReadOnlyList<ArchiveOption> EntityTypes,
    IReadOnlyList<ArchiveOption> Outcomes, OptionsPage<ActorOption> Actors, OptionsPage<PracticeOption> Practices);

public static class ArchiveErrors
{
    public static readonly Error Denied = Error.Security("Archive.AccessDenied", "Access denied / غير مسموح.");
    public static readonly Error NotFound = Error.NotFound("Archive.NotFound", "Resource not found / غير موجود.");
    public static readonly Error InvalidFilter = Error.Validation("Archive.InvalidFilter", "Invalid filters / عوامل تصفية غير صالحة.");
    public static readonly Error InvalidCursor = Error.Validation("Archive.InvalidCursor", "Invalid or incompatible cursor / مؤشر غير صالح.");
    public static readonly Error Conflict = Error.Conflict("Archive.ConcurrencyConflict", "Refresh the control state / حدث تعارض؛ حدّث الحالة.");
    public static readonly Error IdempotencyConflict = Error.Conflict("Archive.IdempotencyConflict", "Idempotency key conflict / تعارض مفتاح الطلب.");
    public static readonly Error NotReady = Error.Infra("Archive.NotReady", "Archive readiness checks have not passed / الأرشفة غير جاهزة.");
}

public interface IArchiveQueries
{
    Task<Result<OptionsPage<ArchivePatientOption>>> PatientsAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<Result<ArchivePage<ArchivedTicketItem>>> TicketsAsync(ReportActor actor, Guid? practice, ArchiveFilter filter, CancellationToken ct);
    Task<Result<ArchivePage<ArchivedReservationItem>>> ReservationsAsync(ReportActor actor, Guid? practice, ArchiveFilter filter, CancellationToken ct);
    Task<Result<ArchivePage<PlatformTicketMetadata>>> PlatformTicketsAsync(ArchiveFilter filter, CancellationToken ct);
    Task<Result<ArchivePage<PlatformReservationMetadata>>> PlatformReservationsAsync(ArchiveFilter filter, CancellationToken ct);
    Task<Result<PlatformTicketMetadata>> PlatformTicketAsync(Guid id, CancellationToken ct);
    Task<Result<PlatformReservationMetadata>> PlatformReservationAsync(Guid id, CancellationToken ct);
    Task<Result<OptionsPage<PracticeOption>>> PracticesAsync(ReportActor actor, string? search, int pageNumber, int pageSize, CancellationToken ct);
}
public interface IArchiveOperations
{
    Task<Result<ArchiveHealth>> HealthAsync(CancellationToken ct);
    Task<Result<ArchivePage<ArchiveRunItem>>> RunsAsync(ArchiveOperationsFilter filter, CancellationToken ct);
    Task<Result<ArchiveRunItem>> RunAsync(Guid id, CancellationToken ct);
    Task<Result<ArchivePage<ArchiveExceptionItem>>> ExceptionsAsync(ArchiveOperationsFilter filter, CancellationToken ct);
    Task<Result<ArchiveExceptionItem>> ExceptionAsync(Guid id, CancellationToken ct);
    Task<Result<ArchiveControlResponse>> ControlAsync(bool pause, ArchiveControlBody body, string key, CancellationToken ct);
    Task<Result<ArchiveOperationsOptions>> OptionsAsync(CancellationToken ct);
}
public interface IAuditExplorer
{
    Task<Result<ArchivePage<SafeAuditEvent>>> SearchAsync(ReportActor actor, Guid? practice, AuditFilter filter, bool sensitive, string? reason, CancellationToken ct);
    Task<Result<SafeAuditEvent>> DetailAsync(ReportActor actor, Guid? practice, string eventKey, CancellationToken ct);
    Task<Result<AuditOptions>> OptionsAsync(ReportActor actor, Guid? practice, string? search, int pageNumber, int pageSize, CancellationToken ct);
}

public interface IArchiveRequestAudit
{
    Task RejectedAsync(bool controlRequest, int statusCode, CancellationToken ct);
}
