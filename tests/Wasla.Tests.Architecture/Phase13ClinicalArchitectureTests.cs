using BuildingBlock.Domain.Primitive;
using Wasla.Domain.Clinical;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Tests.Architecture;

public sealed class Phase13ClinicalArchitectureTests
{
    [Fact]
    public void Clinical_storage_is_separate_from_operational_ticket_and_not_soft_deletable()
    {
        Assert.DoesNotContain(typeof(Ticket).GetProperties(), p => p.Name is "ClinicalNotes" or "Diagnoses" or "Amendments");
        foreach (var type in new[] { typeof(MedicalEncounter), typeof(Diagnosis), typeof(EncounterAmendment), typeof(EncounterAmendmentChange),
                     typeof(EncounterAuditEvent), typeof(FollowUpEligibility), typeof(FollowUpEligibilityHistory) })
            Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(type));
    }

    [Fact]
    public void Clinical_application_does_not_reference_dbcontext_or_infrastructure()
    {
        var references = Wasla.Application.AssemblyReference.Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, r => r.Name!.Contains("EntityFrameworkCore", StringComparison.Ordinal) || r.Name.Contains("Wasla.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public void Reception_delegation_includes_only_operational_follow_up_permission()
    {
        Assert.Contains(PermissionNames.FollowUpEligibilityViewBookingEligibility, PermissionNames.ReceptionAssignmentScoped);
        Assert.DoesNotContain(PermissionNames.ReceptionAssignmentScoped, p => p.StartsWith("MedicalEncounters.", StringComparison.Ordinal) || p.StartsWith("Diagnoses.", StringComparison.Ordinal));
    }
}
