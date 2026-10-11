using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Filters, sorting and paging for the ticket list. Bound directly from the query string.
/// All filters are applied in SQL before counting and paging.
/// </summary>
public sealed record TicketQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    /// <summary>Matches the ticket title, its reference, or the customer name.</summary>
    public string? Search { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public int? CategoryId { get; init; }

    public int? CustomerId { get; init; }

    public int? AssignedAgentId { get; init; }

    public bool UnassignedOnly { get; init; }

    /// <summary>Derived SLA status; filtered in SQL before counting and paging.</summary>
    public SlaStatus? SlaStatus { get; init; }

    /// <summary>createdAtUtc | updatedAtUtc | dueAtUtc | priority | status.</summary>
    public string SortBy { get; init; } = "createdAtUtc";

    /// <summary>asc | desc.</summary>
    public string SortDirection { get; init; } = "desc";

    /// <summary>Page and page size clamped to something a database can serve.</summary>
    public TicketQuery Normalized()
    {
        var pageSize = PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
        var maxPage = int.MaxValue / pageSize + 1;

        return this with
        {
            Page = Math.Clamp(Page, 1, maxPage),
            PageSize = pageSize
        };
    }
}
