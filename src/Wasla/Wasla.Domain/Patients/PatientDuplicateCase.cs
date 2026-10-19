using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
namespace Wasla.Domain.Patients;
public enum PatientDuplicateStatus { NeedsMoreEvidence = 1, ConfirmedDuplicate = 2, DistinctPatients = 3 }
public enum PatientDuplicateTrigger { SoftMatch = 1, StaffReview = 2 }
public sealed class PatientDuplicateCase : Entity<Guid>
{
    private PatientDuplicateCase() { }
    public PatientDuplicateCase(Guid id, Guid first, Guid second, PatientDuplicateTrigger trigger, string indicators, Guid? actor, DateTime now) : base(id)
    {
        if (first == second || first == Guid.Empty || second == Guid.Empty) throw new ArgumentException("Distinct patients required.");
        // SQL Server GUID ordering differs from .NET. Canonicalize using ordinal hex and store the chosen pair explicitly.
        var ordered = string.CompareOrdinal(first.ToString("N"), second.ToString("N")) < 0;
        FirstPatientId = LowerPatientId = ordered ? first : second; SecondPatientId = HigherPatientId = ordered ? second : first;
        Trigger = trigger; MatchIndicators = indicators; CreatedByApplicationUserId = actor; CreatedOnUtc = now;
    }
    public Guid FirstPatientId { get; private set; }
    public Guid SecondPatientId { get; private set; }
    public Guid LowerPatientId { get; private set; }
    public Guid HigherPatientId { get; private set; }
    public PatientDuplicateStatus Status { get; private set; } = PatientDuplicateStatus.NeedsMoreEvidence;
    public PatientDuplicateTrigger Trigger { get; private set; }
    public string MatchIndicators { get; private set; } = string.Empty;
    public Guid? CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public Guid? ResolvedBySuperAdminApplicationUserId { get; private set; }
    public DateTime? ResolvedOnUtc { get; private set; }
    public string? ResolutionReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Result Resolve(PatientDuplicateStatus resolution, Guid actor, string reason, DateTime now)
    {
        if (Status != PatientDuplicateStatus.NeedsMoreEvidence || !Enum.IsDefined(resolution) || actor == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return Result.Fail(PatientIdentityErrors.Conflict("PatientDuplicates.InvalidResolution"));
        Status = resolution; ResolutionReason = reason.Trim(); ResolvedBySuperAdminApplicationUserId = actor; ResolvedOnUtc = now; return Result.Ok();
    }
}
