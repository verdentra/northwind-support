using System.Globalization;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer and triages it: decides its priority, starts its SLA window
/// and picks an owner. The rules themselves live in the domain (<see cref="TicketPriorityRules"/>,
/// <see cref="SlaPolicy"/>, <see cref="AgentAssignmentPolicy"/>); this handler only gathers what
/// they need and records the outcome.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaPolicy slaPolicy,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<RaisedTicketDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;
        var priority = TicketPriorityRules.Decide(request.RequestedPriority, category);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority.Priority,
            now);

        ticket.StartSlaWindow(slaPolicy, customer.Tier, now);

        // A ticket nobody can take is still raised; it simply waits, unassigned, with the reason.
        var assignment = AgentAssignmentPolicy.Choose(
            await agents.GetWorkloadsAsync(ct), request.CategoryId, category.Name, category.RequiresSpecialist);
        ticket.AssignTo(assignment.AgentId, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, due {DueAtUtc}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.DueAtUtc, ticket.AssignedAgentId);

        var triage = new TriageDto(
            priority.Reason,
            $"{slaPolicy.Describe(ticket.Priority, customer.Tier)} " +
            $"Due {ticket.DueAtUtc!.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC.",
            assignment.Reason);

        return RaisedTicketDto.From(await getTicket.HandleAsync(ticket.Id, ct), triage);
    }
}
