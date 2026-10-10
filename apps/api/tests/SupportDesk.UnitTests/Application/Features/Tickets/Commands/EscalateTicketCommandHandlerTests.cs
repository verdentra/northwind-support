using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;
using static SupportDesk.UnitTests.TestDoubles.Workloads;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("Customer cannot take payments");

    [Fact]
    public async Task HandleAsync_RaisesPriorityRestartsTheWindowAndSaves()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).WithSla().Build());
        context.Clock.Advance(TimeSpan.FromHours(6));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_UsesTheCustomersTier()
    {
        var context = new TicketCommandTestContext();
        context.WithCustomer(CustomerTier.Premium);
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Low).WithSla(CustomerTier.Premium).Build());
        context.Clock.Advance(TimeSpan.FromHours(1));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        // Medium: 24 h, halved for Premium.
        Assert.Equal(context.Clock.UtcNow.AddHours(12), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_KeepsAnOwnerWhoIsStillEligible()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Workload(1, open: 0), Workload(2, open: 4));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(2).WithSla().Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(2, response.Escalation.FromAgent?.Id);
        Assert.Equal(2, response.Escalation.ToAgent?.Id);
        Assert.Contains("Kept", response.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ReassignsAnOwnerWhoIsNoLongerEligible()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Workload(1, open: 3), Workload(2, open: 0, active: false), Workload(3, open: 5));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(2).WithSla().Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(1, ticket.AssignedAgentId);
        Assert.Equal("Agent 2", response.Escalation.FromAgent?.FullName);
        Assert.Equal("Agent 1", response.Escalation.ToAgent?.FullName);
        Assert.Contains("no longer eligible", response.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_LeavesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        context.WithCategory("Billing", requiresSpecialist: true);
        context.WithAgents(Workload(2, open: 0, specializations: [9]));
        var ticket = context.ExistingTicket(new TicketBuilder().InCategory(5).AssignedTo(2).WithSla().Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(response.Escalation.ToAgent);
        Assert.Contains("unassigned", response.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheRecordedHistoryRow()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Workload(4, open: 1));
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.High).WithSla().Build());
        var dueBefore = ticket.DueAtUtc;
        context.Clock.Advance(TimeSpan.FromMinutes(90));

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        var row = response.Escalation;
        Assert.Equal(TicketPriority.High, row.FromPriority);
        Assert.Equal(TicketPriority.Critical, row.ToPriority);
        Assert.Null(row.FromAgent);
        Assert.Equal(4, row.ToAgent?.Id);
        Assert.Equal(dueBefore, row.FromDueAtUtc);
        Assert.Equal(context.Clock.UtcNow.AddHours(4), row.ToDueAtUtc);
        Assert.Equal("Customer cannot take payments", row.Reason);
        Assert.Equal("Team Lead", row.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, row.EscalatedAtUtc);
        Assert.Single(ticket.Escalations);
        Assert.Equal(41, response.Ticket.Id);
    }

    [Fact]
    public async Task HandleAsync_RecordsTheSignedInAgentNotAnythingInTheRequest()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(3, "Priya Nair");
        var ticket = context.ExistingTicket(new TicketBuilder().WithSla().Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal("Priya Nair", response.Escalation.EscalatedBy);
    }

    [Fact]
    public async Task HandleAsync_WithAVeryLongAgentName_ShortensItToTheColumnLength()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(3, new string('n', 150));
        var ticket = context.ExistingTicket(new TicketBuilder().WithSla().Build());

        var response = await context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None);

        Assert.Equal(TicketEscalation.EscalatedByMaxLength, response.Escalation.EscalatedBy.Length);
    }

    [Fact]
    public async Task HandleAsync_WithoutASignedInAgent_IsRefused()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(null, null);
        var ticket = context.ExistingTicket(new TicketBuilder().WithSla().Build());

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(404, Request, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ACriticalTicket_IsRefusedWithoutLoadingAnythingElse()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).WithSla().Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        context.Agents.Verify(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()), Times.Never);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_AResolvedOrClosedTicket_IsRefused(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(status).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }
}
