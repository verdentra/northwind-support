using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketEscalationRepository(
    SupportDbContext context) : ITicketEscalationRepository
{
    public async Task AddAsync(
        TicketEscalation escalation,
        CancellationToken cancellationToken)
    {
        await context.Set<TicketEscalation>()
            .AddAsync(escalation, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketEscalation>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken)
    {
        return await context.Set<TicketEscalation>()
            .AsNoTracking()
            .Where(x => x.TicketId == ticketId)
            .OrderByDescending(x => x.EscalatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
