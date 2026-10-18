using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Patients;
namespace Wasla.Application.Features.PatientIdentityMatching;
public sealed class PatientIdentityReuseGuard(IWaslaDataStore data, IPatientIdentityStore store, IDateTimeProvider clock)
{
    public async Task<Result<PatientIdentityAttestation?>> ValidateAsync(Guid patientId, Guid practiceId, Guid actorId, Guid? attestationId, CancellationToken ct)
    {
        await store.LockAsync(ct);
        if (attestationId is { } id)
        {
            var a = await store.AttestationAsync(id, ct);
            return a is not null && a.Purpose == PatientIdentityAttestationPurpose.ReuseForBooking && a.IsValid(patientId, practiceId, actorId, clock.UtcNow)
                ? Result<PatientIdentityAttestation?>.Ok(a) : Result<PatientIdentityAttestation?>.Fail(PatientIdentityErrors.Conflict("Patient.IdentityAttestationInvalid"));
        }
        if (await store.HasPracticeRelationshipAsync(patientId, practiceId, ct)) return Result<PatientIdentityAttestation?>.Ok(null);
        var patient = await data.FindPatientByIdAsync(patientId, ct);
        if (patient is not null && patient.RegistrationDoctorPracticeId == practiceId && patient.RegistrationReceptionApplicationUserId == actorId &&
            patient.ReceptionRegisteredOnUtc is { } created && clock.UtcNow < created.AddMinutes(15)) return Result<PatientIdentityAttestation?>.Ok(null);
        return Result<PatientIdentityAttestation?>.Fail(PatientIdentityErrors.Conflict("Patient.IdentityAttestationRequired"));
    }
}
