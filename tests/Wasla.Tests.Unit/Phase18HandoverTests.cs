using Wasla.Domain.Operations;

namespace Wasla.Tests.Unit;

public sealed class Phase18HandoverTests
{
    [Fact]
    public void Creator_cannot_acknowledge_and_closed_is_terminal()
    {
        var creator = Guid.NewGuid(); var other = Guid.NewGuid(); var now = DateTime.UtcNow;
        var row = ReceptionHandover.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Review queue operation",
            null, null, null, creator, now).Value;
        Assert.True(row.Acknowledge(creator, now).IsFailure);
        Assert.True(row.Close(false, "Reviewed", other, now).IsFailure);
        Assert.True(row.Acknowledge(other, now).IsSuccess);
        Assert.True(row.Close(false, "Queue operation reviewed", creator, now).IsSuccess);
        Assert.True(row.Annotate("Further review", other, now).IsFailure);
        Assert.True(row.Acknowledge(other, now).IsFailure);
        Assert.Equal(3, row.History.Count);
    }
    [Fact]
    public void Doctor_can_close_open_handover_only_with_outcome()
    {
        var actor = Guid.NewGuid(); var now = DateTime.UtcNow;
        var row = ReceptionHandover.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "<b>Queue</b>", null, null, null, actor, now).Value;
        Assert.Contains("&lt;b&gt;", row.Summary);
        Assert.True(row.Close(true, "", actor, now).IsFailure);
        Assert.True(row.Close(true, "Operation reviewed", actor, now).IsSuccess);
        Assert.Equal("DoctorDirectClosure", row.History.Last().Operation);
    }
    [Theory]
    [InlineData("National ID 29801011234567")] [InlineData("phone: 01012345678")]
    [InlineData("diagnosis")] [InlineData("Salary 300 EGP")] [InlineData("https://example.test")]
    public void Sensitive_notes_are_rejected(string note) => Assert.True(OperationalNotePolicy.Sanitize(note).IsFailure);
}
