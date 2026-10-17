using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets;
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
/// clock, and command handlers wired to all of them.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        var customer = new Customer(
            "Contoso Ltd",
            "support@contoso.example",
            null,
            CustomerTier.Premium
            , Clock.UtcNow);

        var category = new Category(
            "General",
            requiresSpecialist: false,
            forcesCriticalPriority: false);
            

        Customers.Setup(c => c.ExistsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Customers.Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        Categories.Setup(c => c.ExistsAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Categories.Setup(c => c.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        Agents.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Agent>)Array.Empty<Agent>());

        CurrentUser.SetupGet(user => user.Subject).Returns("1");
        CurrentUser.SetupGet(user => user.Email).Returns("authenticated.agent@example.com");
        CurrentUser.SetupGet(user => user.AgentId).Returns(1);
        Agents.Setup(a => a.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Agent(
                "Authenticated Agent",
                "authenticated.agent@example.com",
                maxOpenTickets: 10,
                Clock.UtcNow));

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.CountOpenByAgentAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        Tickets.Setup(t => t.AddAsync(
                It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        Escalations.Setup(e => e.AddAsync(
                It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()))
            .Callback<TicketEscalation, CancellationToken>(
                (escalation, _) => AddedEscalation = escalation)
            .Returns(Task.CompletedTask);

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(
                    1,
                    "Contoso Ltd",
                    "support@contoso.example",
                    null,
                    CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        var triage = new TicketTriageService(
            Agents.Object,
            Tickets.Object,
            new SlaOptions());

        EscalateTicket = new EscalateTicketCommandHandler(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            Agents.Object,
            Escalations.Object,
            UnitOfWork.Object,
            Clock,
            CurrentUser.Object,
            triage,
            getTicket);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            UnitOfWork.Object,
            Clock,
            triage,
            getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(
            Tickets.Object,
            UnitOfWork.Object,
            Clock,
            getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object,
            Agents.Object,
            UnitOfWork.Object,
            Clock,
            getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ITicketEscalationRepository> Escalations { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<ICurrentUser> CurrentUser { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    public EscalateTicketCommandHandler EscalateTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>The escalation handed to the repository by the last escalation call.</summary>
    public TicketEscalation? AddedEscalation { get; private set; }

    /// <summary>Makes the ticket repository return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(
                ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(
        int id,
        string fullName = "Alex Turner",
        bool isActive = true)
    {
        var agent = new Agent(
            fullName,
            $"agent{id}@example.com",
            maxOpenTickets: 10,
            Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(
                id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            times);
}
