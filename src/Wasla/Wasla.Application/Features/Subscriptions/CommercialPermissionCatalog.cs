using Wasla.Domain.Security;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public enum SubscriptionPermissionCategory
{
    CommercialFeature, UniversalPatient, SuperAdminGovernance, AuthenticationOnboardingAccount,
    PrivacyConsentRelationship, InternalSystem
}
public sealed record SubscriptionPermissionClassification(SubscriptionPermissionCategory Category,
    string? FeatureCode = null, FeatureAccessLevel? RequiredAccess = null);

// Explicit review inventory: adding PermissionNames.All requires a deliberate classification.
// Unknown names are absent and fail closed, including names within an existing permission prefix.
public static class CommercialPermissionCatalog
{
    public static IReadOnlyDictionary<string, SubscriptionPermissionClassification> All { get; } = Build();
    private static Dictionary<string, SubscriptionPermissionClassification> Build()
    {
        var result = new Dictionary<string, SubscriptionPermissionClassification>(StringComparer.Ordinal);
        Add(result, new(SubscriptionPermissionCategory.SuperAdminGovernance),
        [
            PermissionNames.DoctorsViewAll,
            PermissionNames.DoctorsViewDetails,
            PermissionNames.DoctorsApprove,
            PermissionNames.DoctorsReject,
            PermissionNames.DoctorsSuspend,
            PermissionNames.DoctorsReactivate,
            PermissionNames.RolesView,
            PermissionNames.PermissionsView,
            PermissionNames.RolePermissionsManage,
            PermissionNames.SuperAdminsViewAll,
            PermissionNames.SuperAdminsViewDetails,
            PermissionNames.SuperAdminsCreate,
            PermissionNames.SuperAdminsUpdate,
            PermissionNames.SuperAdminsActivate,
            PermissionNames.SuperAdminsDeactivate,
            PermissionNames.SuperAdminsDelete,
            PermissionNames.SuperAdminsRestore,
            PermissionNames.SpecializationsView,
            PermissionNames.SpecializationsCreate,
            PermissionNames.SpecializationsUpdate,
            PermissionNames.SpecializationsActivate,
            PermissionNames.SpecializationsDeactivate,
            PermissionNames.SpecializationsDelete,
            PermissionNames.SpecializationsRestore,
            PermissionNames.DoctorSpecializationRequestsViewAll,
            PermissionNames.DoctorSpecializationRequestsViewDetails,
            PermissionNames.DoctorSpecializationRequestsAdjust,
            PermissionNames.DoctorSpecializationRequestsApprove,
            PermissionNames.DoctorSpecializationRequestsReject,
            PermissionNames.DoctorSpecializationRequestsRequestModification,
            PermissionNames.ReservationsViewAdministrative,
            PermissionNames.PlatformRevenueViewAggregates,
            PermissionNames.DrugCatalogView,
            PermissionNames.DrugCatalogCreate,
            PermissionNames.DrugCatalogUpdate,
            PermissionNames.DrugCatalogActivate,
            PermissionNames.DrugCatalogDeactivate,
            PermissionNames.DrugCatalogMerge,
            PermissionNames.DrugCatalogImport,
            PermissionNames.DrugCatalogImportHistory,
            PermissionNames.DrugCatalogRequestsView,
            PermissionNames.DrugCatalogRequestsReview,
            PermissionNames.DrugCatalogManagersViewAll,
            PermissionNames.DrugCatalogManagersViewDetails,
            PermissionNames.DrugCatalogManagersCreate,
            PermissionNames.DrugCatalogManagersUpdate,
            PermissionNames.DrugCatalogManagersActivate,
            PermissionNames.DrugCatalogManagersDeactivate,
            PermissionNames.LabCatalogView,
            PermissionNames.LabCatalogCreate,
            PermissionNames.LabCatalogUpdate,
            PermissionNames.LabCatalogActivate,
            PermissionNames.LabCatalogDeactivate,
            PermissionNames.LabCatalogMerge,
            PermissionNames.LabCatalogImport,
            PermissionNames.LabCatalogImportHistory,
            PermissionNames.LabCatalogRequestsView,
            PermissionNames.LabCatalogRequestsReview,
            PermissionNames.RadiologyCatalogView,
            PermissionNames.RadiologyCatalogCreate,
            PermissionNames.RadiologyCatalogUpdate,
            PermissionNames.RadiologyCatalogActivate,
            PermissionNames.RadiologyCatalogDeactivate,
            PermissionNames.RadiologyCatalogMerge,
            PermissionNames.RadiologyCatalogImport,
            PermissionNames.RadiologyCatalogImportHistory,
            PermissionNames.RadiologyCatalogRequestsView,
            PermissionNames.RadiologyCatalogRequestsReview,
            PermissionNames.MedicalCatalogManagersViewAll,
            PermissionNames.MedicalCatalogManagersViewDetails,
            PermissionNames.MedicalCatalogManagersCreate,
            PermissionNames.MedicalCatalogManagersUpdate,
            PermissionNames.MedicalCatalogManagersActivate,
            PermissionNames.MedicalCatalogManagersDeactivate,
            Phase18PermissionNames.PlatformDashboard,
            Phase18PermissionNames.PlatformReports,
            Phase18PermissionNames.PlatformExport,
            Phase18PermissionNames.InvestigationsView,
            Phase18PermissionNames.InvestigationsCreate,
            Phase18PermissionNames.InvestigationsAssign,
            Phase18PermissionNames.InvestigationsStartReview,
            Phase18PermissionNames.InvestigationsResolve,
            Phase18PermissionNames.InvestigationsClose,
            Phase18PermissionNames.InvestigationsReopen,
            Phase18PermissionNames.JobsView,
            Phase18PermissionNames.DataQualityView,
            Phase18PermissionNames.SecurityMetadataView,
            Phase18PartTwoPermissionNames.TicketsPlatform,
            Phase18PartTwoPermissionNames.ReservationsPlatform,
            Phase18PartTwoPermissionNames.Health,
            Phase18PartTwoPermissionNames.Failures,
            Phase18PartTwoPermissionNames.Runs,
            Phase18PartTwoPermissionNames.Pause,
            Phase18PartTwoPermissionNames.Resume,
            Phase18PartTwoPermissionNames.AuditPlatform,
            PermissionNames.PatientClaimsViewAll,
            PermissionNames.PatientClaimsViewDetails,
            PermissionNames.PatientClaimsViewEvidence,
            PermissionNames.PatientClaimsApprove,
            PermissionNames.PatientClaimsReject,
            PermissionNames.PatientClaimsRequestModification,
            PermissionNames.PatientDuplicatesView,
            PermissionNames.PatientDuplicatesResolve,
            PermissionNames.NotificationsSettingsView,
            PermissionNames.NotificationsSettingsManage,
            PermissionNames.NotificationsDeliveriesView,
            PermissionNames.NotificationsDeliveriesRetry,
            SubscriptionPermissionNames.ViewAll,
            SubscriptionPermissionNames.ViewDetails,
            SubscriptionPermissionNames.Activate,
            SubscriptionPermissionNames.ExtendTrial,
            SubscriptionPermissionNames.Renew,
            SubscriptionPermissionNames.ReviewChangeRequests,
            SubscriptionPermissionNames.EnrollLegacyTransition,
        ]);
        Add(result, new(SubscriptionPermissionCategory.AuthenticationOnboardingAccount),
        [
            PermissionNames.DoctorOnboardingViewOwn,
            PermissionNames.PatientProfileViewOwn,
            PermissionNames.DoctorSpecializationsViewOwn,
            PermissionNames.DoctorSpecializationsSubmitOwn,
            PermissionNames.DoctorSpecializationsResubmitOwn,
            PermissionNames.DoctorPracticeLocationViewOwn,
            PermissionNames.DoctorPracticeLocationManageOwn,
            PermissionNames.PatientProfileUpdateOwn,
            PermissionNames.PatientContactsViewOwn,
            PermissionNames.PatientContactsManageOwn,
            PermissionNames.DoctorProfileViewOwn,
            PermissionNames.DoctorProfileUpdateOwn,
            PermissionNames.NotificationsViewOwn,
            PermissionNames.NotificationsMarkReadOwn,
            PermissionNames.NotificationsPreferencesViewOwn,
            PermissionNames.NotificationsPreferencesManageOwn,
            SubscriptionPermissionNames.ViewOwn,
            SubscriptionPermissionNames.RequestChangeOwn,
            SubscriptionPermissionNames.CancelAtPeriodEndOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PATIENT_MANAGEMENT", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.PatientsSearchBasic,
            PermissionNames.DoctorPatientsViewOwn,
            PermissionNames.DoctorPatientsViewHistoricalOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PATIENT_MANAGEMENT", FeatureAccessLevel.Full),
        [
            PermissionNames.PatientsRegister,
            PermissionNames.PatientsRegisterFull,
            PermissionNames.PatientsRegistrationMatch,
        ]);
        Add(result, new(SubscriptionPermissionCategory.PrivacyConsentRelationship),
        [
            PermissionNames.FamiliesViewOwn,
            PermissionNames.FamiliesManageOwn,
            PermissionNames.FamilyRelationshipRequestsCreate,
            PermissionNames.FamilyRelationshipRequestsViewOwn,
            PermissionNames.FamilyRelationshipRequestsResubmitOwn,
            PermissionNames.FamilyRelationshipRequestsCreateAssisted,
            PermissionNames.FamilyRelationshipRequestsViewAssisted,
            PermissionNames.FamilyRelationshipRequestsResubmitAssisted,
            PermissionNames.FamilyRelationshipRequestsViewAll,
            PermissionNames.FamilyRelationshipRequestsViewDetails,
            PermissionNames.FamilyRelationshipRequestsApprove,
            PermissionNames.FamilyRelationshipRequestsReject,
            PermissionNames.FamilyRelationshipRequestsRequestModification,
            PermissionNames.MedicalRecordsViewOwn,
            PermissionNames.MedicalRecordsViewDependent,
            PermissionNames.MedicalRecordsViewOwnAccessLog,
            PermissionNames.MedicalEncounterVisibilityUpdateOwn,
            PermissionNames.MedicalEncounterVisibilityViewHistoryOwn,
            PermissionNames.HiddenEncounterAccessRequestsCreate,
            PermissionNames.HiddenEncounterAccessRequestsViewSent,
            PermissionNames.HiddenEncounterAccessRequestsViewIncoming,
            PermissionNames.HiddenEncounterAccessRequestsDecideOwn,
            PermissionNames.HiddenEncounterAccessRequestsRevokeOwn,
            PermissionNames.HiddenEncounterAccessRequestsViewHistoryOwn,
            PermissionNames.FamilyMedicalAccessRequestsCreateOwn,
            PermissionNames.FamilyMedicalAccessRequestsViewOwn,
            PermissionNames.FamilyMedicalAccessRequestsWithdrawOwn,
            PermissionNames.FamilyMedicalAccessRequestsResubmitOwn,
            PermissionNames.FamilyMedicalAccessRequestsViewOwnDocuments,
            PermissionNames.FamilyMedicalAccessRequestsViewAll,
            PermissionNames.FamilyMedicalAccessRequestsViewAdminDetails,
            PermissionNames.FamilyMedicalAccessRequestsDecide,
            PermissionNames.FamilyMedicalAccessRequestsRequestModification,
            PermissionNames.FamilyMedicalAccessRequestsViewAdminDocuments,
            PermissionNames.FamilyMedicalAccessEligibilityReview,
            PermissionNames.FamilyMedicalAccessEligibilityDecide,
            PermissionNames.FamilyMedicalAccessEligibilityRevoke,
            Phase18PartTwoPermissionNames.SensitivePractice,
            Phase18PartTwoPermissionNames.SensitiveRestricted,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRACTICE_MANAGEMENT", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorPracticesViewOwn,
            PermissionNames.DoctorPracticeConfigurationViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRACTICE_MANAGEMENT", FeatureAccessLevel.Full),
        [
            PermissionNames.DoctorPracticesManageOwn,
            PermissionNames.DoctorPracticesActivateOwn,
            PermissionNames.DoctorPracticeConfigurationManageOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRACTICE_BRANDING", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorPracticeBrandingViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRACTICE_BRANDING", FeatureAccessLevel.Full),
        [
            PermissionNames.DoctorPracticeBrandingManageOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "SCHEDULE_AVAILABILITY", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorPracticeScheduleViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "SCHEDULE_AVAILABILITY", FeatureAccessLevel.Full),
        [
            PermissionNames.DoctorPracticeScheduleManageOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "BASIC_SEGMENTS_PRICING", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorPracticeSegmentsViewOwn,
            PermissionNames.DoctorPracticePricingViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "BASIC_SEGMENTS_PRICING", FeatureAccessLevel.Full),
        [
            PermissionNames.DoctorPracticeSegmentsManageOwn,
            PermissionNames.DoctorPracticePricingManageOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RECEPTION_MANAGEMENT", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.ReceptionUsersViewOwn,
            PermissionNames.ReceptionAssignmentsViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RECEPTION_MANAGEMENT", FeatureAccessLevel.Full),
        [
            PermissionNames.ReceptionUsersManageOwn,
            PermissionNames.ReceptionAssignmentsManageOwn,
            SubscriptionPermissionNames.ReceptionAccountStateOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RESERVATION_MANAGEMENT", FeatureAccessLevel.Full),
        [
            PermissionNames.PracticeReservationsManage,
            PermissionNames.DoctorPracticeReservationsCancelOwn,
            PermissionNames.DoctorPracticeReservationsRescheduleOwn,
            PermissionNames.PracticeReservationsCreate,
            PermissionNames.PracticeReservationsCancel,
            PermissionNames.PracticeReservationsReschedule,
            PermissionNames.PracticeReservationsRestoreNoShow,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "TICKET_QUEUE", FeatureAccessLevel.Full),
        [
            PermissionNames.PracticeQueueManage,
            PermissionNames.PracticeTicketsCall,
            PermissionNames.PracticeTicketsManualCall,
            PermissionNames.PracticeTicketsRestoreNoShow,
            PermissionNames.PracticeTicketsCancel,
            PermissionNames.DoctorPracticeTicketsCallOwn,
            PermissionNames.DoctorPracticeTicketsConfirmNoResponseOwn,
            PermissionNames.DoctorPracticeTicketsManualCallOwn,
            PermissionNames.DoctorPracticeTicketsRestoreNoShowOwn,
            PermissionNames.DoctorPracticeTicketsCancelOwn,
            PermissionNames.DoctorPracticeTicketsStartOwn,
            PermissionNames.DoctorPracticeTicketsCompleteOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PAYMENT_COLLECTION", FeatureAccessLevel.Full),
        [
            PermissionNames.PracticePaymentsRecord,
            PermissionNames.PracticeTicketsRecordPayment,
            PermissionNames.PracticePaymentsCorrect,
            PermissionNames.PracticePaymentsRefund,
            PermissionNames.DoctorPracticePaymentsCorrectOwn,
            PermissionNames.DoctorPracticePaymentsRefundOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "CHECKIN_WALKIN", FeatureAccessLevel.Full),
        [
            PermissionNames.PracticeWalkInsCreate,
            PermissionNames.PracticeTicketsCheckIn,
            PermissionNames.PracticeTicketsForceCheckIn,
            PermissionNames.PracticeTicketsCreateWalkIn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.UniversalPatient),
        [
            PermissionNames.ReservationsViewOwn,
            PermissionNames.ReservationsCreateOwn,
            PermissionNames.ReservationsCancelOwn,
            PermissionNames.ReservationsRescheduleOwn,
            PermissionNames.ReservationsViewDependents,
            PermissionNames.ReservationsCreateDependents,
            PermissionNames.ReservationsCancelDependents,
            PermissionNames.ReservationsRescheduleDependents,
            PermissionNames.TicketsViewOwn,
            PermissionNames.PaymentsViewOwn,
            PermissionNames.MedicalEncountersViewOwnCompleted,
            PermissionNames.DiagnosesViewOwnCompleted,
            PermissionNames.PrescriptionsViewOwnCompleted,
            PermissionNames.LabRequestsViewOwnIssued,
            PermissionNames.LabResultsViewOwnCurrent,
            PermissionNames.LabResultSubmissionsCreateOwn,
            PermissionNames.LabResultSubmissionsWithdrawOwn,
            PermissionNames.RadiologyRequestsViewOwnIssued,
            PermissionNames.RadiologyResultsViewOwnCurrent,
            PermissionNames.RadiologyResultSubmissionsCreateOwn,
            PermissionNames.RadiologyResultSubmissionsWithdrawOwn,
            Phase18PermissionNames.PatientDashboard,
            Phase18PartTwoPermissionNames.TicketsOwn,
            Phase18PartTwoPermissionNames.ReservationsOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RESERVATION_MANAGEMENT", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorPracticeReservationsViewOwn,
            PermissionNames.PracticeReservationsView,
            Phase18PartTwoPermissionNames.ReservationsPractice,
            Phase18PartTwoPermissionNames.ReservationsAssigned,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "TICKET_QUEUE", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.PracticeTicketsView,
            PermissionNames.DoctorPracticeTicketsViewOwn,
            Phase18PermissionNames.ReceptionDashboard,
            Phase18PartTwoPermissionNames.TicketsPractice,
            Phase18PartTwoPermissionNames.TicketsAssigned,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PAYMENT_COLLECTION", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.PracticePaymentsView,
            PermissionNames.DoctorPracticePaymentsViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "DOCTOR_REVENUE_DASHBOARD", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DoctorRevenueViewOwn,
            Phase18PermissionNames.DoctorDashboard,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "MEDICAL_ENCOUNTER", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.MedicalEncountersViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "MEDICAL_ENCOUNTER", FeatureAccessLevel.Full),
        [
            PermissionNames.MedicalEncountersStartOwn,
            PermissionNames.MedicalEncountersUpdateOwn,
            PermissionNames.MedicalEncountersCompleteOwn,
            PermissionNames.MedicalEncountersAmendOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "DIAGNOSIS", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DiagnosesViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "DIAGNOSIS", FeatureAccessLevel.Full),
        [
            PermissionNames.DiagnosesManageOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "FOLLOWUP_ELIGIBILITY", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.FollowUpEligibilityViewOwn,
            PermissionNames.FollowUpEligibilityViewBookingEligibility,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "FOLLOWUP_ELIGIBILITY", FeatureAccessLevel.Full),
        [
            PermissionNames.FollowUpEligibilityCreateOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRESCRIPTION", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.DrugCatalogSearchActive,
            PermissionNames.DrugCatalogRequestsViewOwn,
            PermissionNames.PrescriptionsViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PRESCRIPTION", FeatureAccessLevel.Full),
        [
            PermissionNames.DrugCatalogRequestsCreateOwn,
            PermissionNames.DrugCatalogRequestsUpdateOwn,
            PermissionNames.PrescriptionsManageOwnDraft,
            PermissionNames.PrescriptionsCorrectOwn,
            PermissionNames.PrescriptionsVoidOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "LAB_RADIOLOGY", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.LabCatalogSearchActive,
            PermissionNames.LabCatalogRequestsViewOwn,
            PermissionNames.LabRequestsViewOwn,
            PermissionNames.LabResultsViewOwn,
            PermissionNames.LabResultSubmissionsViewOwn,
            PermissionNames.RadiologyCatalogSearchActive,
            PermissionNames.RadiologyCatalogRequestsViewOwn,
            PermissionNames.RadiologyRequestsViewOwn,
            PermissionNames.RadiologyResultsViewOwn,
            PermissionNames.RadiologyResultSubmissionsViewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "LAB_RADIOLOGY", FeatureAccessLevel.Full),
        [
            PermissionNames.LabCatalogRequestsCreateOwn,
            PermissionNames.LabCatalogRequestsUpdateOwn,
            PermissionNames.LabRequestsManageOwnDraft,
            PermissionNames.LabRequestsCreatePostVisitOwn,
            PermissionNames.LabRequestsCancelOwn,
            PermissionNames.LabResultsUploadOwn,
            PermissionNames.LabResultsCorrectOwn,
            PermissionNames.LabResultsVoidOwn,
            PermissionNames.LabResultSubmissionsReviewOwn,
            PermissionNames.RadiologyCatalogRequestsCreateOwn,
            PermissionNames.RadiologyCatalogRequestsUpdateOwn,
            PermissionNames.RadiologyRequestsManageOwnDraft,
            PermissionNames.RadiologyRequestsCreatePostVisitOwn,
            PermissionNames.RadiologyRequestsCancelOwn,
            PermissionNames.RadiologyResultsUploadOwn,
            PermissionNames.RadiologyResultsCorrectOwn,
            PermissionNames.RadiologyResultsVoidOwn,
            PermissionNames.RadiologyResultSubmissionsReviewOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "MEDICAL_RECORD_ACCESS", FeatureAccessLevel.ReadOnly),
        [
            PermissionNames.MedicalRecordsViewTreatingPatient,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "OPERATIONAL_REPORTS", FeatureAccessLevel.ReadOnly),
        [
            Phase18PermissionNames.DoctorReports,
            Phase18PermissionNames.ReceptionReports,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "PDF_XLSX_EXPORT", FeatureAccessLevel.Full),
        [
            Phase18PermissionNames.DoctorExportOperational,
            Phase18PermissionNames.DoctorExportFinancial,
            Phase18PermissionNames.ReceptionExport,
            PermissionNames.PrescriptionsExportOwn,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "OPERATING_EXPENSES", FeatureAccessLevel.ReadOnly),
        [
            Phase18PermissionNames.ExpensesView,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "OPERATING_EXPENSES", FeatureAccessLevel.Full),
        [
            Phase18PermissionNames.ExpensesManage,
            Phase18PermissionNames.ExpensesSettle,
            Phase18PermissionNames.ExpensesReverse,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "SALARY_MANAGEMENT", FeatureAccessLevel.ReadOnly),
        [
            Phase18PermissionNames.SalaryPlansView,
            Phase18PermissionNames.SalaryObligationsView,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "SALARY_MANAGEMENT", FeatureAccessLevel.Full),
        [
            Phase18PermissionNames.SalaryPlansManage,
            Phase18PermissionNames.SalarySettle,
            Phase18PermissionNames.SalaryReverse,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RECEPTION_HANDOVER", FeatureAccessLevel.ReadOnly),
        [
            Phase18PermissionNames.DoctorHandoversView,
            Phase18PermissionNames.HandoverView,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "RECEPTION_HANDOVER", FeatureAccessLevel.Full),
        [
            Phase18PermissionNames.DoctorHandoversManage,
            Phase18PermissionNames.HandoverCreate,
            Phase18PermissionNames.HandoverAcknowledge,
            Phase18PermissionNames.HandoverClose,
            Phase18PermissionNames.HandoverAnnotate,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "ADVANCED_RULE_BASED_INSIGHTS", FeatureAccessLevel.ReadOnly),
        [
            Phase18PermissionNames.InsightsView,
        ]);
        Add(result, new(SubscriptionPermissionCategory.CommercialFeature, "ADVANCED_RULE_BASED_INSIGHTS", FeatureAccessLevel.Full),
        [
            Phase18PermissionNames.InsightsTrack,
        ]);
        Add(result, new(SubscriptionPermissionCategory.InternalSystem),
        [
            Phase18PartTwoPermissionNames.AuditPractice,
            Phase18PartTwoPermissionNames.AuditOwnActivity,
        ]);
        return result;
    }
    private static void Add(Dictionary<string, SubscriptionPermissionClassification> result,
        SubscriptionPermissionClassification classification, IReadOnlyList<string> permissions)
    {
        foreach (var permission in permissions) result.Add(permission, classification);
    }
}
