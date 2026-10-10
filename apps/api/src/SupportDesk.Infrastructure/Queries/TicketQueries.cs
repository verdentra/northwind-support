using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy slaPolicy) : ITicketQueries
{
    /// <remarks>
    /// Filters, then counts, sorts and pages - all in one SQL statement each for the count and
    /// the page. Only the SLA status filter is applied so far; the other filter values bound into
    /// <paramref name="query"/> (search, status, priority, category, customer, agent,
    /// unassigned-only) are Task 1's server-side filtering, and each one is one more line in
    /// <see cref="ApplyFilters"/>.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = Now();
        var threshold = slaPolicy.AtRiskThresholdPercent;

        var tickets = TicketsWithLabels(ApplyFilters(db.Set<Ticket>().AsNoTracking(), query, now));

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc ?? x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, threshold)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = Now();
        var threshold = slaPolicy.AtRiskThresholdPercent;

        return TicketsWithLabels(db.Set<Ticket>().AsNoTracking())
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc ?? x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, threshold),
                CanBeEscalated = Ticket.IsEscalatable(x.Ticket.Status, x.Ticket.Priority)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = Now();
        var threshold = slaPolicy.AtRiskThresholdPercent;

        return await TicketsWithLabels(db.Set<Ticket>().AsNoTracking())
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc ?? x.Ticket.CreatedAtUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, threshold)
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AsNoTracking().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        // Served by IX_TicketEscalations_TicketId_EscalatedAtUtc, which is already in this order.
        return await (
                from escalation in db.Set<TicketEscalation>().AsNoTracking()
                where escalation.TicketId == ticketId
                from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
                from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
                orderby escalation.EscalatedAtUtc descending, escalation.Id descending
                select new TicketEscalationDto(
                    escalation.Id,
                    escalation.TicketId,
                    escalation.FromPriority,
                    escalation.ToPriority,
                    fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                    toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                    escalation.FromDueAtUtc,
                    escalation.ToDueAtUtc,
                    escalation.Reason,
                    escalation.EscalatedBy,
                    escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every filter the list supports, applied to the tickets before they are joined, counted,
    /// sorted or paged, so the database does all of it. A new filter is one more <c>if</c> here.
    /// </summary>
    private IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> tickets, TicketQuery query, DateTime now)
    {
        if (query.SlaStatus is { } slaStatus)
        {
            tickets = tickets.Where(SlaStatusFilter.Matching(slaStatus, now, slaPolicy.AtRiskThresholdPercent));
        }

        return tickets;
    }

    /// <summary>
    /// The current time, cut to whole milliseconds: the precision of the datetime2(3) columns it
    /// is compared with, so SQL Server and the projected SLA status see exactly the same instant.
    /// </summary>
    private DateTime Now()
    {
        var now = clock.UtcNow;

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    /// <summary>
    /// The given tickets with the rows that label them. Filter the tickets first, then sort and
    /// project this.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels(IQueryable<Ticket> tickets) =>
        from ticket in tickets
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
