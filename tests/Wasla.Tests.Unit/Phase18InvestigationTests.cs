using Wasla.Domain.Operations;

namespace Wasla.Tests.Unit;

public sealed class Phase18InvestigationTests
{
    [Fact]
    public void Review_requires_active_assignee_and_resolution_closure_and_reopen_require_notes()
    {
        var actor = Guid.NewGuid(); var now = DateTime.UtcNow; var assignee = Guid.NewGuid();
        var row = AdministrativeInvestigation.Create(Guid.NewGuid(), "مراجعة تشغيل", null, "Review operation",
            InvestigationIssueType.AdministrativeSupport, InvestigationPriority.Normal, InvestigationResourceType.None, null, actor, now).Value;
        Assert.True(row.StartReview(false, actor, now).IsFailure);
        Assert.True(row.Assign(assignee, false, actor, now).IsFailure);
        Assert.True(row.Assign(assignee, true, actor, now).IsSuccess);
        Assert.True(row.StartReview(true, actor, now).IsSuccess);
        Assert.True(row.Close("Closed", actor, now).IsFailure);
        Assert.True(row.Resolve(InvestigationResolutionType.Verified, "", true, actor, now).IsFailure);
        Assert.True(row.Resolve(InvestigationResolutionType.Verified, "Operation verified", true, actor, now).IsSuccess);
        Assert.True(row.Close("", actor, now).IsFailure);
        Assert.True(row.Close("Review complete", actor, now).IsSuccess);
        Assert.True(row.Reopen("", true, actor, now).IsFailure);
        Assert.True(row.Reopen("New administrative evidence", false, actor, now).IsFailure);
        Assert.True(row.Reopen("New administrative evidence", true, actor, now).IsSuccess);
        Assert.Null(row.ResolutionType); Assert.Null(row.ClosedOnUtc);
        Assert.Equal(6, row.History.Count);
    }
    [Fact]
    public void Incorrect_open_case_can_be_withdrawn_without_granting_resource_access()
    {
        var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        var row = AdministrativeInvestigation.Create(Guid.NewGuid(), "حالة غير صحيحة", null, "Review operation",
            InvestigationIssueType.AdministrativeSupport, InvestigationPriority.Normal, InvestigationResourceType.None, null, actor, now).Value;
        Assert.True(row.Close("Created in error", actor, now).IsSuccess);
        Assert.Equal("WithdrawnInvalidCase", row.History.Last().Operation);
        Assert.True(row.Reopen("Review again", true, actor, now).IsFailure);
    }
}
