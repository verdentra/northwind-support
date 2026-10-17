using System.Text.Json;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public sealed class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest ValidRequest = new()
    {
        Reason = "Customer impact has increased"
    };

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public async Task HandleAsync_IncreasesPriorityByOneLevel(
        TicketPriority currentPriority,
        TicketPriority expectedPriority)
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(context, currentPriority);

        var result = await context.EscalateTicket.HandleAsync(
            ticket.Id,
            ValidRequest,
            CancellationToken.None);

        Assert.Equal(expectedPriority, ticket.Priority);
        Assert.Equal(currentPriority, result.Escalation.FromPriority);
        Assert.Equal(expectedPriority, result.Escalation.ToPriority);
        context.VerifySaved(Times.Once());
        context.Escalations.Verify(
            repository => repository.AddAsync(
                It.IsAny<TicketEscalation>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New, "Critical")]
    [InlineData(TicketPriority.Medium, TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketPriority.Medium, TicketStatus.Closed, "Closed")]
    public async Task HandleAsync_WhenEscalationIsNotAllowed_ThrowsConflictWithoutSaving(
        TicketPriority priority,
        TicketStatus status,
        string expectedMessage)
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(context, priority, status);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(
                ticket.Id,
                ValidRequest,
                CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
        context.VerifySaved(Times.Never());
        context.Escalations.Verify(
            repository => repository.AddAsync(
                It.IsAny<TicketEscalation>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsEligible_RetainsThatAgent()
    {
        var context = new TicketCommandTestContext();
        var currentAgent = AgentWithId(17, "Current Agent");
        ConfigureAgents(context, currentAgent);
        context.Tickets.Setup(repository => repository.CountOpenByAgentAsync(
                currentAgent.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var ticket = ExistingTicket(
            context,
            TicketPriority.Medium,
            assignedAgentId: currentAgent.Id);

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            ValidRequest,
            CancellationToken.None);

        Assert.Equal(currentAgent.Id, ticket.AssignedAgentId);
        Assert.Equal(currentAgent.Id, context.AddedEscalation?.ToAgentId);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentAgentIsIneligible_ReassignsToEligibleAgent()
    {
        var context = new TicketCommandTestContext();
        var currentAgent = AgentWithId(17, "Inactive Agent");
        currentAgent.Deactivate();
        var replacementAgent = AgentWithId(18, "Available Agent");
        ConfigureAgents(context, currentAgent, replacementAgent);
        context.Tickets.Setup(repository => repository.CountOpenByAgentAsync(
                replacementAgent.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var ticket = ExistingTicket(
            context,
            TicketPriority.Medium,
            assignedAgentId: currentAgent.Id);

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            ValidRequest,
            CancellationToken.None);

        Assert.Equal(replacementAgent.Id, ticket.AssignedAgentId);
        Assert.Equal(currentAgent.Id, context.AddedEscalation?.FromAgentId);
        Assert.Equal(replacementAgent.Id, context.AddedEscalation?.ToAgentId);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_LeavesTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        var ticket = ExistingTicket(
            context,
            TicketPriority.Medium,
            assignedAgentId: 17);

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            ValidRequest,
            CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(17, context.AddedEscalation?.FromAgentId);
        Assert.Null(context.AddedEscalation?.ToAgentId);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecordsBeforeAndAfterValuesAndRecalculatesDueDateFromEscalationTime()
    {
        var context = new TicketCommandTestContext();
        var currentAgent = AgentWithId(17, "Current Agent");
        ConfigureAgents(context, currentAgent);
        context.Tickets.Setup(repository => repository.CountOpenByAgentAsync(
                currentAgent.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var previousDueAtUtc = context.Clock.UtcNow.AddHours(24);
        var ticket = ExistingTicket(
            context,
            TicketPriority.Medium,
            assignedAgentId: currentAgent.Id,
            dueAtUtc: previousDueAtUtc);
        context.Clock.Advance(TimeSpan.FromMinutes(30));
        var escalationTimeUtc = context.Clock.UtcNow;

        var request = JsonSerializer.Deserialize<EscalateTicketRequest>(
            "{\"reason\":\"Customer impact has increased\",\"escalatedBy\":\"attacker@example.com\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        await context.EscalateTicket.HandleAsync(
            ticket.Id,
            request,
            CancellationToken.None);

        var escalation = Assert.IsType<TicketEscalation>(context.AddedEscalation);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(currentAgent.Id, escalation.FromAgentId);
        Assert.Equal(currentAgent.Id, escalation.ToAgentId);
        Assert.Equal(previousDueAtUtc, escalation.FromDueAtUtc);
        Assert.Equal(escalationTimeUtc.AddHours(4), escalation.ToDueAtUtc);
        Assert.Equal(request.Reason, escalation.Reason);
        Assert.Equal("authenticated.agent@example.com", escalation.EscalatedBy);
        Assert.Equal(escalationTimeUtc, escalation.EscalatedAtUtc);
        Assert.Equal(escalation.ToDueAtUtc, ticket.DueAtUtc);
        Assert.Equal(escalationTimeUtc, ticket.SlaWindowStartedAtUtc);
        Assert.Equal(4, ticket.SlaWindowHours);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Validator_WhenReasonIsTooShort_RejectsRequest()
    {
        var validator = new EscalateTicketRequestValidator();
        var request = new EscalateTicketRequest
        {
            Reason = "bad"
        };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Reason));
    }

    private static Ticket ExistingTicket(
        TicketCommandTestContext context,
        TicketPriority priority,
        TicketStatus status = TicketStatus.New,
        int? assignedAgentId = null,
        DateTime? dueAtUtc = null)
    {
        var ticket = new TicketBuilder()
            .WithPriority(priority)
            .WithStatus(status)
            .AssignedTo(assignedAgentId)
            .Build();

        if (dueAtUtc is { } due)
        {
            ticket.ApplyTriage(
                priority,
                due,
                assignedAgentId,
                context.Clock.UtcNow.AddHours(-24),
                24,
                context.Clock.UtcNow);
        }

        return context.ExistingTicket(ticket);
    }

    private static Agent AgentWithId(int id, string name)
    {
        var agent = new Agent(
            name,
            $"agent{id}@example.com",
            maxOpenTickets: 10,
            FixedClock.DefaultNow);

        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        return agent;
    }

    private static void ConfigureAgents(
        TicketCommandTestContext context,
        params Agent[] agents)
    {
        context.Agents.Setup(repository => repository.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)agents);
    }
}
