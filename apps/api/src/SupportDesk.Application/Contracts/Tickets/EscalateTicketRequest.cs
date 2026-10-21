
using System.ComponentModel.DataAnnotations;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed class EscalateTicketRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "Reason cannot be blank.")]
    public string Reason { get; init; } = string.Empty;
    
    public string EscalatedBy { get; set; } = string.Empty;

}
