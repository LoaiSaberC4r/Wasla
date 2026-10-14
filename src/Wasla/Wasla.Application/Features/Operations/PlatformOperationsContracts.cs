using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Reporting;

namespace Wasla.Application.Features.Operations;

public sealed record DataQualityItem(string Code, string NameAr, string NameEn, long AffectedCount, ReportDataStatus DataStatus, string DateBasis, string? WarningCode = null);
public sealed record PlatformDataQualityResponse(DateTime GeneratedAtUtc, IReadOnlyList<DataQualityItem> Items, IReadOnlyList<string> Warnings);
public sealed record JobRunMetadata(Guid Id, DateTime StartedAtUtc, DateTime? FinishedAtUtc, string Status, string? FailureCode);
public sealed record OutboxHealth(long Pending, long Processing, long Sent, long Failed, DateTime? OldestPendingAtUtc, long ExpiredClaims,
    string DateBasis = "Current persisted delivery state; no message content");
public sealed record PlatformJobHealthResponse(DateTime GeneratedAtUtc, string GenerationHealth, bool GenerationEnabled,
    int PollingIntervalSeconds, IReadOnlyList<JobRunMetadata> RecentRuns, OutboxHealth Outbox, IReadOnlyList<string> Warnings,
    string ReportingPolicyRefreshHealth = "NotMeasured", IReadOnlyList<JobRunMetadata>? ReportingPolicyRefreshRuns = null);
public sealed record PlatformAttentionItem(Guid Id, string ResourceType, string TitleAr, string TitleEn, string Status,
    DateTime RecordedAtUtc, string? DetailRoute, IReadOnlyList<ReportAction> AvailableActions);
public sealed record PlatformAttentionResponse(DateTime GeneratedAtUtc, long PendingDoctorCount, long PendingSpecializationCount,
    long? OpenInvestigationCount, long? FailedOutboxCount, ReportPage<PlatformAttentionItem> Items, IReadOnlyList<string> Warnings);
public interface IPlatformOperationsService
{
    Task<Result<PlatformDataQualityResponse>> DataQualityAsync(CancellationToken ct);
    Task<Result<PlatformJobHealthResponse>> JobsAsync(CancellationToken ct);
    Task<Result<PlatformAttentionResponse>> AttentionAsync(int pageNumber, int pageSize, CancellationToken ct);
}
