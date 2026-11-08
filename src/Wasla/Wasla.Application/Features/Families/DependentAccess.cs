using BuildingBlock.Domain.Results;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;

public sealed class DependentManagementOptions
{
    public bool Enabled { get; set; }
    public string OperationalTimeZoneId { get; set; } = "Africa/Cairo";
    public int CandidateReferenceTtlMinutes { get; set; } = 10;
    public int SearchMaxCandidates { get; set; } = 5;
    public int MaxEvidenceFiles { get; set; } = 5;
    public long MaxEvidenceFileBytes { get; set; } = 10 * 1024 * 1024;
    public bool IsValid()
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(OperationalTimeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return false; }
        return CandidateReferenceTtlMinutes is >= 1 and <= 30 && SearchMaxCandidates is >= 1 and <= 5 &&
            MaxEvidenceFiles is >= 1 and <= 5 && MaxEvidenceFileBytes is > 0 and <= 10485760;
    }
    public DateOnly Today(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        TimeZoneInfo.FindSystemTimeZoneById(OperationalTimeZoneId)));
}

public sealed record DependentOperationalScope(Guid ActorUserId, Guid AdultPatientId, Guid SubjectPatientId,
    Guid FamilyId, Guid AuthorityId, FamilyMemberRole ParentRole, string Version);
public interface IDependentAccessPolicy
{
    Task<Result<DependentOperationalScope>> AuthorizeAsync(Guid childPatientId, string permission, CancellationToken ct);
}
public interface IOperationalAuthorityReader
{
    Task<ParentChildOperationalAuthority?> FindAsync(Guid adultPatientId, Guid childPatientId, CancellationToken ct);
}
public interface IFamilyOperationalVerification
{
    Task<Result> RecordAsync(FamilyRelationshipRequest request, Family family, CancellationToken ct);
    Task<Result> RevokeAsync(Guid authorityId, string rowVersion, string reason, string idempotencyKey, CancellationToken ct);
    Task<Result<IReadOnlyList<OperationalAuthorityResponse>>> ListAsync(Guid relationshipRequestId, CancellationToken ct);
}
public sealed record OperationalAuthorityResponse(Guid AuthorityId, Guid FamilyId, Guid ParentPatientId, Guid ChildPatientId,
    bool IsActive, Guid ApprovedRelationshipRequestId, int ApprovedRevisionNumber, string RowVersion);
public static class DependentAccessErrors
{
    public static Error Unavailable => Error.NotFound("DependentAccess.Unavailable", Wasla.Domain.Resources.FamilySelfServiceText.Get("DependentAccess_Unavailable"));
    public static Error ProofRequired => Error.Conflict("DependentAccess.OperationalProofRequired", Wasla.Domain.Resources.FamilySelfServiceText.Get("DependentAccess_OperationalProofRequired"));
}
