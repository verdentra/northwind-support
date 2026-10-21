
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search(
        [FromQuery] TicketQuery query,
        [FromServices] SearchTicketsQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(
        int id,
        [FromServices] GetTicketQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(
        CreateTicketRequest request,
        [FromServices] RaiseTicketCommandHandler handler,
        CancellationToken ct)
    {
        var created = await handler.HandleAsync(request, ct);

        return CreatedAtAction(
            nameof(Get),
            new { id = created.Id },
            created);
    }

    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        [FromServices] ChangeTicketStatusCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(
        int id,
        AssignTicketRequest request,
        [FromServices] AssignTicketCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(id, request, ct);

    /// <summary>Escalates a ticket by one priority level.</summary>
    [HttpPost("{id:int}/escalate")]
    public async Task<ActionResult<TicketDetailDto>> Escalate(
        int id,
        EscalateTicketRequest request,
        [FromServices] EscalateTicketCommandHandler handler,
        CancellationToken ct)
    {
        var agentId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(agentId, out _))
        {
            return Unauthorized();
        }

        var escalatedBy = User.FindFirstValue(
            ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? agentId;

        var authenticatedRequest = new EscalateTicketRequest
        {
            Reason = request.Reason,
            EscalatedBy = escalatedBy
        };

        var updated = await handler.HandleAsync(
            id,
            authenticatedRequest,
            ct);

        return Ok(updated);
    }

    /// <summary>Returns escalation history, newest first.</summary>
    [HttpGet("{id:int}/escalations")]
    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalations(
        int id,
        [FromServices] ITicketRepository tickets,
        [FromServices] ITicketEscalationQueries escalations,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);

        if (ticket is null)
        {
            throw new NotFoundException("Ticket", id);
        }

        return await escalations.GetByTicketIdAsync(id, ct);
    }
}
