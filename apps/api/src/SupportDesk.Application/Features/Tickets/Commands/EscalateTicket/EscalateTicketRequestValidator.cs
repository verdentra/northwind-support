using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Lengths are measured after trimming, the same way the ticket stores them and the web app
/// counts them, so "    a    " is not a valid five-character reason.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Give a reason for the escalation.")
            .Must(reason => reason.Trim().Length is >= TicketEscalation.ReasonMinLength and <= TicketEscalation.ReasonMaxLength)
            .WithMessage(
                $"The reason must be between {TicketEscalation.ReasonMinLength} and {TicketEscalation.ReasonMaxLength} characters.");
    }
}
