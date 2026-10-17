namespace SupportDesk.Application.Contracts.Reports;

public sealed record SlaSummaryQuery
{
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string GroupBy { get; init; } = string.Empty;
}
