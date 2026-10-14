using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Reporting;

namespace Wasla.Application.Features.Reporting;

public sealed record InsightWindow(DateOnly FromDate, DateOnly ToDate);
public sealed record PracticeInsightEvidence(Guid PracticeId, InsightWindow CurrentWindow, InsightWindow PreviousWindow,
    IReadOnlyList<ReportMetric> CurrentMetrics, IReadOnlyList<ReportMetric> PreviousMetrics, DateTime AsOfUtc);
public sealed record DoctorInsightResponse(Guid InsightId, Guid PracticeId, string MetricCode,
    string TitleAr, string TitleEn, string ExplanationAr, string ExplanationEn,
    InsightWindow CurrentWindow, InsightWindow PreviousWindow, decimal CurrentValue, decimal PreviousValue,
    decimal Numerator, decimal Denominator, decimal PreviousNumerator, decimal PreviousDenominator,
    long SampleCount, DateTime AsOfUtc, string EvidenceSummaryAr, string EvidenceSummaryEn,
    ReportDataStatus DataStatus, string SuggestedActionAr, string SuggestedActionEn,
    DoctorInsightState State, bool IsPersisted, string? RowVersion, IReadOnlyList<ReportAction> AvailableActions);
public sealed record DoctorInsightHistoryResponse(Guid Id, string Operation, DoctorInsightState State,
    Guid ActorApplicationUserId, DateTime RecordedAtUtc, string? Reason, int SequenceNumber);
public sealed record InsightTransitionRequest(string? RowVersion = null, string? Reason = null);
public sealed record TrackDoctorInsightCommand(Guid InsightId, DoctorInsightState TargetState,
    string IdempotencyKey, InsightTransitionRequest Request) : ICommand<DoctorInsightResponse>, ITransactionalCommand<WaslaWritePersistence>;
public interface IInsightEvidenceReader
{
    Task<Result<ReportModel>> GetPracticeWindowAsync(Guid practiceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PracticeInsightEvidence>>> ReadComparisonsAsync(Guid? practiceId, CancellationToken cancellationToken);
}
public interface IDoctorInsightService
{
    Task<Result<ReportPage<DoctorInsightResponse>>> ListAsync(Guid? practiceId, int pageNumber, int pageSize, CancellationToken cancellationToken, DoctorInsightState? state = null);
    Task<Result<DoctorInsightResponse>> DetailAsync(Guid insightId, CancellationToken cancellationToken);
    Task<Result<ReportPage<DoctorInsightHistoryResponse>>> HistoryAsync(Guid insightId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<DoctorInsightResponse>> TrackAsync(TrackDoctorInsightCommand command, CancellationToken cancellationToken);
}
internal sealed class TrackDoctorInsightHandler(IDoctorInsightService service) : ICommandHandler<TrackDoctorInsightCommand, DoctorInsightResponse>
{
    public Task<Result<DoctorInsightResponse>> Handle(TrackDoctorInsightCommand request, CancellationToken cancellationToken)
        => service.TrackAsync(request, cancellationToken);
}
