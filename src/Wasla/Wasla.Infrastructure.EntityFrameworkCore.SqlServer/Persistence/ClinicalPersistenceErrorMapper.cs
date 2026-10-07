using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ClinicalPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is DbUpdateConcurrencyException concurrency)
        {
            var diagnostic = concurrency.Entries.Select(e => DiagnosticConflict(e.Entity)).FirstOrDefault(c => c is not null);
            if (diagnostic is not null) error = DiagnosticErrors.Conflict(diagnostic);
            else if (concurrency.Message is "LabResult.ConcurrencyConflict" or "RadiologyResult.ConcurrencyConflict") error = DiagnosticErrors.Conflict(concurrency.Message);
            else if (concurrency.Entries.Any(e => e.Entity is Prescription or PrescriptionVersion or PrescriptionItem) || concurrency.Message == "Prescription.ConcurrencyConflict") error = MedicationErrors.Conflict("Prescription.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalog)) error = MedicationErrors.Conflict("DrugCatalog.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalogRequest)) error = MedicationErrors.Conflict("DrugCatalogRequest.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalogImportBatch)) error = MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is FollowUpEligibility)) error = FollowUpErrors.ConcurrencyConflict;
            else if (concurrency.Entries.Any(e => e.Entity is MedicalEncounter)) error = ClinicalErrors.ConcurrencyConflict;
            return error is not null;
        }
        if (exception is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627)
        {
            var diagnostic = exception is DbUpdateException update ? update.Entries.Select(e => DiagnosticConflict(e.Entity)).FirstOrDefault(c => c is not null) : null;
            if (diagnostic is not null) error = DiagnosticErrors.Conflict(diagnostic);
            else if (sql.Message.Contains("UX_Prescriptions_Encounter", StringComparison.Ordinal)) error = MedicationErrors.Conflict("Prescription.AlreadyExistsForEncounter");
            else if (sql.Message.Contains("UX_PrescriptionVersions_", StringComparison.Ordinal)) error = MedicationErrors.Conflict("Prescription.ConcurrencyConflict");
            else if (sql.Message.Contains("UX_DrugCatalogs_SourceIdentity", StringComparison.Ordinal)) error = MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict");
            else if (sql.Message.Contains("UX_MedicalEncounters_TicketId", StringComparison.Ordinal)) error = ClinicalErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_FollowUpEligibilities_Source", StringComparison.Ordinal)) error = FollowUpErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_Diagnoses_ActivePrimary", StringComparison.Ordinal)) error = ClinicalErrors.MultiplePrimary;
            else if (sql.Message.Contains("UX_Diagnoses_ActiveText", StringComparison.Ordinal)) error = ClinicalErrors.DiagnosisDuplicate;
        }
        return error is not null;
    }

    private static string? DiagnosticConflict(object entity) => entity switch
    {
        LabRequest or LabRequestItem => "LabRequest.ConcurrencyConflict",
        RadiologyRequest or RadiologyRequestItem => "RadiologyRequest.ConcurrencyConflict",
        LabResult or LabResultVersion or LabResultCoverage => "LabResult.ConcurrencyConflict",
        RadiologyResult or RadiologyResultVersion or RadiologyResultCoverage => "RadiologyResult.ConcurrencyConflict",
        PatientLabResultSubmission => "LabResultSubmission.ConcurrencyConflict",
        PatientRadiologyResultSubmission => "RadiologyResultSubmission.ConcurrencyConflict",
        LabTestCatalog => "LabCatalog.ConcurrencyConflict",
        RadiologyProcedureCatalog => "RadiologyCatalog.ConcurrencyConflict",
        LabCatalogRequest => "LabCatalogRequest.ConcurrencyConflict",
        RadiologyCatalogRequest => "RadiologyCatalogRequest.ConcurrencyConflict",
        LabCatalogImportBatch or RadiologyCatalogImportBatch => "DiagnosticCatalogImport.ConcurrencyConflict",
        _ => null
    };
}
