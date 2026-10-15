namespace Wasla.Domain.Security;

public static class Phase18PartTwoPermissionNames
{
    public const string TicketsOwn = "Archives.Tickets.ViewOwn";
    public const string ReservationsOwn = "Archives.Reservations.ViewOwn";
    public const string TicketsPractice = "Archives.Tickets.ViewPractice";
    public const string ReservationsPractice = "Archives.Reservations.ViewPractice";
    public const string TicketsAssigned = "Archives.Tickets.ViewAssigned";
    public const string ReservationsAssigned = "Archives.Reservations.ViewAssigned";
    public const string TicketsPlatform = "Archives.Tickets.ViewPlatformMetadata";
    public const string ReservationsPlatform = "Archives.Reservations.ViewPlatformMetadata";
    public const string Health = "Archives.Operations.ViewHealth";
    public const string Failures = "Archives.Operations.ViewFailures";
    public const string Runs = "Archives.Operations.ViewRuns";
    public const string Pause = "Archives.Operations.Pause";
    public const string Resume = "Archives.Operations.Resume";
    public const string AuditPractice = "Audit.Events.ViewPractice";
    public const string AuditOwnActivity = "Audit.Events.ViewOwnActivity";
    public const string AuditPlatform = "Audit.Events.ViewPlatform";
    public const string SensitivePractice = "Audit.SensitiveAccess.ViewPracticeMetadata";
    public const string SensitiveRestricted = "Audit.SensitiveAccess.ViewRestrictedMetadata";

    public static readonly IReadOnlyList<string> PatientDefaults = [TicketsOwn, ReservationsOwn];
    public static readonly IReadOnlyList<string> DoctorDefaults = [TicketsPractice, ReservationsPractice, AuditPractice, SensitivePractice];
    public static readonly IReadOnlyList<string> ReceptionDefaults = [TicketsAssigned, ReservationsAssigned, AuditOwnActivity];
    public static readonly IReadOnlyList<string> PlatformDefaults = [TicketsPlatform, ReservationsPlatform, Health, Failures, Runs, Pause, Resume, AuditPlatform];
    // Append-only: order is part of the permanent permission GUID contract.
    public static readonly IReadOnlyList<string> All = [.. PatientDefaults, TicketsPractice, ReservationsPractice,
        TicketsAssigned, ReservationsAssigned, .. PlatformDefaults, AuditPractice, AuditOwnActivity, SensitivePractice, SensitiveRestricted];
}
