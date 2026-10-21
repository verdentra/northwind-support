
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    SlaCalculator slaCalculator,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        ticket.EnsureCanEscalate();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var nowUtc = clock.UtcNow;

        // Capture the original state before modifying the ticket.
        var previousPriority = ticket.Priority;
        var previousAgentId = ticket.AssignedAgentId;
        var previousDueAtUtc = ticket.DueAtUtc;

        // Escalate exactly one priority level.
        ticket.EscalatePriority(nowUtc);

        // Restart the SLA window from the escalation time.
        var newDueAtUtc = slaCalculator.CalculateDueDate(
            ticket.Priority,
            customer.Tier,
            nowUtc);

        ticket.SetDueDate(newDueAtUtc, nowUtc);

        
        // BR-7: Retain the existing owner if still eligible.
        var keepCurrentAgent = previousAgentId.HasValue &&
            await agents.IsCurrentAgentEligibleAsync(
                previousAgentId.Value,
                category.Id,
                category.RequiresSpecialist,
                ct);

        if (!keepCurrentAgent)
        {
            // BR-5: Reassign to the least-busy eligible agent.
            var selectedAgent = await agents.FindBestAvailableAsync(
                category.Id,
                category.RequiresSpecialist,
                ct);

            ticket.AssignTo(selectedAgent?.Id, nowUtc);
        }



        var escalation = TicketEscalation.Record(
            ticket.Id,
            previousPriority,
            ticket.Priority,
            previousAgentId,
            ticket.AssignedAgentId,
            previousDueAtUtc,
            ticket.DueAtUtc,
            request.Reason,
            request.EscalatedBy,
            nowUtc);

        await tickets.AddEscalationAsync(escalation, ct);

        // Save the updated ticket and history in one transaction.
        await unitOfWork.SaveChangesAsync(ct);

        return await getTicket.HandleAsync(id, ct);
    }
}
