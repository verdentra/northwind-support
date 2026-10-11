namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains the decisions made when a ticket is first created.
/// </summary>
public sealed class TicketTriageDto
{
    public string PriorityReason { get; set; } = string.Empty;

    public string SlaWindowReason { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;
}
