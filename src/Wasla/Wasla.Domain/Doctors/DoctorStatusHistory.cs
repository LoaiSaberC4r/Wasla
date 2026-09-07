using BuildingBlock.Domain.EntitiesHelper;
using Wasla.Domain.Common;

namespace Wasla.Domain.Doctors;

public sealed class DoctorStatusHistory : Entity<Guid>
{
    private DoctorStatusHistory()
    {
    }

    public DoctorStatusHistory(
        Guid id,
        Guid doctorId,
        DoctorApprovalStatus? fromStatus,
        DoctorApprovalStatus toStatus,
        string? reason,
        Guid? performedByApplicationUserId,
        DateTime occurredOnUtc)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(doctorId, Guid.Empty);
        DoctorId = doctorId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        PerformedByApplicationUserId = performedByApplicationUserId;
        OccurredOnUtc = occurredOnUtc.Kind == DateTimeKind.Utc
            ? occurredOnUtc
            : occurredOnUtc.ToUniversalTime();
    }

    public Guid DoctorId { get; private set; }
    public DoctorApprovalStatus? FromStatus { get; private set; }
    public DoctorApprovalStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public Guid? PerformedByApplicationUserId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
}
