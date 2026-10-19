using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
internal sealed class PatientIdentityPersistenceErrorMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        error = null!;
        if (exception is SqlException { Number: 51005 }) { error = PatientIdentityErrors.Conflict("PatientClaims.ConcurrencyConflict"); return true; }
        if (exception is DbUpdateConcurrencyException profile && profile.Entries.Any(e => e.Entity is Patient))
        { error = PatientErrors.ConcurrencyConflict; return true; }
        if (exception is DbUpdateConcurrencyException c && c.Entries.Any(e => e.Entity is PatientClaimRequest or PatientOnboardingChallenge or PatientActivationInvitation or PatientIdentityAttestation or PatientDuplicateCase))
        { error = PatientIdentityErrors.Conflict("PatientClaims.ConcurrencyConflict"); return true; }
        if (exception is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627)
        {
            if (sql.Message.Contains("UX_Patients_NormalizedEmail", StringComparison.Ordinal)) error = PatientIdentityErrors.Conflict("Patient.AlreadyExists");
            else if (sql.Message.Contains("UX_ApplicationUsers_Email", StringComparison.Ordinal)) error = PatientIdentityErrors.Conflict("Identity.AlreadyRegistered");
            else if (sql.Message.Contains("UX_ApplicationUsers_UserName", StringComparison.Ordinal)) error = PatientIdentityErrors.Conflict("Identity.UserNameAlreadyExists");
            else if (exception is DbUpdateException update && update.Entries.Any(e => e.Entity is PatientClaimRequest or PatientActivationInvitation or PatientIdentityAttestation or PatientDuplicateCase or PatientAccountLink)) error = PatientIdentityErrors.Conflict("PatientClaims.ConcurrencyConflict");
        }
        return error is not null;
    }
}
