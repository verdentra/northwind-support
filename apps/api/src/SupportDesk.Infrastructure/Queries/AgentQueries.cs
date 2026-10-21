using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class AgentQueries(SupportDbContext db) : IAgentQueries
{
    public async Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.FullName)
            .Select(a => new AgentDto(
                a.Id,
                a.FullName,
                a.Email,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                (from specialization in a.Specializations
                 join category in db.Set<Category>() on specialization.CategoryId equals category.Id
                 select new CategorySummaryDto(category.Id, category.Name))
                .ToList()))
            .ToListAsync(ct);

    /// <summary>
    /// Selects the active, qualified agent with the fewest open tickets,
    /// excluding agents who have reached their workload limit.
    /// </summary>
    public async Task<AgentSelectionDto?> FindBestAvailableAsync(
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        var eligibleAgents = db.Set<Agent>()
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (requiresSpecialist)
        {
            eligibleAgents = eligibleAgents.Where(a =>
                a.Specializations.Any(s => s.CategoryId == categoryId));
        }

        return await eligibleAgents
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.MaxOpenTickets,
                OpenTicketCount = db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed)
            })
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .Select(a => new AgentSelectionDto(
                a.Id,
                a.FullName,
                a.OpenTicketCount))
            .FirstOrDefaultAsync(ct);
    }

    
    public async Task<bool> IsCurrentAgentEligibleAsync(
        int agentId,
        int categoryId,
        bool requiresSpecialist,
        CancellationToken ct)
    {
        return await db.Set<Agent>()
            .Where(agent => agent.Id == agentId && agent.IsActive)
            .Where(agent =>
                !requiresSpecialist ||
                agent.Specializations.Any(s => s.CategoryId == categoryId))
            .Where(agent =>
                db.Set<Ticket>().Count(ticket =>
                    ticket.AssignedAgentId == agent.Id &&
                    ticket.Status != TicketStatus.Resolved &&
                    ticket.Status != TicketStatus.Closed)
                <= agent.MaxOpenTickets)
            .AnyAsync(ct);
    }

}
