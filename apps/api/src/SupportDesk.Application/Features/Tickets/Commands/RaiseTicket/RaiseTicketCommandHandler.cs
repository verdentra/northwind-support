using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket and applies the shared automatic triage rules.
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
        var customer = await customers.GetByIdAsync(
            request.CustomerId,
            ct);

        if (customer is null)
        {
            throw new NotFoundException(
                "Customer",
                request.CustomerId);
        }

        var category = await categories.GetByIdAsync(
            request.CategoryId,
            ct);

        if (category is null)
        {
            throw new NotFoundException(
                "Category",
                request.CategoryId);
        }

        var now = clock.UtcNow;

        var decision = await triage.DecideAsync(
            customer,
            category,
            request.RequestedPriority,
            now,
            currentAgentId: null,
            ct);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority,
            now);

        ticket.ApplyTriage(
            decision.Priority,
            decision.DueAtUtc,
            decision.AssignedAgent?.Id,
            decision.SlaWindowStartedAtUtc,
            decision.SlaWindowHours,
            now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised with priority {Priority}, due at {DueAtUtc}, and agent {AgentId}.",
            ticket.Reference,
            decision.Priority,
            decision.DueAtUtc,
            decision.AssignedAgent?.Id);

        var result = await getTicket.HandleAsync(
            ticket.Id,
            ct);

        result.Triage = new TicketTriageDto
        {
            AppliedPriority = decision.Priority,
            PriorityReason = decision.PriorityReason,
            SlaWindowHours = decision.SlaWindowHours,
            SlaReason = decision.SlaReason,
            AssignmentReason = decision.AssignmentReason
        };

        return result;
    }
}
