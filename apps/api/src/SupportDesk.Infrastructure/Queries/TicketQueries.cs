using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets;
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
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    SlaOptions slaOptions) : ITicketQueries
{
    public async Task<IReadOnlyList<SlaSummaryDto>> GetSlaSummaryAsync(
        SlaSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return await BuildSlaSummaryQuery(query)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<SlaSummaryDto> BuildSlaSummaryQuery(
        SlaSummaryQuery query)
    {
        var now = clock.UtcNow;
        var thresholdPercent = slaOptions.AtRiskThresholdPercent;
        IQueryable<Ticket> tickets = db.Set<Ticket>().AsNoTracking();

        if (query.FromUtc.HasValue)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            tickets = tickets.Where(ticket => ticket.CreatedAtUtc < query.ToUtc.Value);
        }

        var rows = tickets.Select(ticket => new SlaSummaryRow
        {
            Priority = ticket.Priority,
            AssignedAgentId = ticket.AssignedAgentId,
            IsNotApplicable = ticket.DueAtUtc == null,
            IsMet = ticket.DueAtUtc != null &&
                    ticket.ResolvedAtUtc != null &&
                    ticket.ResolvedAtUtc <= ticket.DueAtUtc,
            IsBreached = ticket.DueAtUtc != null &&
                         ((ticket.ResolvedAtUtc != null &&
                           ticket.ResolvedAtUtc > ticket.DueAtUtc) ||
                          (ticket.ResolvedAtUtc == null &&
                           now > ticket.DueAtUtc)),
            IsAtRisk = ticket.DueAtUtc != null &&
                       ticket.ResolvedAtUtc == null &&
                       now <= ticket.DueAtUtc &&
                       ticket.SlaWindowStartedAtUtc != null &&
                       ticket.SlaWindowStartedAtUtc < ticket.DueAtUtc &&
                       EF.Functions.DateDiffMillisecond(
                           now,
                           ticket.DueAtUtc.Value) * 100.0 <=
                       EF.Functions.DateDiffMillisecond(
                           ticket.SlaWindowStartedAtUtc.Value,
                           ticket.DueAtUtc.Value) * thresholdPercent,
            IsWithinSla = ticket.DueAtUtc != null &&
                          ticket.ResolvedAtUtc == null &&
                          now <= ticket.DueAtUtc &&
                          (ticket.SlaWindowStartedAtUtc == null ||
                           ticket.SlaWindowStartedAtUtc >= ticket.DueAtUtc ||
                           EF.Functions.DateDiffMillisecond(
                               now,
                               ticket.DueAtUtc.Value) * 100.0 >
                           EF.Functions.DateDiffMillisecond(
                               ticket.SlaWindowStartedAtUtc.Value,
                               ticket.DueAtUtc.Value) * thresholdPercent),
            ResolutionHours = ticket.ResolvedAtUtc == null
                ? (double?)null
                : EF.Functions.DateDiffSecond(
                    ticket.CreatedAtUtc,
                    ticket.ResolvedAtUtc.Value) / 3_600d
        });

        IQueryable<SlaSummaryAggregation> aggregates;

        if (string.Equals(query.GroupBy, "priority", StringComparison.OrdinalIgnoreCase))
        {
            aggregates = rows
                .GroupBy(row => row.Priority)
                .Select(bucket => new SlaSummaryAggregation
                {
                    Group = bucket.Key == TicketPriority.Low ? nameof(TicketPriority.Low)
                        : bucket.Key == TicketPriority.Medium ? nameof(TicketPriority.Medium)
                        : bucket.Key == TicketPriority.High ? nameof(TicketPriority.High)
                        : nameof(TicketPriority.Critical),
                    TotalTickets = bucket.Count(),
                    Breached = bucket.Sum(row => row.IsBreached ? 1 : 0),
                    AtRisk = bucket.Sum(row => row.IsAtRisk ? 1 : 0),
                    WithinSla = bucket.Sum(row => row.IsWithinSla ? 1 : 0),
                    Met = bucket.Sum(row => row.IsMet ? 1 : 0),
                    SlaApplicableTickets = bucket.Sum(row => row.IsNotApplicable ? 0 : 1),
                    AverageResolutionHours = bucket.Average(row => row.ResolutionHours)
                });
        }
        else
        {
            aggregates =
                from row in rows
                from agent in db.Set<Agent>()
                    .AsNoTracking()
                    .Where(agent => agent.Id == row.AssignedAgentId)
                    .DefaultIfEmpty()
                group row by new
                {
                    AgentId = agent == null ? (int?)null : agent.Id,
                    AgentName = agent == null ? "Unassigned" : agent.FullName
                }
                into bucket
                select new SlaSummaryAggregation
                {
                    Group = bucket.Key.AgentName,
                    TotalTickets = bucket.Count(),
                    Breached = bucket.Sum(row => row.IsBreached ? 1 : 0),
                    AtRisk = bucket.Sum(row => row.IsAtRisk ? 1 : 0),
                    WithinSla = bucket.Sum(row => row.IsWithinSla ? 1 : 0),
                    Met = bucket.Sum(row => row.IsMet ? 1 : 0),
                    SlaApplicableTickets = bucket.Sum(row => row.IsNotApplicable ? 0 : 1),
                    AverageResolutionHours = bucket.Average(row => row.ResolutionHours)
                };
        }

        return ToSummaryDtos(aggregates);
    }

    /// <remarks>
    /// Applies filters in the database before counting, sorting, and paging tickets.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var tickets = TicketsWithLabels();
        var now = clock.UtcNow;

if (query.Status.HasValue)
{
    tickets = tickets.Where(x => x.Ticket.Status == query.Status.Value);
}

if (query.Priority.HasValue)
{
    tickets = tickets.Where(x => x.Ticket.Priority == query.Priority.Value);
}

if (query.CategoryId.HasValue)
{
    tickets = tickets.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);
}

