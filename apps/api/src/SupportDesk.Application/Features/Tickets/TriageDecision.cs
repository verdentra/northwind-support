using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets;

public sealed class TriageDecision
{
    public required TicketPriority Priority { get; init; }

    public required DateTime DueAtUtc { get; init; }

    public required DateTime SlaWindowStartedAtUtc { get; init; }

    public required double SlaWindowHours { get; init; }

    public required string PriorityReason { get; init; }

    public required string SlaReason { get; init; }

    public required string AssignmentReason { get; init; }

    public Agent? AssignedAgent { get; init; }
}
