using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Clinical;

public static class ClinicalErrors
{
    private static Error Validation(string code, string key) => Error.Validation(code, ErrorMessage.GetString(key));
    private static Error Conflict(string code, string key) => Error.Conflict(code, ErrorMessage.GetString(key));
    public static Error NotFound => Error.NotFound("MedicalEncounter.NotFound", ErrorMessage.GetString("EncounterNotFound"));
    public static Error AccessDenied => Error.Security("MedicalEncounter.AccessDenied", ErrorMessage.GetString("EncounterAccessDenied"));
    public static Error AlreadyExists => Conflict("MedicalEncounter.AlreadyExistsForTicket", "EncounterAlreadyExists");
    public static Error InvalidState => Conflict("MedicalEncounter.InvalidState", "EncounterInvalidState");
    public static Error NotesRequired => Validation("MedicalEncounter.ClinicalNotesRequired", "EncounterNotesRequired");
    public static Error NotesTooLong => Validation("MedicalEncounter.ClinicalNotesTooLong", "EncounterNotesTooLong");
    public static Error ConcurrencyConflict => Conflict("MedicalEncounter.ConcurrencyConflict", "EncounterConcurrencyConflict");
    public static Error DiagnosisNotFound => Error.NotFound("Diagnosis.NotFound", ErrorMessage.GetString("DiagnosisNotFound"));
    public static Error DiagnosisInvalid => Validation("Diagnosis.Invalid", "DiagnosisInvalid");
    public static Error DiagnosisDuplicate => Conflict("Diagnosis.Duplicate", "DiagnosisDuplicate");
    public static Error PrimaryRequired => Validation("Diagnosis.PrimaryRequired", "DiagnosisPrimaryRequired");
    public static Error MultiplePrimary => Validation("Diagnosis.MultiplePrimary", "DiagnosisMultiplePrimary");
    public static Error AmendmentInvalidState => Conflict("EncounterAmendment.InvalidState", "AmendmentInvalidState");
    public static Error AmendmentReasonRequired => Validation("EncounterAmendment.ReasonRequired", "AmendmentReasonRequired");
    public static Error AmendmentInvalidChange => Validation("EncounterAmendment.InvalidChange", "AmendmentInvalidChange");
}

public static class FollowUpErrors
{
    private static Error Conflict(string code, string key) => Error.Conflict("FollowUpEligibility." + code, ErrorMessage.GetString(key));
    public static Error NotFound => Error.NotFound("FollowUpEligibility.NotFound", ErrorMessage.GetString("FollowUpNotFound"));
    public static Error AlreadyExists => Conflict("AlreadyExists", "FollowUpAlreadyExists");
    public static Error NotAvailable => Conflict("NotAvailable", "FollowUpNotAvailable");
    public static Error Expired => Conflict("Expired", "FollowUpExpired");
    public static Error InvalidPatient => Conflict("InvalidPatient", "FollowUpInvalidPatient");
    public static Error InvalidPractice => Conflict("InvalidPractice", "FollowUpInvalidPractice");
    public static Error InvalidDoctor => Conflict("InvalidDoctor", "FollowUpInvalidDoctor");
    public static Error InvalidVisitType => Error.Validation("FollowUpEligibility.InvalidVisitType", ErrorMessage.GetString("FollowUpInvalidVisitType"));
    public static Error InvalidDate => Error.Validation("FollowUpEligibility.InvalidDate", ErrorMessage.GetString("FollowUpInvalidDate"));
    public static Error DateOutsideEligibility => Conflict("DateOutsideEligibility", "FollowUpDateOutside");
    public static Error AlreadyReserved => Conflict("AlreadyReserved", "FollowUpAlreadyReserved");
    public static Error AlreadyConsumed => Conflict("AlreadyConsumed", "FollowUpAlreadyConsumed");
    public static Error ConcurrencyConflict => Conflict("ConcurrencyConflict", "FollowUpConcurrencyConflict");
}
