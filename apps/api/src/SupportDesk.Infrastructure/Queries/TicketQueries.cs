
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
/// Ticket reads. Projects results in the database without tracking changes.
/// Customer, category and agent details are joined on the read side.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    IOptions<SlaOptions> slaOptions) : ITicketQueries
{
    private readonly double _atRiskThresholdPercent =
        slaOptions.Value.AtRiskThresholdPercent;

    /// <remarks>
    /// Applies filters in the database before counting and paging results.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        TicketQuery query,
        CancellationToken ct)
    {
        var tickets = ApplyFilters(TicketsWithLabels(), query);

        var totalCount = await tickets.CountAsync(ct);
        var now = clock.UtcNow;

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
                Customer = new CustomerSummaryDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _atRiskThresholdPercent)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(
            items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(
        int id,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
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
                    x.Customer.Id,
                    x.Customer.Name,
                    x.Customer.Email,
                    x.Customer.Phone,
                    x.Customer.Tier),
                Category = new CategoryDto(
                    x.Category.Id,
                    x.Category.Name,
                    x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _atRiskThresholdPercent)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
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
                Customer = new CustomerSummaryDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(
                    x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null
                    ? null
                    : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaStartedAtUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _atRiskThresholdPercent)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Joins each ticket with the rows that provide its display labels.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>()
            on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>()
            on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>()
            .Where(a => a.Id == ticket.AssignedAgentId)
            .DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent
        };

    // Applies the requested filters before counting and paging.
    private IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        if (query.Status.HasValue)
            source = source.Where(
                x => x.Ticket.Status == query.Status.Value);

        if (query.Priority.HasValue)
            source = source.Where(
                x => x.Ticket.Priority == query.Priority.Value);

        if (query.CategoryId.HasValue)
            source = source.Where(
                x => x.Ticket.CategoryId == query.CategoryId.Value);

        if (query.CustomerId.HasValue)
            source = source.Where(
                x => x.Ticket.CustomerId == query.CustomerId.Value);

        if (query.AssignedAgentId.HasValue)
            source = source.Where(
                x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);

        if (query.UnassignedOnly)
            source = source.Where(
                x => x.Ticket.AssignedAgentId == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.SlaStatus.HasValue)
            source = ApplySlaStatusFilter(source, query.SlaStatus.Value);

        return source;
    }

    /// <summary>
    /// Mirrors <see cref="SlaEvaluator"/> as a composable SQL predicate so filter,
    /// count and paging stay in the database (BR-8).
    /// </summary>
    private IQueryable<TicketWithLabels> ApplySlaStatusFilter(
        IQueryable<TicketWithLabels> source,
        SlaStatus slaStatus)
    {
        var now = clock.UtcNow;
        var threshold = _atRiskThresholdPercent;

        if (db.Database.IsSqlServer())
            return ApplySqlServerSlaStatusFilter(source, slaStatus, now, threshold);

        return slaStatus switch
        {
            SlaStatus.NotApplicable => source.Where(
                x => x.Ticket.DueAtUtc == null),

            SlaStatus.Met => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc != null &&
                x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

            SlaStatus.Breached => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                (
                    (x.Ticket.ResolvedAtUtc != null &&
                     x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                    (x.Ticket.ResolvedAtUtc == null &&
                     now > x.Ticket.DueAtUtc) ||
                    (x.Ticket.ResolvedAtUtc == null &&
                     now <= x.Ticket.DueAtUtc &&
                     x.Ticket.SlaStartedAtUtc != null &&
                     x.Ticket.DueAtUtc <= x.Ticket.SlaStartedAtUtc)
                )),

            SlaStatus.AtRisk => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                x.Ticket.SlaStartedAtUtc != null &&
                x.Ticket.DueAtUtc > x.Ticket.SlaStartedAtUtc &&
                (x.Ticket.DueAtUtc.Value.Ticks - now.Ticks) * 100
                    <= threshold * (x.Ticket.DueAtUtc.Value.Ticks
                        - x.Ticket.SlaStartedAtUtc.Value.Ticks)),

            SlaStatus.WithinSla => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                (
                    x.Ticket.SlaStartedAtUtc == null ||
                    (
                        x.Ticket.DueAtUtc > x.Ticket.SlaStartedAtUtc &&
                        (x.Ticket.DueAtUtc.Value.Ticks - now.Ticks) * 100
                            > threshold * (x.Ticket.DueAtUtc.Value.Ticks
                                - x.Ticket.SlaStartedAtUtc.Value.Ticks)
                    )
                )),

            _ => source
        };
    }

    private static IQueryable<TicketWithLabels> ApplySqlServerSlaStatusFilter(
        IQueryable<TicketWithLabels> source,
        SlaStatus slaStatus,
        DateTime now,
        double threshold)
    {
        return slaStatus switch
        {
            SlaStatus.AtRisk => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                x.Ticket.SlaStartedAtUtc != null &&
                x.Ticket.DueAtUtc > x.Ticket.SlaStartedAtUtc &&
                (long)EF.Functions.DateDiffMillisecond(
                    now, x.Ticket.DueAtUtc.Value) * 100
                    <= threshold * (long)EF.Functions.DateDiffMillisecond(
                        x.Ticket.SlaStartedAtUtc.Value,
                        x.Ticket.DueAtUtc.Value)),

            SlaStatus.WithinSla => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                (
                    x.Ticket.SlaStartedAtUtc == null ||
                    (
                        x.Ticket.DueAtUtc > x.Ticket.SlaStartedAtUtc &&
                        (long)EF.Functions.DateDiffMillisecond(
                            now, x.Ticket.DueAtUtc.Value) * 100
                            > threshold * (long)EF.Functions.DateDiffMillisecond(
                                x.Ticket.SlaStartedAtUtc.Value,
                                x.Ticket.DueAtUtc.Value)
                    )
                )),

            _ => ApplyCommonSlaStatusFilter(source, slaStatus, now)
        };
    }

    private static IQueryable<TicketWithLabels> ApplyCommonSlaStatusFilter(
        IQueryable<TicketWithLabels> source,
        SlaStatus slaStatus,
        DateTime now) =>
        slaStatus switch
        {
            SlaStatus.NotApplicable => source.Where(
                x => x.Ticket.DueAtUtc == null),

            SlaStatus.Met => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc != null &&
                x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

            SlaStatus.Breached => source.Where(x =>
                x.Ticket.DueAtUtc != null &&
                (
                    (x.Ticket.ResolvedAtUtc != null &&
                     x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                    (x.Ticket.ResolvedAtUtc == null &&
                     now > x.Ticket.DueAtUtc) ||
                    (x.Ticket.ResolvedAtUtc == null &&
                     now <= x.Ticket.DueAtUtc &&
                     x.Ticket.SlaStartedAtUtc != null &&
                     x.Ticket.DueAtUtc <= x.Ticket.SlaStartedAtUtc)
                )),

            _ => source
        };

    private static IQueryable<TicketWithLabels> ApplySort(
        IQueryable<TicketWithLabels> source,
        TicketQuery query)
    {
        var descending = !string.Equals(
            query.SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

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

        // Keeps paging stable when several tickets share the same sort value.
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
