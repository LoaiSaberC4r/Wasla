using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;

namespace Wasla.Tests.Unit;

public sealed class Phase17NotificationTests
{
    private static readonly DateTime Appointment = new(2026, 10, 25, 20, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData("A04", 0, true)]
    [InlineData("A05", 900, true)]
    [InlineData("A06", 901, false)]
    [InlineData("E17", 1, true)]
    public void Grace_window_is_inclusive_and_has_no_quiet_hours(string caseId, int secondsAfterDue, bool expected)
    {
        Assert.NotEmpty(caseId);
        var due = NotificationPolicy.ReminderDue(Appointment, 120);
        Assert.Equal(expected, NotificationPolicy.ReminderEligible(due.AddSeconds(secondsAfterDue), due, Appointment, true, "revision", "revision"));
    }
    [Theory]
    [InlineData("A07", false, true)]
    [InlineData("A15", true, false)]
    [InlineData("A09", false, false)]
    public void Source_status_revision_and_future_appointment_are_mandatory(string caseId, bool revisionMatches, bool active)
    {
        Assert.NotEmpty(caseId);
        var due = Appointment.AddMinutes(-5);
        Assert.False(NotificationPolicy.ReminderEligible(Appointment, due, Appointment, active, "one", revisionMatches ? "one" : "two"));
        if (!active || !revisionMatches) Assert.False(NotificationPolicy.ReminderEligible(due, due, Appointment, active, "one", revisionMatches ? "one" : "two"));
    }
    [Theory]
    [InlineData("A20", 120)]
    [InlineData("A21", 1)]
    [InlineData("A18", 43200)]
    public void Due_time_uses_only_committed_utc_and_configured_snapshot(string caseId, int lead)
    {
        Assert.NotEmpty(caseId);
        Assert.Equal(Appointment.AddMinutes(-lead), NotificationPolicy.ReminderDue(Appointment, lead));
        Assert.Throws<ArgumentException>(() => NotificationPolicy.ReminderDue(DateTime.SpecifyKind(Appointment, DateTimeKind.Unspecified), lead));
    }
    [Fact]
    public void E14_E15_E16_templates_fallback_version_and_encode_only_safe_booking_fields()
    {
        var ar = NotificationTemplates.Render("Appointment.ReminderDue", 1, "unsupported", "<script>\r\n", Appointment);
        Assert.Equal(ar, NotificationTemplates.Render("Appointment.ReminderDue", 1, "ar", "<script>\r\n", Appointment));
        Assert.Contains("&lt;script&gt;", ar.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", ar.HtmlBody, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => NotificationTemplates.Render("Prescription.Available", 999));
        var clinical = NotificationTemplates.Render("Prescription.Available", 1, "en", "SENSITIVE_REFERENCE", Appointment);
        Assert.DoesNotContain("SENSITIVE_REFERENCE", clinical.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("2026", clinical.TextBody, StringComparison.Ordinal);
    }
    [Fact]
    public void E09_E10_E11_E12_preferences_are_independent_and_essential_categories_are_not_editable()
    {
        Assert.True(NotificationPolicy.DefaultEnabled(NotificationCategory.AppointmentReminder, NotificationChannel.Email));
        Assert.True(NotificationPolicy.DefaultEnabled(NotificationCategory.AppointmentReminder, NotificationChannel.InApp));
        Assert.False(NotificationPolicy.DefaultEnabled(NotificationCategory.ClinicalAvailability, NotificationChannel.Email));
        Assert.False(NotificationPolicy.Editable(NotificationCategory.SecurityCritical));
        Assert.False(NotificationPolicy.Editable(NotificationCategory.BookingTransactional));
        var user = Guid.NewGuid();
        var email = new NotificationPreference(user, NotificationCategory.AppointmentReminder, NotificationChannel.Email, false, "en", Appointment);
        var inbox = new NotificationPreference(user, NotificationCategory.AppointmentReminder, NotificationChannel.InApp, true, "ar", Appointment);
        Assert.False(email.Enabled); Assert.True(inbox.Enabled);
        Assert.Throws<ArgumentException>(() => new NotificationPreference(user, NotificationCategory.SecurityCritical, NotificationChannel.Email, false, "ar", Appointment));
    }
    [Fact]
    public void O01_O11_terminal_intents_and_idempotent_reads_preserve_logical_identity()
    {
        var sourceId = Guid.NewGuid(); var key = Guid.NewGuid().ToString("N");
        var first = new NotificationEvent("Reservation.Created", "Reservation", sourceId, key, key, Guid.NewGuid(), null, null, null, Appointment);
        var replay = new NotificationEvent("Reservation.Created", "Reservation", sourceId, key, key, first.PatientId, null, null, null, Appointment);
        Assert.Equal(first.IdempotencyKey, replay.IdempotencyKey);
        var intent = new NotificationIntent(first, Appointment);
        intent.Finish(NotificationIntentStatus.Dispatched, Appointment);
        Assert.Throws<InvalidOperationException>(() => intent.Finish(NotificationIntentStatus.Scheduled, Appointment));
        var delivery = new NotificationDelivery(first, intent, NotificationChannel.InApp, "user", Guid.NewGuid(), first.PatientId, Appointment);
        var inbox = new UserNotification(first, delivery, Appointment);
        inbox.MarkRead(Appointment); inbox.MarkRead(Appointment.AddDays(1));
        Assert.Equal(Appointment, inbox.ReadAtUtc);
    }
    [Theory]
    [InlineData("Reservation.Expired", false)]
    [InlineData("Reservation.MarkedNoShow", false)]
    [InlineData("Patient.CheckedIn", false)]
    [InlineData("Doctor.NewBooking", false)]
    [InlineData("Doctor.BookingCancelled", false)]
    [InlineData("Doctor.Approved", false)]
    [InlineData("Prescription.Available", true)]
    [InlineData("Appointment.ReminderDue", true)]
    public void E02_E03_E05_E06_event_channel_matrix_is_explicit(string type, bool email)
        => Assert.Equal(email, NotificationPolicy.EmailAllowed(type));
    [Theory]
    [InlineData("Ticket.Called")]
    [InlineData("Ticket.Recalled")]
    [InlineData("Doctor.DailyScheduleDigest")]
    [InlineData("Doctor.NoShowDigest")]
    [InlineData("Prescription.Voided")]
    [InlineData("VisitSummary.Available")]
    public void E04_E08_unapproved_triggers_cannot_be_captured(string type)
    {
        Assert.False(NotificationPolicy.KnownEvent(type));
        Assert.Throws<ArgumentException>(() => new NotificationEvent(type, "Reservation", Guid.NewGuid(), "key", "key", null, null, null, null, Appointment));
    }
    [Fact]
    public void A18_settings_audit_preserves_old_version_and_increments_monotonically()
    {
        var settings = new PlatformNotificationSettings();
        var audit = settings.Update(true, 90, Guid.NewGuid(), "Approved test update", Appointment);
        Assert.Equal(120, audit.PreviousReminderLeadMinutes); Assert.Equal(90, audit.NewReminderLeadMinutes);
        Assert.Equal(1, audit.PreviousVersion); Assert.Equal(2, settings.Version);
        Assert.Throws<ArgumentException>(() => settings.Update(true, 0, Guid.NewGuid(), "Invalid", Appointment));
    }
    [Fact]
    public void R10_R11_logical_audiences_keep_distinct_inboxes_and_missing_owner_can_only_be_suppressed()
    {
        var source = new NotificationEvent("Appointment.ReminderDue", "Reservation", Guid.NewGuid(), "revision", "revision", Guid.NewGuid(), null, null, null, Appointment);
        var intent = new NotificationIntent(source, Appointment);
        var first = new NotificationDelivery(source, intent, NotificationChannel.InApp, "patient-one", Guid.NewGuid(), source.PatientId, Appointment);
        var second = new NotificationDelivery(source, intent, NotificationChannel.InApp, "patient-two", Guid.NewGuid(), source.PatientId, Appointment);
        Assert.NotEqual(first.LogicalDeliveryKey, second.LogicalDeliveryKey);
        Assert.NotEqual(new UserNotification(source, first, Appointment).ApplicationUserId, new UserNotification(source, second, Appointment).ApplicationUserId);
        var unlinked = new NotificationDelivery(source, intent, NotificationChannel.InApp, "global-patient", null, source.PatientId, Appointment);
        Assert.Throws<InvalidOperationException>(() => unlinked.Queue(Appointment));
        unlinked.Suppress("Recipient.NoAccount");
        Assert.Throws<InvalidOperationException>(() => unlinked.Queue(Appointment));
    }
}
