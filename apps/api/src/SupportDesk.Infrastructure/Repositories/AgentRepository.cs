using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>().FirstOrDefaultAsync(a => a.Email == email, ct);

    public async Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            // Triage needs each agent's specializations to check category eligibility.
            .Include(a => a.Specializations)
            // only read agent details here, so there is no need to track changes.
            .AsNoTracking()
            .ToListAsync(ct);
}
