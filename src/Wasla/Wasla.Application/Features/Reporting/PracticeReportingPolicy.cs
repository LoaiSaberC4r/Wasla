using Wasla.Domain.Practices;

namespace Wasla.Application.Features.Reporting;

public sealed record ReportingSchedulePeriod(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes);
public sealed record ReportingScheduleOverride(Guid Id, DateOnly Date, DoctorPracticeScheduleExceptionType Type,
    TimeOnly? StartTime, TimeOnly? EndTime, int? SlotDurationMinutes);
public sealed record ReportingSegmentPolicy(Guid Id, bool IsActive, int? ReservedDailyQuota);
public sealed record ReportingVisitTypePolicy(Guid Id, bool IsActive);
public sealed record PracticeReportingPolicy(Guid PracticeId, Guid DoctorId, Guid OwnerUserId, bool IsActive, string TimeZoneId,
    int? MaximumDailyPatients, IReadOnlyList<ReportingSchedulePeriod> Periods,
    IReadOnlyList<ReportingScheduleOverride> Exceptions, IReadOnlyList<ReportingSegmentPolicy> Segments,
    IReadOnlyList<ReportingVisitTypePolicy> VisitTypes);
