using BuildingBlock.Application.Email;

namespace Wasla.Application.Email;

internal sealed class EmailNotificationFactory(IEmailTemplateRenderer renderer) : IEmailNotificationFactory
{
    public async Task<EmailNotificationContent> CreateAsync(
        EmailTemplateModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        var language = model.Language switch
        {
            EmailLanguageMode.Arabic => "ar",
            EmailLanguageMode.English => "en",
            EmailLanguageMode.Bilingual => "ar-en",
            _ => throw new ArgumentException("Unsupported email language.")
        };

        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        var template = model switch
        {
            PasswordResetOtpEmail m => Otp(EmailTemplateKeys.PasswordResetOtp, m.OtpCode, m.ExpirationMinutes),
            PatientEmailVerificationEmail m => Otp(EmailTemplateKeys.PatientEmailVerification, m.OtpCode, m.ExpirationMinutes),
            PatientAccountActivationEmail m => Activation(m),
            PatientIdentityClaimUpdateEmail => EmailTemplateKeys.PatientIdentityClaimUpdate,
            DoctorRegistrationReceivedEmail m => Doctor(EmailTemplateKeys.DoctorRegistrationReceived, m),
            DoctorAccountApprovedEmail m => Doctor(EmailTemplateKeys.DoctorAccountApproved, m),
            DoctorRegistrationRejectedEmail m => Doctor(EmailTemplateKeys.DoctorRegistrationRejected, m, Required(m.Reason)),
            DoctorAccountSuspendedEmail m => Doctor(EmailTemplateKeys.DoctorAccountSuspended, m, Required(m.Reason)),
            DoctorAccountReactivatedEmail m => Doctor(EmailTemplateKeys.DoctorAccountReactivated, m),
            DoctorSpecializationChangesRequestedEmail m => Review(EmailTemplateKeys.DoctorSpecializationChangesRequested,
                m.Doctor, m.Revision, Required(m.ReviewMessage)),
            FamilyRelationshipApprovedEmail m => Review(EmailTemplateKeys.FamilyRelationshipApproved, m.Submitter, m.Revision),
            FamilyRelationshipRejectedEmail m => Review(EmailTemplateKeys.FamilyRelationshipRejected, m.Submitter, m.Revision),
            FamilyRelationshipChangesRequestedEmail m => Review(EmailTemplateKeys.FamilyRelationshipChangesRequested, m.Submitter, m.Revision),
            ReceptionAccountCreatedEmail m => Reception(m),
            ReservationConfirmedEmail m => Reservation(EmailTemplateKeys.ReservationConfirmed, m),
            ReservationRescheduledEmail m => Reservation(EmailTemplateKeys.ReservationRescheduled, m),
            ReservationCancelledEmail m => Reservation(EmailTemplateKeys.ReservationCancelled, m),
            AppointmentReminderEmail m => Reservation(EmailTemplateKeys.AppointmentReminder, m),
            ReservationRestoredEmail m => Reservation(EmailTemplateKeys.ReservationRestored, m),
            MedicalUpdateAvailableEmail => EmailTemplateKeys.MedicalUpdateAvailable,
            TrialStartedEmail m => Subscription(EmailTemplateKeys.TrialStarted, m, m.StartsAtUtc, m.PracticeLimit, m.ReceptionLimit),
            TrialReminderEmail m => Subscription(EmailTemplateKeys.TrialReminder, m),
            TrialExpiredEmail m => Subscription(EmailTemplateKeys.TrialExpired, m),
            TrialExtendedEmail m => Subscription(EmailTemplateKeys.TrialExtended, m),
            PaidSubscriptionActivatedEmail m => Subscription(EmailTemplateKeys.PaidSubscriptionActivated, m,
                m.StartsAtUtc, m.PracticeLimit, m.ReceptionLimit, m.LinkedPractices),
            PaidSubscriptionReminderEmail m => Subscription(EmailTemplateKeys.PaidSubscriptionReminder, m,
                practices: m.LinkedPractices, price: m.RenewalPrice, currency: m.Currency),
            SubscriptionExpiredEmail m => Subscription(EmailTemplateKeys.SubscriptionExpired, m),
            _ => throw new ArgumentException("Unknown email template model.")
        };

        var result = await renderer.RenderAsync(new(template, fields, language), cancellationToken);
        if (string.IsNullOrWhiteSpace(result.Subject) || result.Subject.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(result.HtmlBody) || string.IsNullOrWhiteSpace(result.TextBody))
            throw new InvalidOperationException("Email rendering returned incomplete or invalid content.");

        return new(result.Subject, result.HtmlBody, result.TextBody);

        string Otp(string key, string code, int minutes)
        {
            if (string.IsNullOrEmpty(code) || code.Any(digit => !char.IsAsciiDigit(digit)))
                throw new ArgumentException("Invalid verification code.");
            fields["OtpCode"] = code;
            fields["Expiration"] = Positive(minutes);
            return key;
        }

        string Activation(PatientAccountActivationEmail input)
        {
            fields["ActivationToken"] = Required(input.ActivationToken);
            fields["Expiration"] = Positive(input.ExpirationHours);
            return EmailTemplateKeys.PatientAccountActivation;
        }

        string Doctor(string key, DoctorLifecycleEmail input, string? reason = null)
        {
            fields["Name"] = ValidName(input.Doctor);
            fields["OccurredAtUtc"] = input.OccurredAtUtc == default
                ? throw new ArgumentException("Missing doctor lifecycle timestamp.")
                : input.OccurredAtUtc;
            fields["ReviewMessage"] = reason;
            return key;
        }

        string Review(string key, EmailName name, int revision, string? reviewMessage = null)
        {
            fields["Name"] = ValidName(name);
            fields["Revision"] = Positive(revision);
            fields["ReviewMessage"] = reviewMessage;
            return key;
        }

        string Reception(ReceptionAccountCreatedEmail input)
        {
            fields["Name"] = ValidName(input.Receptionist);
            fields["Username"] = Required(input.Username);
            return EmailTemplateKeys.ReceptionAccountCreated;
        }

        string Reservation(string key, ReservationEmail input)
        {
            fields["Reference"] = Required(new string(Required(input.Reference).Where(c => !char.IsControl(c)).Take(32).ToArray()));
            if (key != EmailTemplateKeys.ReservationCancelled && input.ScheduledLocalDateTime is null or { Ticks: 0 })
                throw new ArgumentException("Missing local appointment time.");
            fields["ScheduledLocalDateTime"] = input.ScheduledLocalDateTime;
            return key;
        }

        string Subscription(string key, SubscriptionEmail input, DateTime? startsAtUtc = null,
            int? practiceLimit = null, int? receptionLimit = null, IReadOnlyList<EmailName>? practices = null,
            decimal? price = null, string? currency = null)
        {
            fields["Name"] = ValidName(input.Doctor);
            fields["Plan"] = Required(input.Plan);
            fields["EndsAtUtc"] = input.EndsAtUtc;
            fields["StartsAtUtc"] = startsAtUtc;
            fields["PracticeLimit"] = practiceLimit;
            fields["ReceptionLimit"] = receptionLimit;
            fields["LinkedPractices"] = practices?.Select(ValidName).ToArray();
            fields["RenewalPrice"] = price;
            fields["Currency"] = price.HasValue ? Required(currency) : null;
            return key;
        }
    }

    private static string Required(string? value) => !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException("Missing required email field.");

    private static int Positive(int value) => value > 0
        ? value
        : throw new ArgumentException("Invalid numeric email field.");

    private static EmailName ValidName(EmailName? name)
    {
        Required(name?.Arabic);
        return name!;
    }
}
