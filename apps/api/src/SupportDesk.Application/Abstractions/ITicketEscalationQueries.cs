
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Abstractions;

public interface ITicketEscalationQueries
{
    Task<IReadOnlyList<TicketEscalationDto>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken ct);
}
