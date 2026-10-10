using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket: one priority level up, a new due date from now, the owner re-checked
/// against the same assignment rules a new ticket uses, and an immutable history record. The
/// signed-in agent is recorded as the one who escalated it.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaPolicy slaPolicy,
    ICurrentUser currentUser,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="UnauthorizedException">No signed-in agent.</exception>
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved, closed or already Critical.</exception>
    public async Task<EscalateTicketResponse> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var escalatedBy = ActorName(currentUser.FullName ?? throw new UnauthorizedException("You are not signed in."));

        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Refuse before loading anything else.
        ticket.EnsureCanBeEscalated();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var workloads = await agents.GetWorkloadsAsync(ct);
        var assignment = AgentAssignmentPolicy.Choose(
            workloads, ticket.CategoryId, category.Name, category.RequiresSpecialist, ticket.AssignedAgentId);

        var escalation = ticket.Escalate(
            slaPolicy, customer.Tier, assignment.AgentId, request.Reason, escalatedBy, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}; agent {FromAgentId} -> {ToAgentId}.",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy,
            escalation.FromAgentId, escalation.ToAgentId);

        var names = workloads.ToDictionary(a => a.AgentId, a => a.FullName);

        var recorded = new TicketEscalationDto(
            escalation.Id,
            ticket.Id,
            escalation.FromPriority,
            escalation.ToPriority,
            Summary(escalation.FromAgentId, names),
            Summary(escalation.ToAgentId, names),
            escalation.FromDueAtUtc,
            escalation.ToDueAtUtc,
            escalation.Reason,
            escalation.EscalatedBy,
            escalation.EscalatedAtUtc);

        return new EscalateTicketResponse(await getTicket.HandleAsync(id, ct), recorded, assignment.Reason);
    }

    /// <summary>
    /// Agent names may be up to 200 characters; the history column holds 100, so a very long name
    /// is shortened rather than failing the escalation.
    /// </summary>
    private static string ActorName(string fullName)
    {
        var name = fullName.Trim();

        return name.Length <= TicketEscalation.EscalatedByMaxLength
            ? name
            : name[..TicketEscalation.EscalatedByMaxLength];
    }

    private static AgentSummaryDto? Summary(int? agentId, IReadOnlyDictionary<int, string> names) =>
        agentId is { } id && names.TryGetValue(id, out var name) ? new AgentSummaryDto(id, name) : null;
}
