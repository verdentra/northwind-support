using System.ComponentModel.DataAnnotations;

namespace SupportDesk.Presentation.Contracts.Tickets;

public sealed class EscalateTicketBody
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}
