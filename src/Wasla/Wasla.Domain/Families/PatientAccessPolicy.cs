using Wasla.Domain.Patients;

namespace Wasla.Domain.Families;

public interface IPatientAccessPolicy
{
    bool CanManageFamily(FamilyMember actor);
    bool CanAccessDependentMedicalData(FamilyMember actor, FamilyMember subject, Patient subjectPatient, DateOnly today);
}

public sealed class PatientAccessPolicy : IPatientAccessPolicy
{
    public bool CanManageFamily(FamilyMember actor) => actor.CanManageFamily;

    public bool CanAccessDependentMedicalData(FamilyMember actor, FamilyMember subject, Patient subjectPatient, DateOnly today)
        => actor.IsActive && subject.IsActive && actor.FamilyId == subject.FamilyId &&
           actor.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother or FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian &&
           subject.Role == FamilyMemberRole.Child && subjectPatient.Id == subject.PatientId && subjectPatient.GetAge(today) < 16;
}
