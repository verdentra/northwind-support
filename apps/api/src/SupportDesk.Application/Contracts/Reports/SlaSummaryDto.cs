namespace SupportDesk.Application.Contracts.Reports;

public sealed class SlaSummaryDto
{
    public string Group { get; init; } = string.Empty;

    public int TotalTickets { get; init; }

    public int Breached { get; init; }

    public int AtRisk { get; init; }

    public int WithinSla { get; init; }

    public int Met { get; init; }

    public decimal BreachRate { get; init; }

    public double? AverageResolutionHours { get; init; }
}
