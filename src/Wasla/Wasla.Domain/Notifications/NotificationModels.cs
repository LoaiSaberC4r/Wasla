namespace Wasla.Domain.Notifications;

public enum NotificationCategory { SecurityCritical = 1, AccountGovernance, BookingTransactional, AppointmentReminder, ClinicalAvailability, DoctorOperational }
public enum NotificationChannel { Email = 1, InApp }
public enum NotificationIntentStatus { Scheduled = 1, Processing, Dispatched, Cancelled, Skipped, Failed }
public enum NotificationDeliveryStatus { Pending = 1, Queued, Sent, Failed, Suppressed }
public enum NotificationDataClassification { Operational = 1, Clinical }

public static class NotificationPolicy
{
    public const int TemplateVersion = 1;
    public static bool Editable(NotificationCategory category) => category is NotificationCategory.AppointmentReminder or NotificationCategory.ClinicalAvailability;
    public static bool DefaultEnabled(NotificationCategory category, NotificationChannel channel)
        => category != NotificationCategory.ClinicalAvailability || channel != NotificationChannel.Email;
    public static string Language(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
    public static DateTime ReminderDue(DateTime appointmentUtc, int leadMinutes)
    {
        if (appointmentUtc.Kind != DateTimeKind.Utc || leadMinutes is < 1 or > 43200) throw new ArgumentException("Invalid reminder timing.");
        return appointmentUtc.AddMinutes(-leadMinutes);
    }
    public static bool ReminderEligible(DateTime nowUtc, DateTime dueUtc, DateTime appointmentUtc, bool active, string revision, string currentRevision)
        => active && revision == currentRevision && nowUtc >= dueUtc && nowUtc <= dueUtc.AddMinutes(15) && nowUtc < appointmentUtc;
    public static bool EmailAllowed(string eventType) => eventType is "Reservation.Created" or "Reservation.Rescheduled" or
        "Reservation.Cancelled" or "Reservation.RestoredFromNoShow" or "Appointment.ReminderDue" || Clinical(eventType);
    public static bool Clinical(string eventType) => eventType is "Prescription.Available" or "Lab.RequestAvailable" or
        "Radiology.RequestAvailable" or "Lab.OfficialResultAvailable" or "Radiology.OfficialResultAvailable" or
        "DiagnosticResult.Corrected" or "DiagnosticResult.Voided";
    public static bool KnownEvent(string eventType) => EmailAllowed(eventType) || eventType is "Reservation.Expired" or
        "Reservation.MarkedNoShow" or "Patient.CheckedIn" or "Doctor.NewBooking" or "Doctor.BookingCancelled" or
        "Doctor.Approved" or "Doctor.Rejected" or "Doctor.Suspended";
}

public sealed class NotificationEvent
{
    private NotificationEvent() { }
    public NotificationEvent(string eventType, string sourceType, Guid sourceId, string sourceEventKey, string revision,
        Guid? patientId, Guid? doctorId, Guid? practiceId, Guid? targetUser, DateTime now, bool legacyEmailOwned = false)
    {
        if (!NotificationPolicy.KnownEvent(eventType) || sourceId == Guid.Empty || string.IsNullOrWhiteSpace(sourceEventKey) ||
            sourceEventKey.Length > 100 || revision.Length > 100 || now.Kind != DateTimeKind.Utc ||
            sourceType is not ("Reservation" or "Doctor" or "Prescription" or "LabRequest" or "RadiologyRequest" or "LabResult" or "RadiologyResult"))
            throw new ArgumentException("Invalid notification identity.");
        Id = Guid.NewGuid(); EventType = eventType; SourceType = sourceType; SourceId = sourceId; SourceEventKey = sourceEventKey;
        SourceRevisionKey = revision; PatientId = patientId; DoctorId = doctorId; DoctorPracticeId = practiceId;
        TargetApplicationUserId = targetUser; OccurredAtUtc = CreatedAtUtc = now; LegacyEmailOwned = legacyEmailOwned;
        DataClassification = NotificationPolicy.Clinical(eventType) ? NotificationDataClassification.Clinical : NotificationDataClassification.Operational;
        SourceVersionId = DataClassification == NotificationDataClassification.Clinical && Guid.TryParse(revision, out var version) ? version : null;
        Category = DataClassification == NotificationDataClassification.Clinical ? NotificationCategory.ClinicalAvailability :
            eventType == "Appointment.ReminderDue" ? NotificationCategory.AppointmentReminder :
            eventType is "Doctor.NewBooking" or "Doctor.BookingCancelled" ? NotificationCategory.DoctorOperational :
            sourceType == "Doctor" ? NotificationCategory.AccountGovernance : NotificationCategory.BookingTransactional;
        IdempotencyKey = $"{eventType}:{sourceType}:{sourceId:N}:{sourceEventKey}";
    }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = "";
    public string SourceType { get; private set; } = "";
    public Guid SourceId { get; private set; }
    public string SourceEventKey { get; private set; } = "";
    public string SourceRevisionKey { get; private set; } = "";
    public Guid? SourceVersionId { get; private set; }
    public Guid? PatientId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public Guid? DoctorPracticeId { get; private set; }
    public Guid? TargetApplicationUserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public NotificationDataClassification DataClassification { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool LegacyEmailOwned { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public byte[] RowVersion { get; private set; } = [];
}

public sealed class NotificationIntent
{
    private NotificationIntent() { }
    public NotificationIntent(NotificationEvent source, DateTime due, DateTime? appointment = null, int? lead = null, long? settings = null)
    {
        if (due.Kind != DateTimeKind.Utc || appointment.HasValue && (lead is null || settings is null)) throw new ArgumentException("Invalid intent.");
        Id = Guid.NewGuid(); NotificationEventId = source.Id; SourceRevisionKey = source.SourceRevisionKey;
        DueAtUtc = due; CreatedAtUtc = source.CreatedAtUtc; AppointmentStartUtcSnapshot = appointment;
        ReminderLeadMinutesSnapshot = lead; SettingsVersionSnapshot = settings;
        ExpiresAtUtc = appointment.HasValue ? due.AddMinutes(15) : null; Status = NotificationIntentStatus.Scheduled;
    }
    public Guid Id { get; private set; }
    public Guid NotificationEventId { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public NotificationIntentStatus Status { get; private set; }
    public string SourceRevisionKey { get; private set; } = "";
    public DateTime? AppointmentStartUtcSnapshot { get; private set; }
    public int? ReminderLeadMinutesSnapshot { get; private set; }
    public long? SettingsVersionSnapshot { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptOnUtc { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public DateTime? LeaseUntilUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? SkipReasonCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Finish(NotificationIntentStatus status, DateTime now, string? reason = null)
    {
        if (Status is not (NotificationIntentStatus.Scheduled or NotificationIntentStatus.Processing) ||
            status is NotificationIntentStatus.Scheduled or NotificationIntentStatus.Processing) throw new InvalidOperationException("Invalid intent transition.");
        Status = status; ProcessedAtUtc = now; SkipReasonCode = reason; ProcessingToken = null; LeaseUntilUtc = null; NextAttemptOnUtc = null;
    }
}

public sealed class NotificationDelivery
{
    private NotificationDelivery() { }
    public NotificationDelivery(NotificationEvent source, NotificationIntent intent, NotificationChannel channel, string recipientKey,
        Guid? userId, Guid? patientId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(recipientKey) || recipientKey.Length > 100 || !Enum.IsDefined(channel)) throw new ArgumentException("Invalid delivery recipient.");
        Id = Guid.NewGuid(); NotificationEventId = source.Id; NotificationIntentId = intent.Id; Channel = channel;
        RecipientLogicalKey = recipientKey; RecipientApplicationUserId = userId; RecipientPatientId = patientId;
        TemplateCode = source.EventType; TemplateVersion = NotificationPolicy.TemplateVersion;
        LogicalDeliveryKey = $"{source.IdempotencyKey}:{recipientKey}:{channel}"; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid NotificationEventId { get; private set; }
    public Guid NotificationIntentId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; } = NotificationDeliveryStatus.Pending;
    public string RecipientLogicalKey { get; private set; } = "";
    public Guid? RecipientApplicationUserId { get; private set; }
    public Guid? RecipientPatientId { get; private set; }
    public string TemplateCode { get; private set; } = "";
    public int TemplateVersion { get; private set; }
    public string LogicalDeliveryKey { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public string? SafeFailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Queue(DateTime now)
    {
        if (Status != NotificationDeliveryStatus.Pending || Channel == NotificationChannel.InApp && RecipientApplicationUserId is null) throw new InvalidOperationException("Delivery cannot be queued.");
        Status = NotificationDeliveryStatus.Queued; DispatchedAtUtc = now;
    }
    public void Suppress(string code) { Status = NotificationDeliveryStatus.Suppressed; SafeFailureCode = code; }
}

public sealed class UserNotification
{
    private UserNotification() { }
    public UserNotification(NotificationEvent source, NotificationDelivery delivery, DateTime now)
    {
        Id = Guid.NewGuid(); NotificationDeliveryId = delivery.Id;
        ApplicationUserId = delivery.RecipientApplicationUserId ?? throw new ArgumentException("Inbox owner required.");
        Category = source.Category; EventType = TemplateCode = SafePreviewCode = source.EventType;
        TemplateVersion = delivery.TemplateVersion; OccurredAtUtc = source.OccurredAtUtc; CreatedAtUtc = now;
        // Clinical navigation is deliberately generic: never publish a Hidden encounter identity.
        TargetResourceType = source.DataClassification == NotificationDataClassification.Clinical ? "MedicalRecord" : source.SourceType;
        TargetResourceKey = source.DataClassification == NotificationDataClassification.Clinical ? null : source.SourceId.ToString("D");
    }
    public Guid Id { get; private set; }
    public Guid NotificationDeliveryId { get; private set; }
    public Guid ApplicationUserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public string EventType { get; private set; } = "";
    public string TemplateCode { get; private set; } = "";
    public int TemplateVersion { get; private set; }
    public string SafePreviewCode { get; private set; } = "";
    public string TargetResourceType { get; private set; } = "";
    public string? TargetResourceKey { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void MarkRead(DateTime now) => ReadAtUtc ??= now;
}

public sealed class NotificationPreference
{
    private NotificationPreference() { }
    public NotificationPreference(Guid user, NotificationCategory category, NotificationChannel channel, bool enabled, string? language, DateTime now)
    {
        Id = Guid.NewGuid(); ApplicationUserId = user; Category = category; Channel = channel; Update(enabled, language, now);
    }
    public Guid Id { get; private set; }
    public Guid ApplicationUserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool Enabled { get; private set; }
    public string PreferredLanguage { get; private set; } = "ar";
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Update(bool enabled, string? language, DateTime now)
    {
        if (ApplicationUserId == Guid.Empty || !NotificationPolicy.Editable(Category) || !Enum.IsDefined(Channel)) throw new ArgumentException("Preference is not editable.");
        Enabled = enabled; PreferredLanguage = NotificationPolicy.Language(language); UpdatedAtUtc = now;
    }
}

public sealed class PlatformNotificationSettings
{
    public static readonly Guid SingletonId = new("17000000-0000-0000-0000-000000000001");
    public Guid Id { get; private set; } = SingletonId;
    public bool AppointmentReminderEnabled { get; private set; } = true;
    public int ReminderLeadMinutes { get; private set; } = 120;
    public long Version { get; private set; } = 1;
    public DateTime UpdatedAtUtc { get; private set; } = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
    public Guid? UpdatedByApplicationUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = new byte[8];
    public PlatformNotificationSettingsAudit Update(bool enabled, int lead, Guid actor, string reason, DateTime now)
    {
        if (lead is < 1 or > 43200 || actor == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new ArgumentException("Invalid settings.");
        var audit = new PlatformNotificationSettingsAudit(Guid.NewGuid(), actor, Version, Version + 1, ReminderLeadMinutes, lead,
            AppointmentReminderEnabled, enabled, now, reason.Trim());
        AppointmentReminderEnabled = enabled; ReminderLeadMinutes = lead; Version++; UpdatedAtUtc = now; UpdatedByApplicationUserId = actor;
        return audit;
    }
}
public sealed record PlatformNotificationSettingsAudit(Guid Id, Guid ActorApplicationUserId, long PreviousVersion, long NewVersion,
    int PreviousReminderLeadMinutes, int NewReminderLeadMinutes, bool PreviousEnabled, bool NewEnabled, DateTime ChangedAtUtc, string Reason);
public sealed record NotificationAudit(Guid Id, Guid ActorApplicationUserId, Guid? DeliveryId, string Action, string OutcomeCode, string? Reason, DateTime OccurredAtUtc);
