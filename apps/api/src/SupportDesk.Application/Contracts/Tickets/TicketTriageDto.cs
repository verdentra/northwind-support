using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class TicketTriageDto
{
    public TicketPriority AppliedPriority { get; init; }

    public string PriorityReason { get; init; } = string.Empty;

    public double SlaWindowHours { get; init; }

    public string SlaReason { get; init; } = string.Empty;

    public string AssignmentReason { get; init; } = string.Empty;
}
