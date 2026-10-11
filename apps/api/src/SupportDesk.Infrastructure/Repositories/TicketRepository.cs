using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class TicketRepository(SupportDbContext db) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Ticket>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        var lastId = await db.Set<Ticket>()
            .AsNoTracking()
            .OrderByDescending(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return $"TCK-{lastId + 1:D4}";
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct) =>
        await db.Set<Ticket>().AddAsync(ticket, ct);

    public Task<int> CountOpenByAgentAsync(int agentId, CancellationToken ct) =>
        db.Set<Ticket>()
            .CountAsync(
                t => t.AssignedAgentId == agentId
                    && t.Status != TicketStatus.Resolved
                    && t.Status != TicketStatus.Closed,
                ct);
}
