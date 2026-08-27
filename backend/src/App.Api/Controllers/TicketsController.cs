using App.Application.Tickets;
using App.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly TicketService _ticketService;
    public TicketsController(TicketService ticketService) => _ticketService = ticketService;

    [HttpGet]
    public Task<List<Ticket>> List([FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, CancellationToken ct)
        => _ticketService.ListAsync(status, priority, ct);

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TicketStatus status, CancellationToken ct)
    {
        var ticket = await _ticketService.UpdateStatusAsync(id, status, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }
}