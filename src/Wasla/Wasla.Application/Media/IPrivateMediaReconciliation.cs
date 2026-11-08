namespace Wasla.Application.Media;

public sealed record PrivateMediaReconciliationReport(long ExaminedFiles, long StaleUnreferencedCandidates,
    long ExaminedReferences, long MissingReferencedFiles, bool HasMore, IReadOnlyList<PrivateMediaModuleReport>? Modules = null);
public sealed record PrivateMediaModuleReport(string Module, Guid ReportId, long ExaminedFiles, long ExaminedReferences,
    long StaleUnreferencedCandidates, long MissingReferencedFiles, bool HasMore);
public interface IPrivateMediaReconciliation
{
    /// <summary>Detect discrepancies without deleting retained evidence or uncertain-commit uploads.</summary>
    Task<PrivateMediaReconciliationReport> InspectAsync(CancellationToken ct);
}
