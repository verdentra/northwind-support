using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared arrangement for the ticket command tests: mocked repositories and queries, a fixed
/// clock, the development SLA policy, and command handlers wired to all of them.
/// </summary>
/// <remarks>
/// By default every customer is Standard, every category is a plain one (no specialist, no
/// forced priority) and there are no agents; tests override what matters to them.
/// </remarks>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        WithCustomer(CustomerTier.Standard);
        WithCategory("Technical");
        WithAgents();
        SignedInAs(4, "Team Lead");

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, Agents.Object, UnitOfWork.Object, Clock,
            TestSla.Policy, getTicket, NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(Tickets.Object, UnitOfWork.Object, Clock, getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object, Agents.Object, UnitOfWork.Object, Clock, getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);

        EscalateTicket = new EscalateTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, Agents.Object, UnitOfWork.Object, Clock,
            TestSla.Policy, CurrentUser.Object, getTicket, NullLogger<EscalateTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public Mock<ICurrentUser> CurrentUser { get; } = new();

    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    public EscalateTicketCommandHandler EscalateTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>Makes <see cref="ITicketRepository.GetByIdAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true)
    {
        var agent = new Agent(fullName, $"agent{id}@example.com", maxOpenTickets: 10, Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    /// <summary>Every customer looked up has this tier.</summary>
    public void WithCustomer(CustomerTier tier) =>
        Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso Ltd", "support@contoso.example", null, tier, Clock.UtcNow));

    /// <summary>Every category looked up has these rules.</summary>
    public void WithCategory(string name, bool requiresSpecialist = false, bool forcesCriticalPriority = false) =>
        Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category(name, requiresSpecialist, forcesCriticalPriority));

    /// <summary>The agents, with their workloads, that assignment chooses from.</summary>
    public void WithAgents(params AgentWorkload[] agents) =>
        Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

    /// <summary>The agent the access token says is making the request; nulls for an anonymous one.</summary>
    public void SignedInAs(int? agentId, string? fullName)
    {
        CurrentUser.Setup(u => u.AgentId).Returns(agentId);
        CurrentUser.Setup(u => u.FullName).Returns(fullName);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}
