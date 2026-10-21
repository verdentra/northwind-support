using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportDesk.Domain.Aggregates.Customers.Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }
    
    [Fact]
    public async Task HandleAsync_AppliesPremiumSlaAndReturnsTriageExplanation()
    {
        var context = new TicketCommandTestContext();

        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);

        // High priority has an 8-hour base SLA.
        // Premium customers receive half: 4 hours.
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(4),
            created.DueAtUtc);

        // No eligible agent was configured in the test context.
        Assert.Null(created.AssignedAgentId);

        // Verify the API response explains the triage decision.
        Assert.NotNull(result.Triage);
        Assert.Equal(TicketPriority.High, result.Triage.AppliedPriority);
        Assert.Equal(4, result.Triage.SlaWindowHours);
        Assert.False(string.IsNullOrWhiteSpace(result.Triage.PriorityReason));
        Assert.False(string.IsNullOrWhiteSpace(result.Triage.SlaReason));
        Assert.False(string.IsNullOrWhiteSpace(result.Triage.AssignmentReason));

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ForcedCriticalPriority_AssignsEligibleAgent()
    {
        var context = new TicketCommandTestContext();

        // This category requires both Critical priority and a specialist.
        var category = new Category(
            "Security",
            requiresSpecialist: true,
            forcesCriticalPriority: true);

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Simulate the agent selection query finding an eligible specialist.
        context.AgentQueries
            .Setup(a => a.FindBestAvailableAsync(
                It.IsAny<int>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSelectionDto(
                7,
                "Priya Nair",
                2));

        // The customer requests Low, but the category must override it.
        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Low),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);

        // BR-1: Category rule overrides requested priority.
        Assert.Equal(TicketPriority.Critical, created.Priority);

        // BR-2/3: Premium Critical SLA = 4 hours × 0.5 = 2 hours.
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(2),
            created.DueAtUtc);

        // BR-4/5: The selected specialist receives the ticket.
        Assert.Equal(7, created.AssignedAgentId);

        // The response explains the applied decisions.
        Assert.NotNull(result.Triage);
        Assert.Equal(TicketPriority.Critical, result.Triage.AppliedPriority);
        Assert.Equal(2, result.Triage.SlaWindowHours);
        Assert.Contains("Critical", result.Triage.PriorityReason);
        Assert.Contains("Priya Nair", result.Triage.AssignmentReason);

        context.VerifySaved(Times.Once());
    }


}
