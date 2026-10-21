
using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// An immutable record of a ticket escalation.
/// Stores the ticket state before and after escalation.
/// </summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime? ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }

    /// <summary>
    /// Creates a permanent history entry for a completed escalation.
    /// </summary>
    public static TicketEscalation Record(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime? toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        if (ticketId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticketId));

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length is < 5 or > 500)
            throw new ArgumentException(
                "Escalation reason must be between 5 and 500 characters.",
                nameof(reason));

        if (string.IsNullOrWhiteSpace(escalatedBy) ||
            escalatedBy.Trim().Length > 100)
            throw new ArgumentException(
                "EscalatedBy must be between 1 and 100 characters.",
                nameof(escalatedBy));

        return new TicketEscalation
        {
            TicketId = ticketId,
            FromPriority = fromPriority,
            ToPriority = toPriority,
            FromAgentId = fromAgentId,
            ToAgentId = toAgentId,
            FromDueAtUtc = fromDueAtUtc,
            ToDueAtUtc = toDueAtUtc,
            Reason = reason.Trim(),
            EscalatedBy = escalatedBy.Trim(),
            EscalatedAtUtc = escalatedAtUtc
        };
    }
}
