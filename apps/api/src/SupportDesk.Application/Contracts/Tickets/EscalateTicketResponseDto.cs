namespace SupportDesk.Application.Contracts.Tickets;

public sealed class EscalateTicketResponseDto
{
    public required TicketDetailDto Ticket { get; init; }

    public required TicketEscalationDto Escalation { get; init; }
}
