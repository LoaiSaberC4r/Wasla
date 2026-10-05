using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ClinicalPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is DbUpdateConcurrencyException concurrency)
        {
            if (concurrency.Entries.Any(e => e.Entity is Prescription or PrescriptionVersion or PrescriptionItem) || concurrency.Message == "Prescription.ConcurrencyConflict") error = MedicationErrors.Conflict("Prescription.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalog)) error = MedicationErrors.Conflict("DrugCatalog.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalogRequest)) error = MedicationErrors.Conflict("DrugCatalogRequest.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is DrugCatalogImportBatch)) error = MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict");
            else if (concurrency.Entries.Any(e => e.Entity is FollowUpEligibility)) error = FollowUpErrors.ConcurrencyConflict;
            else if (concurrency.Entries.Any(e => e.Entity is MedicalEncounter)) error = ClinicalErrors.ConcurrencyConflict;
            return error is not null;
        }
        if (exception is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627)
        {
            if (sql.Message.Contains("UX_Prescriptions_Encounter", StringComparison.Ordinal)) error = MedicationErrors.Conflict("Prescription.AlreadyExistsForEncounter");
            else if (sql.Message.Contains("UX_PrescriptionVersions_", StringComparison.Ordinal)) error = MedicationErrors.Conflict("Prescription.ConcurrencyConflict");
            else if (sql.Message.Contains("UX_DrugCatalogs_SourceIdentity", StringComparison.Ordinal)) error = MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict");
            else if (sql.Message.Contains("UX_MedicalEncounters_TicketId", StringComparison.Ordinal)) error = ClinicalErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_FollowUpEligibilities_Source", StringComparison.Ordinal)) error = FollowUpErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_Diagnoses_ActivePrimary", StringComparison.Ordinal)) error = ClinicalErrors.MultiplePrimary;
            else if (sql.Message.Contains("UX_Diagnoses_ActiveText", StringComparison.Ordinal)) error = ClinicalErrors.DiagnosisDuplicate;
        }
        return error is not null;
    }
}
