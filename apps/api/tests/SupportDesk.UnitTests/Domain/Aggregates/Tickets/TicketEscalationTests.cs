
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using Xunit;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public sealed class TicketEscalationTests
{
    private static readonly DateTime Now =
        new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket CreateTicket(TicketPriority priority)
    {
        return Ticket.Raise(
            "TCK-TEST",
            "Test escalation ticket",
            "A valid ticket description for testing.",
            1,
            1,
            priority,
            Now);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void EscalatePriority_MovesExactlyOneLevel(
        TicketPriority original,
        TicketPriority expected)
    {
        var ticket = CreateTicket(original);

        ticket.EscalatePriority(Now.AddMinutes(5));

        Assert.Equal(expected, ticket.Priority);
    }

    [Fact]
    public void EscalatePriority_RejectsCriticalTicket()
    {
        var ticket = CreateTicket(TicketPriority.Critical);

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.EscalatePriority(Now.AddMinutes(5)));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void EscalatePriority_RejectsCompletedTicket(
        TicketStatus status)
    {
        var ticket = CreateTicket(TicketPriority.Low);
        ticket.ChangeStatus(status, Now.AddMinutes(5));

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.EscalatePriority(Now.AddMinutes(10)));
    }

    [Fact]
    public void EscalationHistory_RecordsBeforeAndAfterState()
    {
        var record = TicketEscalation.Record(
            ticketId: 10,
            fromPriority: TicketPriority.Low,
            toPriority: TicketPriority.Medium,
            fromAgentId: 2,
            toAgentId: 3,
            fromDueAtUtc: Now.AddHours(72),
            toDueAtUtc: Now.AddHours(24),
            reason: "Customer reported increased urgency.",
            escalatedBy: "Test Agent",
            escalatedAtUtc: Now);

        Assert.Equal(10, record.TicketId);
        Assert.Equal(TicketPriority.Low, record.FromPriority);
        Assert.Equal(TicketPriority.Medium, record.ToPriority);
        Assert.Equal(2, record.FromAgentId);
        Assert.Equal(3, record.ToAgentId);
        Assert.Equal("Test Agent", record.EscalatedBy);
        Assert.Equal(Now, record.EscalatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("bad")]
    public void EscalationHistory_RejectsInvalidReason(string reason)
    {
        Assert.Throws<ArgumentException>(() =>
            TicketEscalation.Record(
                10,
                TicketPriority.Low,
                TicketPriority.Medium,
                null,
                null,
                null,
                null,
                reason,
                "Test Agent",
                Now));
    }
}
