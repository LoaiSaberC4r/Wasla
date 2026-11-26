using BuildingBlock.Application.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Email;
using Wasla.Infrastructure.Email;

namespace Wasla.EmailPreview;

internal sealed record SampleEmail(string Slug, string Title, EmailTemplateModel Model, string Example);

internal static class SampleEmails
{
    internal const string AssetBaseUrl = "https://app.wasla.test/email-assets/";
    private static readonly DateTime Today = new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);

    internal static ServiceProvider BuildServices(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new PreviewClock());
        services.AddWaslaApplication();
        services.AddWaslaEmailRendering(configuration);
        return services.BuildServiceProvider();
    }

    internal static Dictionary<string, string?> Branding() => new(StringComparer.Ordinal)
    {
        ["EmailBranding:AllowedOrigins:0"] = "https://app.wasla.test",
        ["EmailBranding:AssetBaseUrl"] = AssetBaseUrl,
        ["EmailBranding:TimeZoneId"] = "Africa/Cairo",
        ["EmailBranding:WebsiteUrl"] = "https://app.wasla.test/",
        ["EmailBranding:SupportUrl"] = "https://app.wasla.test/help",
        ["EmailBranding:PrivacyPolicyUrl"] = "https://app.wasla.test/privacy",
        ["EmailBranding:LoginUrl"] = "https://app.wasla.test/sign-in",
        ["EmailBranding:PatientActivationUrl"] = "https://app.wasla.test/activate",
        ["EmailBranding:PatientActivationTokenQueryParameter"] = "token",
        ["EmailBranding:PatientIdentityClaimUrl"] = "https://app.wasla.test/identity-claim",
        ["EmailBranding:DoctorSpecializationRequestUrl"] = "https://app.wasla.test/doctor/specializations",
        ["EmailBranding:FamilyUrl"] = "https://app.wasla.test/family",
        ["EmailBranding:FamilyRequestUrl"] = "https://app.wasla.test/family/requests",
        ["EmailBranding:ReservationUrl"] = "https://app.wasla.test/reservations",
        ["EmailBranding:SubscriptionUrl"] = "https://app.wasla.test/subscription",
        ["EmailBranding:SubscriptionPlansUrl"] = "https://app.wasla.test/plans"
    };

    internal static IReadOnlyList<SampleEmail> Catalog { get; } = CreateCatalog();

    private static SampleEmail[] CreateCatalog()
    {
        var doctor = new EmailName("ليلى نور", "Layla Noor");
        var patient = new EmailName("سلمى عمر", "Salma Omar");
        var receptionist = new EmailName("ملك علي", "Malak Ali");
        EmailName[] practices = [new("عيادة النور", "Noor Practice"), new("عيادة الأمل", "Amal Practice")];
        const string token = "SYNTHETIC-ACTIVATION-TOKEN";
        const string reference = "WSL-278519";
        var plan = new SubscriptionPlanEmailName("PROFESSIONAL", "الباقة الاحترافية", "Professional Plan");
        var trial = new SubscriptionPlanEmailName("DEMO-TRIAL");
        var appointment = new DateTime(2026, 11, 30, 18, 30, 0, DateTimeKind.Unspecified);
        var end = Today.AddDays(7);
        return
        [
            new("01-password-reset-otp", "Password Reset OTP", new PasswordResetOtpEmail("042916", 5), "042916"),
            new("02-patient-email-verification", "Patient Email Verification", new PatientEmailVerificationEmail("071852", 10), "071852"),
            new("03-patient-account-activation", "Patient Account Activation", new PatientAccountActivationEmail(token, 48), token),
            new("04-doctor-registration-received", "Doctor Registration Received", new DoctorRegistrationReceivedEmail(doctor, Today), "11:00"),
            new("05-doctor-account-approved", "Doctor Account Approved", new DoctorAccountApprovedEmail(doctor, Today), "11:00"),
            new("06-doctor-registration-rejected", "Doctor Registration Rejected", new DoctorRegistrationRejectedEmail(doctor, Today, "Registration document is out of date."), "Registration document is out of date."),
            new("07-doctor-account-suspended", "Doctor Account Suspended", new DoctorAccountSuspendedEmail(doctor, Today, "Professional credentials require review."), "Professional credentials require review."),
            new("08-doctor-account-reactivated", "Doctor Account Reactivated", new DoctorAccountReactivatedEmail(doctor, Today), "11:00"),
            new("09-doctor-specialization-changes-requested", "Doctor Specialization Changes Requested", new DoctorSpecializationChangesRequestedEmail(doctor, "Please update the specialization information.", 3), "Please update the specialization information."),
            new("10-patient-identity-claim-update", "Patient Identity Claim Update", new PatientIdentityClaimUpdateEmail(), "https://app.wasla.test/identity-claim"),
            new("11-family-relationship-approved", "Family Relationship Approved", new FamilyRelationshipApprovedEmail(patient, 2), "2"),
            new("12-family-relationship-rejected", "Family Relationship Rejected", new FamilyRelationshipRejectedEmail(patient, 2), "2"),
            new("13-family-relationship-changes-requested", "Family Relationship Changes Requested", new FamilyRelationshipChangesRequestedEmail(patient, 3), "3"),
            new("14-reception-account-created", "Reception Account Created", new ReceptionAccountCreatedEmail(receptionist, "reception.demo"), "reception.demo"),
            new("15-reservation-confirmed", "Reservation Confirmed", new ReservationConfirmedEmail(reference, appointment), reference),
            new("16-reservation-rescheduled", "Reservation Rescheduled", new ReservationRescheduledEmail(reference, appointment.AddHours(1)), reference),
            new("17-reservation-cancelled", "Reservation Cancelled", new ReservationCancelledEmail(reference, appointment), reference),
            new("18-appointment-reminder", "Appointment Reminder", new AppointmentReminderEmail(reference, appointment), reference),
            new("19-reservation-restored", "Reservation Restored", new ReservationRestoredEmail(reference, appointment), reference),
            new("20-medical-update-available", "Medical Update Available", new MedicalUpdateAvailableEmail(), "https://app.wasla.test/sign-in"),
            new("21-trial-started", "Trial Started", new TrialStartedEmail(doctor, trial, end, Today, 2, 4), trial.PlanCode),
            new("22-trial-reminder", "Trial Reminder", new TrialReminderEmail(doctor, trial, end), trial.PlanCode),
            new("23-trial-expired", "Trial Expired", new TrialExpiredEmail(doctor, trial, Today), trial.PlanCode),
            new("24-trial-extended", "Trial Extended", new TrialExtendedEmail(doctor, trial, end.AddDays(14)), trial.PlanCode),
            new("25-paid-subscription-activated", "Paid Subscription Activated", new PaidSubscriptionActivatedEmail(doctor, plan, end, Today, 2, 4, practices), "11:00"),
            new("26-paid-subscription-reminder", "Paid Subscription Reminder", new PaidSubscriptionReminderEmail(doctor, plan, end, practices, 1200m, "EGP"), "1200.00"),
            new("27-subscription-expired", "Subscription Expired", new SubscriptionExpiredEmail(doctor, trial, Today), trial.PlanCode)
        ];
    }

    private sealed class PreviewClock : IDateTimeProvider
    {
        public DateTime UtcNow => Today;
    }
}
