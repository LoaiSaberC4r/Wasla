using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;
using Wasla.Application.Persistence;
using Wasla.Domain.Operations;

namespace Wasla.Application.Features.Operations;

public sealed record HandoverCreateRequest(string Summary, Guid? ReservationId = null, Guid? TicketId = null, Guid? PreviousHandoverId = null);
public sealed record HandoverActionRequest(string RowVersion, string? Outcome = null, string? Annotation = null);
public sealed record HandoverFilter(Guid? PracticeId = null, HandoverStatus? Status = null, DateOnly? FromDate = null,
    DateOnly? ToDate = null, int PageNumber = 1, int PageSize = 20, string SortDirection = "desc");
public sealed record HandoverResponse(Guid Id, Guid DoctorPracticeId, string Summary, Guid? ReservationId,
    Guid? TicketId, Guid? PreviousHandoverId, HandoverStatus Status, Guid CreatedByApplicationUserId, DateTime CreatedOnUtc,
    Guid? AcknowledgedByApplicationUserId, DateTime? AcknowledgedOnUtc, Guid? ClosedByApplicationUserId,
    DateTime? ClosedOnUtc, string? ClosureOutcome, string RowVersion, IReadOnlyList<ReportAction> AvailableActions);
public sealed record HandoverHistoryResponse(Guid Id, string Operation, HandoverStatus Status, Guid ActorApplicationUserId,
    DateTime RecordedAtUtc, string? Note, int SequenceNumber);
public enum HandoverOperation { Create = 1, Acknowledge = 2, Close = 3, Annotate = 4 }
public sealed record WriteHandoverCommand(ReportActor Actor, Guid PracticeId, Guid? HandoverId, HandoverOperation Operation,
    string IdempotencyKey, HandoverCreateRequest? Create = null, HandoverActionRequest? Action = null)
    : ICommand<HandoverResponse>, ITransactionalCommand<WaslaWritePersistence>;
public interface IHandoverService
{
    Task<Result<ReportPage<HandoverResponse>>> ListAsync(ReportActor actor, HandoverFilter filter, CancellationToken cancellationToken);
    Task<Result<HandoverResponse>> DetailAsync(ReportActor actor, Guid practiceId, Guid handoverId, CancellationToken cancellationToken);
    Task<Result<ReportPage<HandoverHistoryResponse>>> HistoryAsync(ReportActor actor, Guid practiceId, Guid handoverId,
        int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<HandoverResponse>> WriteAsync(WriteHandoverCommand command, CancellationToken cancellationToken);
}
internal sealed class WriteHandoverHandler(IHandoverService service) : ICommandHandler<WriteHandoverCommand, HandoverResponse>
{
    public Task<Result<HandoverResponse>> Handle(WriteHandoverCommand request, CancellationToken cancellationToken)
        => service.WriteAsync(request, cancellationToken);
}
