
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class TicketEscalationQueries(SupportDbContext db)
    : ITicketEscalationQueries
{
    public async Task<IReadOnlyList<TicketEscalationDto>> GetByTicketIdAsync(
        int ticketId,
        CancellationToken ct)
    {
        return await db.TicketEscalations
            .AsNoTracking()
            .Where(x => x.TicketId == ticketId)
            .OrderByDescending(x => x.EscalatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new TicketEscalationDto(
                x.Id,
                x.TicketId,
                x.FromPriority,
                x.ToPriority,
                x.FromAgentId,
                x.ToAgentId,
                x.FromDueAtUtc,
                x.ToDueAtUtc,
                x.Reason,
                x.EscalatedBy,
                x.EscalatedAtUtc))
            .ToListAsync(ct);
    }
}
