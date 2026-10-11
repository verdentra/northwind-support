using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>Returns escalation history for a ticket, newest first.</summary>
public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId,
        CancellationToken ct)
    {
        _ = await tickets.GetByIdAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        var history = await escalations.GetByTicketIdAsync(ticketId, ct);

        return history.Select(Map).ToList();
    }

    private static TicketEscalationDto Map(TicketEscalation escalation) =>
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
