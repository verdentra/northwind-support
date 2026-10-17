using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    ITicketEscalationRepository escalations)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(
        int ticketId,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);

        if (ticket is null)
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        var history = await escalations.GetByTicketIdAsync(ticketId, ct);

        return history
            .Select(TicketEscalationDto.FromDomain)
            .ToList();
    }
}
