using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;
using static SupportDesk.UnitTests.TestDoubles.Workloads;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <remarks>
/// The rules themselves are covered by the domain tests (SLA policy, priority rules, assignment
/// policy); these tests cover that raising a ticket applies them and reports the outcome.
/// </remarks>
public class RaiseTicketCommandHandlerTests
{
    private const int CategoryId = 3;

    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, CategoryId, priority);

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

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
        Assert.Contains("defaults to Medium", result.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_InACategoryThatForcesCritical_IgnoresTheRequestedPriority()
    {
        var context = new TicketCommandTestContext();
        context.WithCategory("Security", requiresSpecialist: true, forcesCriticalPriority: true);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Contains("Security", result.Triage.PriorityReason);
        Assert.Contains("Low was requested", result.Triage.PriorityReason);
    }

    [Theory]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    public async Task HandleAsync_SetsTheDueDateFromTheFinalPriorityAndTier(
        TicketPriority priority, CustomerTier tier, int hours)
    {
        var context = new TicketCommandTestContext();
        context.WithCustomer(tier);

        var result = await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(hours), context.AddedTicket!.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, context.AddedTicket.SlaStartedAtUtc);
        Assert.Contains(priority.ToString(), result.Triage.SlaReason);
    }

    [Fact]
    public async Task HandleAsync_ForcedCriticalForAPremiumCustomer_GetsTheHalvedCriticalWindow()
    {
        var context = new TicketCommandTestContext();
        context.WithCustomer(CustomerTier.Premium);
        context.WithCategory("Outage", requiresSpecialist: true, forcesCriticalPriority: true);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(2), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheLeastLoadedEligibleAgent()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Workload(1, open: 5), Workload(2, open: 2), Workload(3, open: 0, active: false));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
        Assert.Contains("Agent 2", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_OnlyAssignsSpecialistsWhenTheCategoryRequiresOne()
    {
        var context = new TicketCommandTestContext();
        context.WithCategory("Billing", requiresSpecialist: true);
        context.WithAgents(Workload(1, open: 0), Workload(2, open: 4, specializations: [CategoryId]));

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_StillRaisesTheTicketUnassignedWithAReason()
    {
        var context = new TicketCommandTestContext();
        context.WithCategory("Billing", requiresSpecialist: true);
        context.WithAgents(Workload(1, open: 6, max: 6, specializations: [CategoryId]));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(context.AddedTicket.DueAtUtc);
        Assert.Contains("unassigned", result.Triage.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheSavedTicketWithTheTriage()
    {
        var context = new TicketCommandTestContext();

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(41, result.Id);
        Assert.Equal("TCK-0041", result.Reference);
        Assert.NotNull(result.Triage);
        Assert.False(string.IsNullOrWhiteSpace(result.Triage.SlaReason));
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupportDesk.Domain.Aggregates.Categories.Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
