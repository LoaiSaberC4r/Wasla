using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Reservations;
using Wasla.Domain.Tickets;

namespace Wasla.Application.Features.Operations;

public enum WorklistCategory { UpcomingBooking = 1, AwaitingCheckIn = 2, LateArrival = 3, QueueAttention = 4,
    ReservationNoShow = 5, TicketNoShow = 6, OpenHandover = 7 }
public sealed record WorklistFilter(DateOnly? BusinessDate = null, WorklistCategory? Category = null, Guid? SegmentId = null,
    Guid? VisitTypeId = null, ReservationStatus? ReservationStatus = null, TicketStatus? TicketStatus = null,
    int PageNumber = 1, int PageSize = 20);
public sealed record WorklistItem(Guid Id, string ResourceType, WorklistCategory Category, string CategoryAr,
    string CategoryEn, DateOnly BusinessDate, DateTime OccurredAtUtc, string Reference, string Status,
    Guid? PatientId, string? PatientNameAr, string? PatientNameEn, IReadOnlyList<ReportAction> AvailableActions);
public sealed record ReceptionAttentionResponse(DateTime AsOfUtc, IReadOnlyList<ReportMetric> Metrics,
    ReportPage<WorklistItem> Worklist, IReadOnlyList<string> Warnings);
public interface IReceptionOperationsService
{
    Task<Result<ReportPage<WorklistItem>>> WorklistAsync(Guid practiceId, WorklistFilter filter, CancellationToken cancellationToken);
    Task<Result<ReceptionAttentionResponse>> AttentionAsync(Guid? practiceId, CancellationToken cancellationToken);
}
