using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(
        string reason = "Customer is blocked in production.",
        string escalatedBy = "team-lead@example.com") =>
        new() { Reason = reason, EscalatedBy = escalatedBy };

    private static Agent NewAgent(
        int id,
        string name,
        int maxOpenTickets = 5,
        bool active = true,
        int? specializationCategoryId = null)
    {
        var agent = new Agent(
            name,
            $"{name.Replace(" ", ".").ToLowerInvariant()}@example.com",
            maxOpenTickets,
            FixedClock.DefaultNow);

        EntityId.WithId(agent, id);

        if (!active)
        {
            agent.Deactivate();
        }

        if (specializationCategoryId.HasValue)
        {
            agent.AddSpecialization(specializationCategoryId.Value);
        }

        return agent;
    }

    private static Ticket OpenTicket(
        TicketPriority priority = TicketPriority.Medium,
        int? assignedAgentId = null,
        int id = 41)
    {
        return new TicketBuilder()
            .WithId(id)
            .WithPriority(priority)
            .AssignedTo(assignedAgentId)
            .WithSlaWindow(
                FixedClock.DefaultNow.AddHours(-2),
                FixedClock.DefaultNow.AddHours(10))
            .Build();
    }

    [Fact]
    public async Task HandleAsync_RaisesPriorityOneLevelAndRecomputesDueFromNow()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.Low));

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow, ticket.SlaStartedAtUtc);
        // Premium Contoso: Medium base 24h * 0.5 = 12h
        Assert.Equal(context.Clock.UtcNow.AddHours(12), ticket.DueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WritesImmutableHistoryRow()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(
            OpenTicket(TicketPriority.Medium, assignedAgentId: 7));

        var fromDue = ticket.DueAtUtc;

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request("Need a senior engineer on this.", "priya.nair"),
            CancellationToken.None);

        var escalation = context.AddedEscalation;

        Assert.NotNull(escalation);
        Assert.Equal(ticket.Id, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(7, escalation.FromAgentId);
        Assert.Equal(fromDue, escalation.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, escalation.ToDueAtUtc);
        Assert.Equal("Need a senior engineer on this.", escalation.Reason);
        Assert.Equal("priya.nair", escalation.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, escalation.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_KeepsCurrentAgentWhenStillEligible()
    {
        var context = new TicketCommandTestContext();
        var agent = NewAgent(3, "Alex Turner");

        context.Agents
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)[agent]);

        context.Tickets
            .Setup(t => t.CountOpenByAgentAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var ticket = context.ExistingTicket(
            OpenTicket(TicketPriority.Medium, assignedAgentId: 3));

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Equal(3, context.AddedEscalation!.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenCurrentAgentIsInactive()
    {
        var context = new TicketCommandTestContext();
        var inactive = NewAgent(3, "Inactive Agent", active: false);
        var replacement = NewAgent(8, "Available Agent");

        context.Agents
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)[inactive, replacement]);

        context.Tickets
            .Setup(t => t.CountOpenByAgentAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var ticket = context.ExistingTicket(
            OpenTicket(TicketPriority.Medium, assignedAgentId: 3));

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Equal(8, ticket.AssignedAgentId);
        Assert.Equal(3, context.AddedEscalation!.FromAgentId);
        Assert.Equal(8, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsConflict()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.Critical));

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                Request(),
                CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsTerminal_ThrowsConflict(
        TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(
            new TicketBuilder()
                .WithId(41)
                .WithPriority(TicketPriority.Medium)
                .WithStatus(status)
                .WithSlaWindow(
                    FixedClock.DefaultNow.AddHours(-2),
                    FixedClock.DefaultNow.AddHours(10))
                .Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                Request(),
                CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(
                999,
                Request(),
                CancellationToken.None));

        Assert.Contains("Ticket", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_StandardCustomerUsesFullSlaWindow()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Fabrikam Ltd",
                "support@fabrikam.example",
                null,
                CustomerTier.Standard,
                context.Clock.UtcNow));

        var ticket = context.ExistingTicket(OpenTicket(TicketPriority.Medium));

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        // Medium → High: base 8 hours, no premium multiplier
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentEligible_LeavesUnassigned()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(
            OpenTicket(TicketPriority.Medium, assignedAgentId: 3));

        // Current agent no longer returned / available; empty agent list.
        context.Agents
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)Array.Empty<Agent>());

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            Request(),
            CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(context.AddedEscalation!.ToAgentId);
    }
}
