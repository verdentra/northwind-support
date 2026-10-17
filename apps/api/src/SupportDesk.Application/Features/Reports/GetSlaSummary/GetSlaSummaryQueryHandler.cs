using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Reports;

namespace SupportDesk.Application.Features.Reports.GetSlaSummary;

public sealed class GetSlaSummaryQueryHandler
{
    private readonly ITicketQueries tickets;

    public GetSlaSummaryQueryHandler(ITicketQueries tickets)
    {
        this.tickets = tickets;
    }

    public Task<IReadOnlyList<SlaSummaryDto>> HandleAsync(
        SlaSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return tickets.GetSlaSummaryAsync(
            query,
            cancellationToken);
    }
}
