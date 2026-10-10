using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <remarks>
    /// The Email column uses the database's case-insensitive collation and has a unique index,
    /// so this is a single index seek.
    /// </remarks>
    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>().AsNoTracking().FirstOrDefaultAsync(a => a.Email == email, ct);

    /// <remarks>
    /// One round trip: the open count is a correlated COUNT the database answers from
    /// IX_Tickets_AssignedAgentId, and the specializations come back in the same query.
    /// </remarks>
    public async Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new AgentWorkload(
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                a.Specializations.Select(s => s.CategoryId).ToList()))
            .ToListAsync(ct);
}
