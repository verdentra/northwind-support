namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Updated ticket plus the immutable escalation row recorded for the change.</summary>
public sealed class EscalateTicketResultDto
{
    public required TicketDetailDto Ticket { get; init; }

    public required TicketEscalationDto Escalation { get; init; }
}
