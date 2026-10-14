using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Reporting;

public sealed class PracticeReportingPolicySnapshot : Entity<Guid>
{
    private PracticeReportingPolicySnapshot() { }
    public PracticeReportingPolicySnapshot(Guid id, Guid doctorId, Guid practiceId, long revisionNumber,
        string policyJson, DateTime recordedAtUtc) : base(id)
    { DoctorId = doctorId; PracticeId = practiceId; RevisionNumber = revisionNumber; PolicyJson = policyJson; RecordedAtUtc = recordedAtUtc; }
    public Guid DoctorId { get; private set; }
    public Guid PracticeId { get; private set; }
    public long RevisionNumber { get; private set; }
    public string PolicyJson { get; private set; } = string.Empty;
    public DateTime RecordedAtUtc { get; private set; }
}
