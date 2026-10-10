using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketPriorityRulesTests
{
    private static readonly Category Technical = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);

    [Fact]
    public void Decide_UsesTheRequestedPriority()
    {
        var decision = TicketPriorityRules.Decide(TicketPriority.High, Technical);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Contains("requested", decision.Reason);
    }

    [Fact]
    public void Decide_WithoutARequest_DefaultsToMedium()
    {
        var decision = TicketPriorityRules.Decide(null, Technical);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Contains("defaults to Medium", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(null)]
    public void Decide_ACategoryThatForcesCritical_IsAlwaysCritical(TicketPriority? requested)
    {
        var decision = TicketPriorityRules.Decide(requested, Security);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Security", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Raise_GoesUpExactlyOneLevel(TicketPriority from, TicketPriority expected) =>
        Assert.Equal(expected, TicketPriorityRules.Raise(from));

    [Fact]
    public void Raise_FromCritical_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TicketPriorityRules.Raise(TicketPriority.Critical));
}
