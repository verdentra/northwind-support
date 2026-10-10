using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalateTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(5);

    private static TicketEscalation Escalate(Ticket ticket, int? agentId = 3, CustomerTier tier = CustomerTier.Standard) =>
        ticket.Escalate(TestSla.Policy, tier, agentId, "  Customer is losing revenue  ", "  Team Lead  ", Later);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesThePriorityByOneLevel(TicketPriority from, TicketPriority to)
    {
        var ticket = new TicketBuilder().WithPriority(from).WithSla().Build();

        Escalate(ticket);

        Assert.Equal(to, ticket.Priority);
    }

    [Fact]
    public void Escalate_RestartsTheSlaWindowFromTheEscalationTime()
    {
        // Medium (24 h) raised five hours in becomes High: 8 h from the escalation, not from creation.
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).WithSla().Build();

        Escalate(ticket);

        Assert.Equal(Later.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(Later, ticket.SlaStartedAtUtc);
        Assert.Equal(Later, ticket.SlaWindowStartUtc);
    }

    [Fact]
    public void Escalate_UsesTheCustomersTierForTheNewWindow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).WithSla(CustomerTier.Premium).Build();

        Escalate(ticket, tier: CustomerTier.Premium);

        // Critical: 4 h, halved for Premium.
        Assert.Equal(Later.AddHours(2), ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_HandsTheTicketToTheChosenAgent()
    {
        var ticket = new TicketBuilder().AssignedTo(2).WithSla().Build();

        Escalate(ticket, agentId: 5);

        Assert.Equal(5, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_WithNoEligibleAgent_LeavesItUnassigned()
    {
        var ticket = new TicketBuilder().AssignedTo(2).WithSla().Build();

        Escalate(ticket, agentId: null);

        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_RecordsEveryBeforeAndAfterValue()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(2).WithSla().Build();
        var dueBefore = ticket.DueAtUtc;

        var escalation = Escalate(ticket, agentId: 5);

        Assert.Equal(ticket.Id, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(2, escalation.FromAgentId);
        Assert.Equal(5, escalation.ToAgentId);
        Assert.Equal(dueBefore, escalation.FromDueAtUtc);
        Assert.Equal(FixedClock.DefaultNow.AddHours(24), escalation.FromDueAtUtc);
        Assert.Equal(Later.AddHours(8), escalation.ToDueAtUtc);
        Assert.Equal("Customer is losing revenue", escalation.Reason);
        Assert.Equal("Team Lead", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
        Assert.Same(escalation, Assert.Single(ticket.Escalations));
    }

    [Fact]
    public void Escalate_ATicketWithoutADueDate_RecordsNoPreviousDueDate()
    {
        var ticket = new TicketBuilder().Build();

        var escalation = Escalate(ticket);

        Assert.Null(escalation.FromDueAtUtc);
        Assert.NotNull(ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_ACriticalTicket_IsRefused()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0006").WithPriority(TicketPriority.Critical).WithSla().AssignedTo(2).Build();
        var dueBefore = ticket.DueAtUtc;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Contains("TCK-0006", exception.Message);
        Assert.Contains("Critical", exception.Message);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(dueBefore, ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_AResolvedOrClosedTicket_IsRefused(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.New, TicketPriority.High, true)]
    [InlineData(TicketStatus.InProgress, TicketPriority.Low, true)]
    [InlineData(TicketStatus.Open, TicketPriority.Critical, false)]
    [InlineData(TicketStatus.Resolved, TicketPriority.Low, false)]
    [InlineData(TicketStatus.Closed, TicketPriority.Medium, false)]
    public void CanBeEscalated_MatchesWhatEscalateAccepts(TicketStatus status, TicketPriority priority, bool expected) =>
        Assert.Equal(expected, new TicketBuilder().WithStatus(status).WithPriority(priority).Build().CanBeEscalated);

    [Theory]
    [InlineData("four")]
    [InlineData("   ab   ")]
    public void Escalate_WithATooShortReason_Throws(string reason)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ticket.Escalate(TestSla.Policy, CustomerTier.Standard, null, reason, "Team Lead", Later));
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
    }

    [Fact]
    public void StartSlaWindow_SetsTheDueDateFromThePriorityAndTier()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        ticket.StartSlaWindow(TestSla.Policy, CustomerTier.Premium, Later);

        Assert.Equal(Later.AddHours(36), ticket.DueAtUtc);
        Assert.Equal(Later, ticket.SlaStartedAtUtc);
    }

    [Fact]
    public void SlaWindowStart_WithoutARecordedStart_IsTheCreationTime() =>
        Assert.Equal(FixedClock.DefaultNow, new TicketBuilder().Build().SlaWindowStartUtc);
}
