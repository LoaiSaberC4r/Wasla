using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ClinicalPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is DbUpdateConcurrencyException concurrency)
        {
            if (concurrency.Entries.Any(e => e.Entity is FollowUpEligibility)) error = FollowUpErrors.ConcurrencyConflict;
            else if (concurrency.Entries.Any(e => e.Entity is MedicalEncounter)) error = ClinicalErrors.ConcurrencyConflict;
            return error is not null;
        }
        if (exception is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627)
        {
            if (sql.Message.Contains("UX_MedicalEncounters_TicketId", StringComparison.Ordinal)) error = ClinicalErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_FollowUpEligibilities_Source", StringComparison.Ordinal)) error = FollowUpErrors.AlreadyExists;
            else if (sql.Message.Contains("UX_Diagnoses_ActivePrimary", StringComparison.Ordinal)) error = ClinicalErrors.MultiplePrimary;
            else if (sql.Message.Contains("UX_Diagnoses_ActiveText", StringComparison.Ordinal)) error = ClinicalErrors.DiagnosisDuplicate;
        }
        return error is not null;
    }
}
