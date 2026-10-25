namespace Wasla.Application.Features.Notifications;

public sealed class NotificationOptions
{
    public bool Enabled { get; set; } = true;
    public bool DispatchEnabled { get; set; } = true;
    // Legacy ownership is recorded per event; changing this switch cannot resend history.
    public bool OwnReservationEmails { get; set; }
    public int PollingIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 50;
    public int ClaimLeaseSeconds { get; set; } = 120;
    public int MaxAttempts { get; set; } = 5;
}
public sealed class NotificationPrivacyOptions
{
    public bool ClinicalEmailReleaseApproved { get; set; }
    public bool ClinicalInAppReleaseApproved { get; set; }
}
