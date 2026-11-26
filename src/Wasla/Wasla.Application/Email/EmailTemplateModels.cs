namespace Wasla.Application.Email;

public enum EmailLanguageMode
{
    Arabic = 1,
    English = 2,
    Bilingual = 3
}

public sealed record EmailName(string Arabic, string? English = null);

public abstract record EmailTemplateModel
{
    public EmailLanguageMode Language { get; init; } = EmailLanguageMode.Bilingual;
}

public sealed record PasswordResetOtpEmail(string OtpCode, int ExpirationMinutes) : EmailTemplateModel;
public sealed record PatientEmailVerificationEmail(string OtpCode, int ExpirationMinutes) : EmailTemplateModel;
public sealed record PatientAccountActivationEmail(string ActivationToken, int ExpirationHours) : EmailTemplateModel;
public sealed record PatientIdentityClaimUpdateEmail : EmailTemplateModel;

public abstract record DoctorLifecycleEmail(EmailName Doctor, DateTime OccurredAtUtc) : EmailTemplateModel;
public sealed record DoctorRegistrationReceivedEmail(EmailName Doctor, DateTime OccurredAtUtc) : DoctorLifecycleEmail(Doctor, OccurredAtUtc);
public sealed record DoctorAccountApprovedEmail(EmailName Doctor, DateTime OccurredAtUtc) : DoctorLifecycleEmail(Doctor, OccurredAtUtc);
public sealed record DoctorRegistrationRejectedEmail(EmailName Doctor, DateTime OccurredAtUtc, string Reason) : DoctorLifecycleEmail(Doctor, OccurredAtUtc);
public sealed record DoctorAccountSuspendedEmail(EmailName Doctor, DateTime OccurredAtUtc, string Reason) : DoctorLifecycleEmail(Doctor, OccurredAtUtc);
public sealed record DoctorAccountReactivatedEmail(EmailName Doctor, DateTime OccurredAtUtc) : DoctorLifecycleEmail(Doctor, OccurredAtUtc);
public sealed record DoctorSpecializationChangesRequestedEmail(EmailName Doctor, string ReviewMessage, int Revision) : EmailTemplateModel;

public abstract record FamilyRelationshipEmail(EmailName Submitter, int Revision) : EmailTemplateModel;
public sealed record FamilyRelationshipApprovedEmail(EmailName Submitter, int Revision) : FamilyRelationshipEmail(Submitter, Revision);
public sealed record FamilyRelationshipRejectedEmail(EmailName Submitter, int Revision) : FamilyRelationshipEmail(Submitter, Revision);
public sealed record FamilyRelationshipChangesRequestedEmail(EmailName Submitter, int Revision) : FamilyRelationshipEmail(Submitter, Revision);
public sealed record ReceptionAccountCreatedEmail(EmailName Receptionist, string Username) : EmailTemplateModel;

public abstract record ReservationEmail(string Reference, DateTime? ScheduledLocalDateTime) : EmailTemplateModel;
public sealed record ReservationConfirmedEmail(string Reference, DateTime Appointment) : ReservationEmail(Reference, Appointment);
public sealed record ReservationRescheduledEmail(string Reference, DateTime Appointment) : ReservationEmail(Reference, Appointment);
public sealed record ReservationCancelledEmail(string Reference, DateTime? Appointment) : ReservationEmail(Reference, Appointment);
public sealed record AppointmentReminderEmail(string Reference, DateTime Appointment) : ReservationEmail(Reference, Appointment);
public sealed record ReservationRestoredEmail(string Reference, DateTime Appointment) : ReservationEmail(Reference, Appointment);

public sealed record MedicalUpdateAvailableEmail : EmailTemplateModel;

public abstract record SubscriptionEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc) : EmailTemplateModel;
public sealed record TrialStartedEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc,
    DateTime? StartsAtUtc, int PracticeLimit, int ReceptionLimit) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record TrialReminderEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record TrialExpiredEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record TrialExtendedEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record PaidSubscriptionActivatedEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc,
    DateTime? StartsAtUtc, int PracticeLimit, int ReceptionLimit, IReadOnlyList<EmailName> LinkedPractices) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record PaidSubscriptionReminderEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc,
    IReadOnlyList<EmailName> LinkedPractices, decimal? RenewalPrice, string? Currency) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
public sealed record SubscriptionExpiredEmail(EmailName Doctor, string Plan, DateTime? EndsAtUtc) : SubscriptionEmail(Doctor, Plan, EndsAtUtc);
