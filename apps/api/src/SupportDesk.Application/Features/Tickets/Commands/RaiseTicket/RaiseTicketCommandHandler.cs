using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket and automatically determines its priority, SLA and owner.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriageService triage,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        CreateTicketRequest request,
        CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        // BR-1: Use the requested priority, defaulting to Medium.
        // Categories configured to force Critical priority override that choice.
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;

        var priorityReason = category.ForcesCriticalPriority
            ? $"Priority is Critical because category '{category.Name}' requires it."
            : request.RequestedPriority.HasValue
                ? $"Priority is {priority}, as requested."
                : "Priority defaults to Medium because no priority was requested.";

        var now = clock.UtcNow;
        var sla = triage.CalculateSlaWindow(priority, customer.Tier, now);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now);

        ticket.SetSlaWindow(now, sla.DueAtUtc);

        var assignment = await triage.DecideAssignmentAsync(
            category,
            currentAssigneeId: null,
            ct);

        if (assignment.Agent is not null)
        {
            ticket.AssignTo(assignment.Agent.Id, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}. Priority: {Priority}, assigned agent: {AgentId}.",
            ticket.Reference,
            ticket.CustomerId,
            ticket.CategoryId,
            ticket.Priority,
            ticket.AssignedAgentId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);

        result.Triage = new TicketTriageDto
        {
            PriorityReason = priorityReason,
            SlaWindowReason = sla.Reason,
            AssignmentReason = assignment.Reason
        };

        return result;
    }
}
