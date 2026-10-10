using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The permanent record of one escalation: what the ticket looked like before and after, who
/// escalated it, why, and when. Part of the <see cref="Ticket"/> aggregate and only ever created
/// by <see cref="Ticket.Escalate"/>.
/// </summary>
/// <remarks>
/// Immutable: every property is set once, in the constructor, and there are no methods that
/// change it. <c>SupportDbContext</c> also refuses to save an update or delete of one.
/// </remarks>
public sealed class TicketEscalation : Entity
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;
    public const int EscalatedByMaxLength = 100;

    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason;
        EscalatedBy = escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    /// <summary>Who owned the ticket before; null when it was unassigned.</summary>
    public int? FromAgentId { get; private set; }

    /// <summary>Who owns it afterwards; null when nobody was eligible.</summary>
    public int? ToAgentId { get; private set; }

    /// <summary>The due date before; null when the ticket had none.</summary>
    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Who escalated it. Supplied by the caller until sign-in exists.</summary>
    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
