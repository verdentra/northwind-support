using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    SlaCalculator slaCalculator,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var nowUtc = clock.UtcNow;

        // BR-1: Apply the requested priority, with category overrides.
        var appliedPriority = TicketPriorityPolicy.Determine(
            request.RequestedPriority, category);

        var priorityReason = category.ForcesCriticalPriority
            ? "The selected category requires Critical priority."
            : request.RequestedPriority.HasValue
                ? "The customer's requested priority was applied."
                : "No priority was requested, so Medium was applied.";

        // BR-2 and BR-3: Calculate the SLA window and deadline.
        var slaWindowHours = slaCalculator.CalculateWindowHours(
            appliedPriority, customer.Tier);

        var dueAtUtc = slaCalculator.CalculateDueDate(
            appliedPriority, customer.Tier, nowUtc);

        var slaReason = customer.Tier == CustomerTier.Premium
            ? "The Premium customer SLA multiplier and minimum window were applied."
            : "The configured SLA window for the applied priority was used.";

        // BR-4 and BR-5: Find the least-busy eligible agent.
        var selectedAgent = await agents.FindBestAvailableAsync(
            category.Id,
            category.RequiresSpecialist,
            ct);

        var assignmentReason = selectedAgent is null
            ? "No active, qualified agent currently has available capacity."
            : category.RequiresSpecialist
                ? $"Assigned to {selectedAgent.FullName}, the least-busy eligible specialist."
                : $"Assigned to {selectedAgent.FullName}, the least-busy eligible agent.";

        // Create the ticket using the calculated triage decisions.
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            customer.Id,
            category.Id,
            appliedPriority,
            nowUtc);

        ticket.SetDueDate(dueAtUtc, nowUtc);

        if (selectedAgent is not null)
        {
            ticket.AssignTo(selectedAgent.Id, nowUtc);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        var result = await getTicket.HandleAsync(ticket.Id, ct);

        result.Triage = new TicketTriageDto(
            appliedPriority,
            priorityReason,
            slaWindowHours,
            slaReason,
            assignmentReason);

        return result;
    }
}
