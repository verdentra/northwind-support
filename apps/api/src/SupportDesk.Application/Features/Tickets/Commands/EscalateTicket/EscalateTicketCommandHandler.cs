using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Raises priority one level, recomputes the SLA window, re-evaluates ownership, and
/// records an immutable escalation history row (BR-7).
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriageService triage,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<EscalateTicketResultDto> HandleAsync(
        int ticketId,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        // Guard before computing the next priority so Critical never becomes an invalid enum.
        EnsureCanEscalate(ticket);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        var toPriority = (TicketPriority)((int)fromPriority + 1);
        var now = clock.UtcNow;
        var sla = triage.CalculateSlaWindow(toPriority, customer.Tier, now);

        ticket.Escalate(toPriority, now, sla.DueAtUtc);

        var assignment = await triage.DecideAssignmentAsync(
            category,
            currentAssigneeId: fromAgentId,
            ct);

        ticket.AssignTo(assignment.Agent?.Id, now);

        var escalation = new TicketEscalation(
            ticket.Id,
            fromPriority,
            toPriority,
            fromAgentId,
            ticket.AssignedAgentId,
            fromDueAtUtc,
            ticket.DueAtUtc,
            request.Reason,
            request.EscalatedBy,
            now);

        await escalations.AddAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}.",
            ticket.Reference,
            fromPriority,
            toPriority,
            request.EscalatedBy);

        return new EscalateTicketResultDto
        {
            Ticket = await getTicket.HandleAsync(ticket.Id, ct),
            Escalation = MapEscalation(escalation)
        };
    }

    private static void EnsureCanEscalate(Ticket ticket)
    {
        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is already Critical and cannot be escalated.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        }
    }

    private static TicketEscalationDto MapEscalation(TicketEscalation escalation) =>
        new()
        {
            Id = escalation.Id,
            TicketId = escalation.TicketId,
            FromPriority = escalation.FromPriority,
            ToPriority = escalation.ToPriority,
            FromAgentId = escalation.FromAgentId,
            ToAgentId = escalation.ToAgentId,
            FromDueAtUtc = escalation.FromDueAtUtc,
            ToDueAtUtc = escalation.ToDueAtUtc,
            Reason = escalation.Reason,
            EscalatedBy = escalation.EscalatedBy,
            EscalatedAtUtc = escalation.EscalatedAtUtc
        };
}
