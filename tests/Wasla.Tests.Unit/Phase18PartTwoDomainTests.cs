using Wasla.Domain.Archives;
using Wasla.Domain.Tickets;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Tests.Unit;

public sealed class Phase18PartTwoDomainTests
{
    [Theory]
    [InlineData(TicketStatus.Waiting,false)] [InlineData(TicketStatus.Called,false)]
    [InlineData(TicketStatus.InProgress,false)] [InlineData(TicketStatus.NoShow,true)]
    [InlineData(TicketStatus.Completed,true)] [InlineData(TicketStatus.Cancelled,true)]
    public void Eligibility_does_not_add_or_reinterpret_business_states(TicketStatus status,bool eligible)
        => Assert.Equal(eligible,ArchiveEligibility.Terminal(status));
    [Theory]
    [InlineData(ReservationStatus.Active,false)] [InlineData(ReservationStatus.ConvertedToTicket,false)]
    [InlineData(ReservationStatus.Cancelled,true)] [InlineData(ReservationStatus.NoShow,true)] [InlineData(ReservationStatus.Expired,true)]
    public void Converted_reservations_are_never_independent_units(ReservationStatus status,bool eligible)
        => Assert.Equal(eligible,ArchiveEligibility.Standalone(status));
    [Fact]
    public void Closed_observed_day_cannot_be_revised_or_reopened()
    {
        var now=new DateTime(2026,10,14,10,0,0,DateTimeKind.Utc);var end=now.AddHours(8);
        var day=PracticeOperationalDay.Observe(Guid.NewGuid(),new(2026,10,14),"Africa/Cairo",end,"observed",now);
        Assert.True(day.Revise("Africa/Cairo",end.AddHours(1),"legitimate revision",now.AddHours(1)));
        Assert.Throws<InvalidOperationException>(()=>day.Close(end));
        day.Close(end.AddHours(1));var closed=day.ClosedAtUtc;
        Assert.False(day.Revise("UTC",end.AddDays(1),"later config",end.AddHours(2)));
        day.Close(end.AddDays(2));Assert.Equal(closed,day.ClosedAtUtc);Assert.Equal(2,day.Revision);
    }
    [Fact]
    public void Historical_day_cannot_be_fabricated_as_an_observed_snapshot()
    {var now=new DateTime(2026,10,14,10,0,0,DateTimeKind.Utc);Assert.Throws<InvalidOperationException>(()=>PracticeOperationalDay.Observe(Guid.NewGuid(),new(2026,10,13),"UTC",now.AddDays(-1),"fake",now));}
    [Fact]
    public void Restricted_permission_has_no_default_or_delegation_and_existing_prefix_order_is_preserved()
    {
        var original=PermissionNames.All.Take(PermissionNames.All.Count-Phase18PartTwoPermissionNames.All.Count).ToArray();
        Assert.Equal(Phase18PartTwoPermissionNames.All,PermissionNames.All.Skip(original.Length));
        Assert.DoesNotContain(Phase18PartTwoPermissionNames.SensitiveRestricted,Phase18PartTwoPermissionNames.PlatformDefaults);
        Assert.DoesNotContain(Phase18PartTwoPermissionNames.SensitiveRestricted,PermissionNames.ReceptionAssignmentScoped);
        Assert.Equal(3,Phase18PartTwoPermissionNames.ReceptionDefaults.Count);
        Assert.Equal(18,Phase18PartTwoPermissionNames.All.Count);
    }
}
