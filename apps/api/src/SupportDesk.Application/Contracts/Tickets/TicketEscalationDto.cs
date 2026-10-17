using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class TicketEscalationDto
{
    public int Id { get; init; }

    public int TicketId { get; init; }

    public TicketPriority FromPriority { get; init; }

    public TicketPriority ToPriority { get; init; }

    public int? FromAgentId { get; init; }

    public int? ToAgentId { get; init; }

    public DateTime? FromDueAtUtc { get; init; }

    public DateTime? ToDueAtUtc { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string EscalatedBy { get; init; } = string.Empty;

    public DateTime EscalatedAtUtc { get; init; }

    public static TicketEscalationDto FromDomain(TicketEscalation escalation) =>
        new()
        {
            Id = escalation.Id,
            TicketId = escalation.TicketId,
            FromPriority = escalation.FromPriority,
            ToPriority = escalation.ToPriority,
            FromAgentId = escalation.FromAgentId,
            ToAgentId = escalation.ToAgentId,
            FromDueAtUtc = escalation.FromDueAtUtc,
            ToDueAtUtc = escalation.ToDueAtUtc,
            Reason = escalation.Reason,
            EscalatedBy = escalation.EscalatedBy,
            EscalatedAtUtc = escalation.EscalatedAtUtc
        };
}
