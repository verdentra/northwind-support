namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Why a new ticket got the priority, due date and owner it did.</summary>
/// <param name="PriorityReason">E.g. "Security tickets are always Critical (Low was requested)."</param>
/// <param name="SlaReason">The window applied and how it was worked out.</param>
/// <param name="AssignmentReason">Who was chosen and why, or why nobody was.</param>
public sealed record TriageDto(string PriorityReason, string SlaReason, string AssignmentReason);

/// <summary>
/// Response of POST /api/tickets: the created ticket - the same fields GET /api/tickets/{id}
/// returns - plus the triage decision.
/// </summary>
public sealed class RaisedTicketDto : TicketDetailDto
{
    public TriageDto Triage { get; set; } = null!;

    public static RaisedTicketDto From(TicketDetailDto ticket, TriageDto triage) => new()
    {
        Id = ticket.Id,
        Reference = ticket.Reference,
        Title = ticket.Title,
        Description = ticket.Description,
        Status = ticket.Status,
        Priority = ticket.Priority,
        Customer = ticket.Customer,
        Category = ticket.Category,
        AssignedAgent = ticket.AssignedAgent,
        CreatedAtUtc = ticket.CreatedAtUtc,
        UpdatedAtUtc = ticket.UpdatedAtUtc,
        DueAtUtc = ticket.DueAtUtc,
        ResolvedAtUtc = ticket.ResolvedAtUtc,
        SlaStatus = ticket.SlaStatus,
        CanBeEscalated = ticket.CanBeEscalated,
        Triage = triage
    };
}
