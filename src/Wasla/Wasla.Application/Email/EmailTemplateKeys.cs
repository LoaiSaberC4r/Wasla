namespace Wasla.Application.Email;

public static class EmailTemplateKeys
{
    public const string PasswordResetOtp = "Authentication.PasswordResetOtp";
    public const string PatientEmailVerification = "Patient.EmailVerificationOtp";
    public const string PatientAccountActivation = "Patient.AccountActivation";
    public const string DoctorRegistrationReceived = "Doctor.RegistrationReceived";
    public const string DoctorAccountApproved = "Doctor.AccountApproved";
    public const string DoctorRegistrationRejected = "Doctor.RegistrationRejected";
    public const string DoctorAccountSuspended = "Doctor.AccountSuspended";
    public const string DoctorAccountReactivated = "Doctor.AccountReactivated";
    public const string DoctorSpecializationChangesRequested = "Doctor.SpecializationChangesRequested";
    public const string PatientIdentityClaimUpdate = "Patient.IdentityClaimUpdate";
    public const string FamilyRelationshipApproved = "Family.RelationshipApproved";
    public const string FamilyRelationshipRejected = "Family.RelationshipRejected";
    public const string FamilyRelationshipChangesRequested = "Family.RelationshipChangesRequested";
    public const string ReceptionAccountCreated = "Reception.AccountCreated";
    public const string ReservationConfirmed = "Reservation.Confirmed";
    public const string ReservationRescheduled = "Reservation.Rescheduled";
    public const string ReservationCancelled = "Reservation.Cancelled";
    public const string AppointmentReminder = "Reservation.AppointmentReminder";
    public const string ReservationRestored = "Reservation.RestoredFromNoShow";
    public const string MedicalUpdateAvailable = "Clinical.MedicalUpdateAvailable";
    public const string TrialStarted = "Subscription.TrialStarted";
    public const string TrialReminder = "Subscription.TrialReminder";
    public const string TrialExpired = "Subscription.TrialExpired";
    public const string TrialExtended = "Subscription.TrialExtended";
    public const string PaidSubscriptionActivated = "Subscription.PaidActivated";
    public const string PaidSubscriptionReminder = "Subscription.PaidReminder";
    public const string SubscriptionExpired = "Subscription.Expired";
}
