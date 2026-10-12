using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;

namespace Wasla.Tests.Architecture;

public sealed class Phase16MedicalRecordArchitectureTests
{
    [Fact]
    public void Central_record_uses_existing_sources_and_transactional_mutations_without_new_clinical_content_aggregate()
    {
        Assert.True(typeof(IAggregateRoot).IsAssignableFrom(typeof(HiddenEncounterAccessRequest)));
        Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(HiddenEncounterAccessRequest)));
        Assert.DoesNotContain(typeof(HiddenEncounterAccessRequest).GetProperties(), p => p.Name is "ClinicalNotes" or "Diagnosis" or "Prescription" or "Attachments");
        var marker = typeof(ITransactionalCommand<WaslaWritePersistence>);
        foreach (var t in new[] { typeof(ChangeEncounterVisibilityCommand), typeof(RequestHiddenEncounterAccessCommand), typeof(DecideHiddenEncounterAccessCommand) }) Assert.True(marker.IsAssignableFrom(t));
        Assert.DoesNotContain(Wasla.Application.AssemblyReference.Assembly.GetReferencedAssemblies(), a => a.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal) || a.Name.Contains("Wasla.Infrastructure", StringComparison.Ordinal));
        Assert.False(new MedicalRecordOptions().CrossDoctorSharingEnabled); Assert.False(new MedicalRecordOptions().GuardianSharingEnabled);
    }
}