if (query.CustomerId.HasValue)
{
    tickets = tickets.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);
}

if (query.AssignedAgentId.HasValue)
{
    tickets = tickets.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
}

if (query.UnassignedOnly)
{
    tickets = tickets.Where(x => x.Ticket.AssignedAgentId == null);
}

if (!string.IsNullOrWhiteSpace(query.Search))
{
    var search = query.Search.Trim();

    tickets = tickets.Where(x =>
        x.Ticket.Title.Contains(search) ||
        x.Ticket.Reference.Contains(search) ||
        x.Customer.Name.Contains(search));
}

if (query.SlaStatus is { } slaStatus)
{
    var thresholdPercent = slaOptions.AtRiskThresholdPercent;

    tickets = slaStatus switch
    {
        SlaStatus.NotApplicable =>
            tickets.Where(x => x.Ticket.DueAtUtc == null),

        SlaStatus.Met =>
            tickets.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc != null &&
                x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

        SlaStatus.Breached =>
            tickets.Where(x =>
                x.Ticket.DueAtUtc != null &&
                (
                    (x.Ticket.ResolvedAtUtc != null &&
                     x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc)
                    ||
                    (x.Ticket.ResolvedAtUtc == null &&
                     now > x.Ticket.DueAtUtc)
                )),

        SlaStatus.AtRisk =>
            tickets.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                x.Ticket.SlaWindowStartedAtUtc != null &&
                x.Ticket.SlaWindowStartedAtUtc < x.Ticket.DueAtUtc &&
                EF.Functions.DateDiffMillisecond(
                    now,
                    x.Ticket.DueAtUtc.Value) * 100.0
                <=
                EF.Functions.DateDiffMillisecond(
                    x.Ticket.SlaWindowStartedAtUtc.Value,
                    x.Ticket.DueAtUtc.Value) * thresholdPercent),

        SlaStatus.WithinSla =>
            tickets.Where(x =>
                x.Ticket.DueAtUtc != null &&
                x.Ticket.ResolvedAtUtc == null &&
                now <= x.Ticket.DueAtUtc &&
                (
                    x.Ticket.SlaWindowStartedAtUtc == null ||
                    x.Ticket.SlaWindowStartedAtUtc >= x.Ticket.DueAtUtc ||
                    EF.Functions.DateDiffMillisecond(
                        now,
                        x.Ticket.DueAtUtc.Value) * 100.0
                    >
                    EF.Functions.DateDiffMillisecond(
                        x.Ticket.SlaWindowStartedAtUtc.Value,
                        x.Ticket.DueAtUtc.Value) * thresholdPercent
                )),

        _ => tickets
    };
}

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
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.Ticket.SlaWindowStartedAtUtc,
                    slaOptions.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
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
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.Ticket.SlaWindowStartedAtUtc,
                    slaOptions.AtRiskThresholdPercent)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
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
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    x.Ticket.SlaWindowStartedAtUtc,
                    slaOptions.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
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

    private sealed class SlaSummaryRow
    {
        public required TicketPriority Priority { get; init; }

        public int? AssignedAgentId { get; init; }

        public required bool IsNotApplicable { get; init; }

        public required bool IsMet { get; init; }

        public required bool IsBreached { get; init; }

        public required bool IsAtRisk { get; init; }

        public required bool IsWithinSla { get; init; }

        public double? ResolutionHours { get; init; }
    }

    private sealed class SlaSummaryAggregation
    {
        public required string Group { get; init; }

        public required int TotalTickets { get; init; }

        public required int Breached { get; init; }

        public required int AtRisk { get; init; }

        public required int WithinSla { get; init; }

        public required int Met { get; init; }

        public required int SlaApplicableTickets { get; init; }

        public double? AverageResolutionHours { get; init; }
    }

    private static IQueryable<SlaSummaryDto> ToSummaryDtos(
        IQueryable<SlaSummaryAggregation> aggregates) =>
        aggregates.Select(aggregate => new SlaSummaryDto
        {
            Group = aggregate.Group,
            TotalTickets = aggregate.TotalTickets,
            Breached = aggregate.Breached,
            AtRisk = aggregate.AtRisk,
            WithinSla = aggregate.WithinSla,
            Met = aggregate.Met,
            BreachRate = aggregate.SlaApplicableTickets == 0
                ? 0m
                : (decimal)aggregate.Breached / aggregate.SlaApplicableTickets,
            AverageResolutionHours = aggregate.AverageResolutionHours
        });
}
