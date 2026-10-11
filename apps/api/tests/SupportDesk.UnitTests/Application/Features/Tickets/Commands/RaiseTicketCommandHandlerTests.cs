
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new(
            "Cannot log in to the portal",
            "Happens for every user since this morning.",
            1,
            3,
            priority);

    private static Agent NewAgent(
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

    private static void ConfigureAgents(
        TicketCommandTestContext context,
        params Agent[] agents)
    {
        context.Agents
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)agents);
    }

    private static void ConfigureOpenTickets(
        TicketCommandTestContext context,
        Agent agent,
        int count)
    {
        context.Tickets
            .Setup(t => t.CountOpenByAgentAsync(
                agent.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
    }

    private static RaiseTicketCommandHandler CreateHandler(
        TicketCommandTestContext context,
        SlaOptions? options = null)
    {
        var triage = new TicketTriageService(
            context.Tickets.Object,
            context.Agents.Object,
            Options.Create(options ?? TicketCommandTestContext.DefaultSlaOptions()));

        return new RaiseTicketCommandHandler(
            context.Tickets.Object,
            context.Customers.Object,
            context.Categories.Object,
            context.UnitOfWork.Object,
            context.Clock,
            triage,
            new GetTicketQueryHandler(context.TicketQueries.Object),
            NullLogger<RaiseTicketCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.High),
            CancellationToken.None);

        var created = context.AddedTicket;

        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(context.Clock.UtcNow, created.CreatedAtUtc);
        Assert.Equal(context.Clock.UtcNow, created.UpdatedAtUtc);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(),
            CancellationToken.None);

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
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(
                Request(),
                CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryForcesCriticalPriority_OverridesRequestedPriority()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Emergency",
                requiresSpecialist: false,
                forcesCriticalPriority: true));

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Low),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public async Task HandleAsync_PremiumCustomerGetsHalfSlaWindow(
        TicketPriority priority,
        int expectedHours)
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(
            Request(priority),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(
            context.Clock.UtcNow.AddHours(expectedHours),
            context.AddedTicket.DueAtUtc);
        Assert.Equal(
            context.Clock.UtcNow,
            context.AddedTicket.SlaStartedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenEligibleAgentExists_AssignsAgent()
    {
        var context = new TicketCommandTestContext();
        var agent = NewAgent("Alex Turner");

        ConfigureAgents(context, agent);
        ConfigureOpenTickets(context, agent, 0);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(agent.Id, context.AddedTicket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsAvailable_CreatesUnassignedTicketWithReason()
    {
        var context = new TicketCommandTestContext();

        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.Contains("remains unassigned", result.Triage.AssignmentReason);

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenSpecialistIsRequired_DoesNotAssignUnqualifiedAgent()
    {
        var context = new TicketCommandTestContext();

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(
                "Technical Support",
                requiresSpecialist: true,
                forcesCriticalPriority: false));

        var agent = NewAgent("Alex Turner");
        ConfigureAgents(context, agent);

        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.Contains("No active specialist", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenQualifiedSpecialistExists_AssignsSpecialist()
    {
        var context = new TicketCommandTestContext();

        var category = new Category(
            "Technical Support",
            requiresSpecialist: true,
            forcesCriticalPriority: false);

        context.Categories
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var specialist = NewAgent(
            "Specialist",
            specializationCategoryId: category.Id);

        var generalAgent = NewAgent("General Agent");

        ConfigureAgents(context, generalAgent, specialist);
        ConfigureOpenTickets(context, generalAgent, 0);
        ConfigureOpenTickets(context, specialist, 0);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(specialist.Id, context.AddedTicket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsAtMaximumCapacity_DoesNotAssignAgent()
    {
        var context = new TicketCommandTestContext();
        var agent = NewAgent("Alex Turner", maxOpenTickets: 5);

        ConfigureAgents(context, agent);
        ConfigureOpenTickets(context, agent, 5);

        var result = await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.Contains("No active agent", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_DoesNotAssignAgent()
    {
        var context = new TicketCommandTestContext();
        var agent = NewAgent("Inactive Agent", active: false);

        ConfigureAgents(context, agent);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);

        context.Tickets.Verify(
            t => t.CountOpenByAgentAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task HandleAsync_AssignsEligibleAgentWithFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        var busyAgent = NewAgent("Busy Agent");
        var availableAgent = NewAgent("Available Agent");

        ConfigureAgents(context, busyAgent, availableAgent);
        ConfigureOpenTickets(context, busyAgent, 3);
        ConfigureOpenTickets(context, availableAgent, 1);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(availableAgent.Id, context.AddedTicket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenOpenTicketCountsTie_UsesLowestAgentId()
    {
        var context = new TicketCommandTestContext();
        var firstAgent = NewAgent("First Agent");
        var secondAgent = NewAgent("Second Agent");

        ConfigureAgents(context, secondAgent, firstAgent);
        ConfigureOpenTickets(context, firstAgent, 1);
        ConfigureOpenTickets(context, secondAgent, 1);

        await context.RaiseTicket.HandleAsync(
            Request(TicketPriority.Medium),
            CancellationToken.None);

        Assert.NotNull(context.AddedTicket);

        var expectedAgentId = Math.Min(firstAgent.Id, secondAgent.Id);
        Assert.Equal(expectedAgentId, context.AddedTicket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenSlaWindowFallsBelowMinimum_UsesMinimumWindow()
    {
        var context = new TicketCommandTestContext();

        context.Customers
            .Setup(c => c.GetByIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer(
                "Contoso Ltd",
                "support@contoso.example",
                null,
                CustomerTier.Premium,
                context.Clock.UtcNow));

        var options = TicketCommandTestContext.DefaultSlaOptions();
        options.PremiumCustomerMultiplier = 0.1;

        var handler = CreateHandler(context, options);

        await handler.HandleAsync(
            Request(TicketPriority.Critical),
            CancellationToken.None);

        var ticket = context.AddedTicket;

        Assert.NotNull(ticket);
        Assert.True(ticket.SlaStartedAtUtc.HasValue);
        Assert.True(ticket.DueAtUtc.HasValue);

        Assert.Equal(
            ticket.SlaStartedAtUtc.Value.AddHours(1),
            ticket.DueAtUtc.Value);
    }
}
