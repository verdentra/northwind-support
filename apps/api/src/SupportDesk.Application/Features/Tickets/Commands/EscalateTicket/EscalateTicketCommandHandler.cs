using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    ITicketEscalationRepository escalations,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser,
    TicketTriageService triage,
    GetTicketQueryHandler getTicket)
{
    public async Task<EscalateTicketResponseDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (currentUser.AgentId is not { } actorAgentId ||
            string.IsNullOrWhiteSpace(currentUser.Email))
        {
            throw new UnauthorizedException("The authenticated user is not a valid agent.");
        }

        var actorAgent = await agents.GetByIdAsync(actorAgentId, ct);

        if (actorAgent is null ||
            !string.Equals(actorAgent.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException("The authenticated user is not a valid agent.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is already Critical and cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Resolved)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is Resolved and cannot be escalated.");
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is Closed and cannot be escalated.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var escalationTimeUtc = clock.UtcNow;
        var previousPriority = ticket.Priority;
        var previousAgentId = ticket.AssignedAgentId;
        var previousDueAtUtc = ticket.DueAtUtc;
        var nextPriority = GetNextPriority(previousPriority);

        var decision = await triage.DecideAsync(
            customer,
            category,
            nextPriority,
            escalationTimeUtc,
            ticket.AssignedAgentId,
            ct);

        ticket.ApplyTriage(
            decision.Priority,
            decision.DueAtUtc,
            decision.AssignedAgent?.Id,
            decision.SlaWindowStartedAtUtc,
            decision.SlaWindowHours,
            escalationTimeUtc);

        var escalation = TicketEscalation.Record(
            ticket.Id,
            previousPriority,
            decision.Priority,
            previousAgentId,
            decision.AssignedAgent?.Id,
            previousDueAtUtc,
            decision.DueAtUtc,
            request.Reason,
            currentUser.Email,
            escalationTimeUtc);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var updatedTicket = await getTicket.HandleAsync(id, ct);

        return new EscalateTicketResponseDto
        {
            Ticket = updatedTicket,
            Escalation = TicketEscalationDto.FromDomain(escalation)
        };
    }

    private static TicketPriority GetNextPriority(TicketPriority priority) =>
        priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => throw new ConflictException(
                "A Critical ticket cannot be escalated.")
        };
}
