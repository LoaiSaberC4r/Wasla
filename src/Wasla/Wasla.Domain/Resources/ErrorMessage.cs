using System.Globalization;
using System.Resources;

namespace Wasla.Domain.Resources;

public static class ErrorMessage
{
    private static readonly ResourceManager Manager = new(
        "Wasla.Domain.Resources.ErrorMessage",
        typeof(ErrorMessage).Assembly);

    public static string ValidationErrorTitle => GetString(nameof(ValidationErrorTitle));
    public static string DomainErrorTitle => GetString(nameof(DomainErrorTitle));
    public static string ResourceNotFoundTitle => GetString(nameof(ResourceNotFoundTitle));
    public static string ConflictTitle => GetString(nameof(ConflictTitle));
    public static string UnauthorizedTitle => GetString(nameof(UnauthorizedTitle));
    public static string SecurityErrorTitle => GetString(nameof(SecurityErrorTitle));
    public static string TooManyRequestsTitle => GetString(nameof(TooManyRequestsTitle));
    public static string InfrastructureErrorTitle => GetString(nameof(InfrastructureErrorTitle));
    public static string UnexpectedErrorTitle => GetString(nameof(UnexpectedErrorTitle));
    public static string MethodNotAllowedTitle => GetString(nameof(MethodNotAllowedTitle));
    public static string UnsupportedMediaTypeTitle => GetString(nameof(UnsupportedMediaTypeTitle));
    public static string NotFound => GetString(nameof(NotFound));
    public static string AuthenticationRequired => GetString(nameof(AuthenticationRequired));
    public static string AccessForbidden => GetString(nameof(AccessForbidden));
    public static string InvalidCredentials => GetString(nameof(InvalidCredentials));
    public static string AccountInactive => GetString(nameof(AccountInactive));
    public static string ApplicationUserNotFound => GetString(nameof(ApplicationUserNotFound));
    public static string UserNameRequired => GetString(nameof(UserNameRequired));
    public static string UserNameTooLong => GetString(nameof(UserNameTooLong));
    public static string UserNameAlreadyExists => GetString(nameof(UserNameAlreadyExists));
    public static string EmailRequired => GetString(nameof(EmailRequired));
    public static string EmailInvalid => GetString(nameof(EmailInvalid));
    public static string EmailAlreadyExists => GetString(nameof(EmailAlreadyExists));
    public static string PhoneNumberRequired => GetString(nameof(PhoneNumberRequired));
    public static string PasswordRequired => GetString(nameof(PasswordRequired));
    public static string PasswordInvalid => GetString(nameof(PasswordInvalid));
    public static string PasswordConfirmationMismatch => GetString(nameof(PasswordConfirmationMismatch));
    public static string CurrentPasswordInvalid => GetString(nameof(CurrentPasswordInvalid));
    public static string PasswordMustBeDifferent => GetString(nameof(PasswordMustBeDifferent));
    public static string NameArRequired => GetString(nameof(NameArRequired));
    public static string DateOfBirthRequired => GetString(nameof(DateOfBirthRequired));
    public static string DateOfBirthFuture => GetString(nameof(DateOfBirthFuture));
    public static string GenderInvalid => GetString(nameof(GenderInvalid));
    public static string RequiredMediaMissing => GetString(nameof(RequiredMediaMissing));
    public static string DoctorNotFound => GetString(nameof(DoctorNotFound));
    public static string DoctorInvalidStatus => GetString(nameof(DoctorInvalidStatus));
    public static string DoctorNationalIdRequired => GetString(nameof(DoctorNationalIdRequired));
    public static string DoctorNationalIdAlreadyExists => GetString(nameof(DoctorNationalIdAlreadyExists));
    public static string DoctorMediaNotFound => GetString(nameof(DoctorMediaNotFound));
    public static string ReasonRequired => GetString(nameof(ReasonRequired));
    public static string InvalidRowVersion => GetString(nameof(InvalidRowVersion));
    public static string SuperAdminNotFound => GetString(nameof(SuperAdminNotFound));
    public static string RootSuperAdminRequired => GetString(nameof(RootSuperAdminRequired));
    public static string RootSuperAdminProtected => GetString(nameof(RootSuperAdminProtected));
    public static string RoleNotFound => GetString(nameof(RoleNotFound));
    public static string PermissionNotFound => GetString(nameof(PermissionNotFound));
    public static string RootPermissionNotAssignable => GetString(nameof(RootPermissionNotAssignable));
    public static string DuplicatePermission => GetString(nameof(DuplicatePermission));
    public static string OtpInvalid => GetString(nameof(OtpInvalid));
    public static string OtpExpired => GetString(nameof(OtpExpired));
    public static string TooManyAttempts => GetString(nameof(TooManyAttempts));
    public static string ResetTokenInvalid => GetString(nameof(ResetTokenInvalid));
    public static string ResetTokenExpired => GetString(nameof(ResetTokenExpired));
    public static string PasswordResetChallengeConsumed => GetString(nameof(PasswordResetChallengeConsumed));
    public static string PasswordResetConfigurationInvalid => GetString(nameof(PasswordResetConfigurationInvalid));
    public static string PasswordResetRequestAccepted => GetString(nameof(PasswordResetRequestAccepted));
    public static string MedicalSpecializationInvalid => GetString(nameof(MedicalSpecializationInvalid));
    public static string MedicalSpecializationNotFound => GetString(nameof(MedicalSpecializationNotFound));
    public static string MedicalSpecializationDuplicateNameAr => GetString(nameof(MedicalSpecializationDuplicateNameAr));
    public static string MedicalSpecializationDuplicateNameEn => GetString(nameof(MedicalSpecializationDuplicateNameEn));
    public static string MedicalSpecializationAlreadyActive => GetString(nameof(MedicalSpecializationAlreadyActive));
    public static string MedicalSpecializationAlreadyInactive => GetString(nameof(MedicalSpecializationAlreadyInactive));
    public static string MedicalSpecializationMustDeactivateBeforeDelete => GetString(nameof(MedicalSpecializationMustDeactivateBeforeDelete));
    public static string MedicalSpecializationDeleted => GetString(nameof(MedicalSpecializationDeleted));
    public static string MedicalSpecializationNotDeleted => GetString(nameof(MedicalSpecializationNotDeleted));
    public static string MedicalSpecializationConcurrencyConflict => GetString(nameof(MedicalSpecializationConcurrencyConflict));
    public static string LocationInvalid => GetString(nameof(LocationInvalid));
    public static string LocationGovernorateNotFound => GetString(nameof(LocationGovernorateNotFound));
    public static string LocationCityNotFound => GetString(nameof(LocationCityNotFound));
    public static string LocationAreaNotFound => GetString(nameof(LocationAreaNotFound));
    public static string LocationInvalidHierarchy => GetString(nameof(LocationInvalidHierarchy));
    public static string LocationInactive => GetString(nameof(LocationInactive));
    public static string DoctorSpecializationsOpenRequestAlreadyExists => GetString(nameof(DoctorSpecializationsOpenRequestAlreadyExists));
    public static string DoctorSpecializationsRequestNotFound => GetString(nameof(DoctorSpecializationsRequestNotFound));
    public static string DoctorSpecializationsNoSpecializations => GetString(nameof(DoctorSpecializationsNoSpecializations));
    public static string DoctorSpecializationsPrimaryRequired => GetString(nameof(DoctorSpecializationsPrimaryRequired));
    public static string DoctorSpecializationsMultiplePrimary => GetString(nameof(DoctorSpecializationsMultiplePrimary));
    public static string DoctorSpecializationsDuplicateSpecialization => GetString(nameof(DoctorSpecializationsDuplicateSpecialization));
    public static string DoctorSpecializationsSpecializationNotAvailable => GetString(nameof(DoctorSpecializationsSpecializationNotAvailable));
    public static string DoctorSpecializationsSpecializationNoLongerAvailable => GetString(nameof(DoctorSpecializationsSpecializationNoLongerAvailable));
    public static string DoctorSpecializationsInvalidRequestStatus => GetString(nameof(DoctorSpecializationsInvalidRequestStatus));
    public static string DoctorSpecializationsConcurrencyConflict => GetString(nameof(DoctorSpecializationsConcurrencyConflict));
    public static string DoctorPracticeLocationInvalid => GetString(nameof(DoctorPracticeLocationInvalid));
    public static string DoctorPracticeLocationNotFound => GetString(nameof(DoctorPracticeLocationNotFound));
    public static string DoctorPracticeLocationInvalidLatitude => GetString(nameof(DoctorPracticeLocationInvalidLatitude));
    public static string DoctorPracticeLocationInvalidLongitude => GetString(nameof(DoctorPracticeLocationInvalidLongitude));
    public static string DoctorPracticeLocationRowVersionRequired => GetString(nameof(DoctorPracticeLocationRowVersionRequired));
    public static string DoctorPracticeLocationRowVersionMustBeNull => GetString(nameof(DoctorPracticeLocationRowVersionMustBeNull));
    public static string DoctorPracticeLocationConcurrencyConflict => GetString(nameof(DoctorPracticeLocationConcurrencyConflict));
    public static string DoctorOnboardingUnavailable => GetString(nameof(DoctorOnboardingUnavailable));
    public static string ValidationNotNull => GetString(nameof(ValidationNotNull));
    public static string ValidationNotEmpty => GetString(nameof(ValidationNotEmpty));
    public static string ValidationEmail => GetString(nameof(ValidationEmail));
    public static string ValidationMaximumLength => GetString(nameof(ValidationMaximumLength));
    public static string ValidationMinimumLength => GetString(nameof(ValidationMinimumLength));
    public static string ValidationExactLength => GetString(nameof(ValidationExactLength));
    public static string ValidationLength => GetString(nameof(ValidationLength));
    public static string ValidationGreaterThan => GetString(nameof(ValidationGreaterThan));
    public static string ValidationGreaterThanOrEqual => GetString(nameof(ValidationGreaterThanOrEqual));
    public static string ValidationInclusiveBetween => GetString(nameof(ValidationInclusiveBetween));
    public static string ValidationEqual => GetString(nameof(ValidationEqual));
    public static string ValidationRegularExpression => GetString(nameof(ValidationRegularExpression));
    public static string ValidationPredicate => GetString(nameof(ValidationPredicate));
    public static string ValidationEnum => GetString(nameof(ValidationEnum));
    public static string ValidationNull => GetString(nameof(ValidationNull));
    public static string ValidationEmpty => GetString(nameof(ValidationEmpty));

    public static string GetString(string name, CultureInfo? culture = null)
        => Manager.GetString(name, culture ?? CultureInfo.CurrentUICulture)
           ?? throw new MissingManifestResourceException(
               $"The required error resource '{name}' is missing for culture '{(culture ?? CultureInfo.CurrentUICulture).Name}'.");
}
