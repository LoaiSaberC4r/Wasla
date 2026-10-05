using BuildingBlock.Domain.Primitive;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Medications;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Architecture;

public sealed class Phase14MedicationArchitectureTests
{
    [Fact]
    public void Prescription_and_history_are_separate_from_ticket_and_not_soft_deletable()
    {
        Assert.DoesNotContain(typeof(Ticket).GetProperties(), p => p.Name.Contains("Prescription", StringComparison.Ordinal));
        foreach (var type in new[] { typeof(Prescription), typeof(PrescriptionVersion), typeof(PrescriptionItem), typeof(PrescriptionAuditEvent), typeof(DrugCatalogHistory), typeof(DrugCatalogRequestHistory) })
            Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(type));
        Assert.DoesNotContain(typeof(PrescriptionItem).GetProperties(), p => p.Name.Contains("Price", StringComparison.Ordinal) || p.Name is "InternalNotes" or "Notes");
    }
    [Fact]
    public void Manager_defaults_and_reception_assignments_have_no_clinical_permissions()
    {
        Assert.All(PermissionNames.DrugCatalogManagerDefaults, p => Assert.True(p.StartsWith("DrugCatalog.", StringComparison.Ordinal) || p.StartsWith("DrugCatalogRequests.", StringComparison.Ordinal)));
        Assert.DoesNotContain(PermissionNames.ReceptionAssignmentScoped, p => p.StartsWith("Prescriptions.", StringComparison.Ordinal));
        Assert.Contains(PermissionNames.DrugCatalogManagersCreate, PermissionNames.RootOnly);
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000005"), SystemRoleIds.DrugCatalogManager);
    }
    [Fact]
    public void Patient_queries_and_output_cannot_select_arbitrary_patient_or_expose_internal_context()
    {
        Assert.DoesNotContain(typeof(ListMyPrescriptionsQuery).GetProperties(), p => p.Name == "PatientId");
        Assert.DoesNotContain(typeof(GetMyPrescriptionQuery).GetProperties(), p => p.Name == "PatientId");
        foreach (var type in new[] { typeof(PatientPrescriptionResponse), typeof(PatientPrescriptionItemResponse), typeof(DrugRequestResponse) })
            Assert.DoesNotContain(type.GetProperties(), p => p.Name is "PatientId" or "ClinicalNotes" or "Diagnosis" or "CorrectionReason" or "DrugCatalogRequestId" or "Audit");
    }
    [Fact]
    public void Medication_application_has_no_ef_infrastructure_or_http_runtime_dependency()
    {
        var assembly = Wasla.Application.AssemblyReference.Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal) || a.Name.Contains("Wasla.Infrastructure", StringComparison.Ordinal));
        var types = assembly.GetTypes().Where(t => t.Namespace?.StartsWith("Wasla.Application.Features.Medications", StringComparison.Ordinal) == true);
        Assert.All(types, t => Assert.DoesNotContain(t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public), f => f.FieldType == typeof(HttpClient)));
    }
}
