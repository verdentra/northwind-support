using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

public interface ITicketEscalationRepository
{
    Task AddAsync(
        TicketEscalation escalation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TicketEscalation>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken);
}
