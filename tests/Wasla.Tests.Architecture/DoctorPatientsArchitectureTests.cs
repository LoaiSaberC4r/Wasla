using BuildingBlock.Application.Abstraction;
using Wasla.Application.Features.DoctorPatients;

namespace Wasla.Tests.Architecture;

public sealed class DoctorPatientsArchitectureTests
{
    [Fact]
    public void Patient_workspace_is_a_read_model_without_a_patient_ownership_aggregate_or_commands()
    {
        var application = typeof(DoctorPatientsQuery).Assembly;
        var types = application.GetTypes().Where(t => t.Namespace == typeof(DoctorPatientsQuery).Namespace).ToArray();
        Assert.DoesNotContain(types, t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));
        Assert.DoesNotContain(typeof(Wasla.Domain.Patients.Patient).Assembly.GetTypes(), t => t.Name == "DoctorPatient");
        Assert.DoesNotContain(application.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal) || a.Name.Contains("Puppeteer", StringComparison.Ordinal));
    }
    [Fact]
    public void Directory_and_overview_contracts_have_no_private_profile_or_mutation_fields()
    {
        foreach (var type in new[] { typeof(DoctorPatientListItem), typeof(DoctorMyPatientOverviewResponse), typeof(DoctorPatientDiagnosis) })
            Assert.DoesNotContain(type.GetProperties(), p => p.Name is "RowVersion" or "ClinicalNotes" or "PatientContact" or "Guardian" or "PrivateMediaKey" or "PersonalIdFrontMediaKey");
    }
}
