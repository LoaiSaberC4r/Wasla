using Wasla.Application.Features.Families;
using Wasla.Domain.Families;

namespace Wasla.Tests.Unit;

public sealed class Phase16FamilyMedicalDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    private static FamilyMedicalAccessRequest Restriction(Guid actor) => FamilyMedicalAccessRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), actor,
        FamilyMedicalRequestType.Restriction, " justification ", Now).Value;
    [Theory]
    [InlineData("approve", FamilyMedicalRequestStatus.Approved)]
    [InlineData("reject", FamilyMedicalRequestStatus.Rejected)]
    [InlineData("withdraw", FamilyMedicalRequestStatus.Withdrawn)]
    public void Terminal_requests_cannot_be_reopened(string action, FamilyMedicalRequestStatus status)
    {
        var actor = Guid.NewGuid(); var r = Restriction(actor);
        Assert.True(r.Transition(action, actor, Now, "Reason").IsSuccess); Assert.Equal(status, r.Status);
        Assert.True(r.Transition("resubmit", actor, Now, "New reason").IsFailure); Assert.True(r.Transition("approve", actor, Now).IsFailure);
    }
    [Fact]
    public void Modification_resubmits_same_case_with_incremented_revision_and_withdraw_is_owned()
    {
        var actor = Guid.NewGuid(); var r = Restriction(actor); var id = r.Id;
        Assert.True(r.Transition("request-modification", Guid.NewGuid(), Now, "Proof please").IsSuccess);
        Assert.True(r.IsOpen); Assert.True(r.Transition("approve", actor, Now).IsFailure);
        Assert.True(r.Transition("withdraw", Guid.NewGuid(), Now).IsFailure);
        Assert.True(r.Transition("resubmit", actor, Now, "Revised").IsSuccess); Assert.Equal(2, r.CurrentRevisionNumber); Assert.Equal(id, r.Id);
    }
    [Fact]
    public void Restoration_requires_self_and_exact_restriction_and_never_reactivates_lifted_record()
    {
        var actor = Guid.NewGuid(); var request = Restriction(actor); var admin = Guid.NewGuid();
        var restriction = FamilyMedicalAccessRestriction.Activate(request, admin, Now);
        var restoration = FamilyMedicalAccessRequest.Create(request.FamilyId, request.ChildPatientId, request.AffectedAdultPatientId,
            request.AffectedAdultPatientId, actor, FamilyMedicalRequestType.Restoration, "Restore", Now, restriction.Id).Value;
        Assert.True(restriction.Lift(restoration, admin, Now).IsSuccess); Assert.Equal(FamilyMedicalRestrictionStatus.Lifted, restriction.Status);
        Assert.True(restriction.Lift(restoration, admin, Now).IsFailure);
        Assert.True(FamilyMedicalAccessRequest.Create(request.FamilyId, request.ChildPatientId, request.AffectedAdultPatientId,
            Guid.NewGuid(), actor, FamilyMedicalRequestType.Restoration, "Restore", Now, restriction.Id).IsFailure);
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_justification_is_invalid(string reason)
        => Assert.True(FamilyMedicalAccessRequest.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FamilyMedicalRequestType.Restriction, reason, Now).IsFailure);
    [Fact]
    public void Governance_options_default_fail_closed_even_after_product_approval()
    { var p = new FamilyMedicalPolicyOptions { Approved = true }; Assert.False(p.IsConfigured); }
}
