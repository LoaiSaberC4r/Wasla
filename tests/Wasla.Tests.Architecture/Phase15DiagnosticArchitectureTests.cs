using BuildingBlock.Domain.Primitive;
using Wasla.Application.Features.Diagnostics;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Architecture;

public sealed class Phase15DiagnosticArchitectureTests
{
    [Fact]
    public void Clinical_models_are_distinct_aggregates_with_retained_history_and_no_ticket_payload()
    {
        foreach (var t in new[] { typeof(LabRequest), typeof(RadiologyRequest), typeof(LabResult), typeof(RadiologyResult), typeof(PatientLabResultSubmission), typeof(PatientRadiologyResultSubmission) })
        { Assert.True(typeof(IAggregateRoot).IsAssignableFrom(t)); Assert.False(t.IsGenericType); Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(t)); }
        Assert.DoesNotContain(typeof(Ticket).GetProperties(), p => p.Name.Contains("Lab", StringComparison.Ordinal) || p.Name.Contains("Radiology", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(LabCatalogRequest).GetProperties(), p => p.Name is "PatientId" or "PracticeId" or "MedicalEncounterId");
        Assert.DoesNotContain(typeof(RadiologyCatalogRequest).GetProperties(), p => p.Name is "PatientId" or "PracticeId" or "MedicalEncounterId");
    }
    [Fact]
    public void Catalog_manager_and_root_permissions_do_not_create_clinical_authority()
    {
        Assert.Equal(20, PermissionNames.MedicalCatalogManagerDefaults.Count);
        Assert.All(PermissionNames.MedicalCatalogManagerDefaults, p => Assert.True(p.StartsWith("LabCatalog", StringComparison.Ordinal) || p.StartsWith("RadiologyCatalog", StringComparison.Ordinal)));
        Assert.Contains(PermissionNames.MedicalCatalogManagersCreate, PermissionNames.RootOnly);
        Assert.DoesNotContain(PermissionNames.RootOnly, p => p.StartsWith("LabResults", StringComparison.Ordinal) || p.StartsWith("RadiologyResults", StringComparison.Ordinal));
        Assert.Equal(PermissionNames.All.Count, PermissionNames.All.Distinct().Count());
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000006"), SystemRoleIds.MedicalCatalogManager);
    }
    [Fact]
    public void Diagnostic_application_has_no_EF_or_HTTP_dependency_and_private_contracts_have_no_storage_keys()
    {
        var assembly = Wasla.Application.AssemblyReference.Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal) || a.Name.Contains("Wasla.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(DiagnosticAttachmentResponse).GetProperties(), p => p.Name is "PrivateMediaKey" or "PublicUrl" or "Content");
        Assert.DoesNotContain(typeof(LabResultVersion).GetProperties(), p => p.Name is "Value" or "Unit" or "ReferenceRange" or "AbnormalFlag");
        Assert.DoesNotContain(typeof(DiagnosticOrderCommand).GetProperties(), p => p.Name is "PatientId" or "DoctorId");
    }
    [Fact]
    public void Sensitive_commands_use_the_existing_transaction_marker()
    {
        var marker = typeof(BuildingBlock.Application.Abstraction.Persistence.ITransactionalCommand<Wasla.Application.Persistence.WaslaWritePersistence>);
        foreach (var t in new[] { typeof(DiagnosticOrderCommand), typeof(DiagnosticResultCommand), typeof(MutateMedicalCatalogCommand), typeof(MutateMedicalCatalogRequestCommand), typeof(MutateDiagnosticImportCommand) }) Assert.True(marker.IsAssignableFrom(t));
        Assert.Equal(DiagnosticUploader.Patient, (DiagnosticUploader)2);
    }
}
