
using System.ComponentModel.DataAnnotations;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class EscalateTicketRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string EscalatedBy { get; set; } = string.Empty;
}
