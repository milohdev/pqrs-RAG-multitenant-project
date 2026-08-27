using App.Application.Tickets;
using App.Application.Widget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

[ApiController]
[Route("api/v1/widget")]
[AllowAnonymous] // la identidad del tenant se resuelve por X-Tenant-Id, no por JWT (sección 5.1)
public class WidgetController : ControllerBase
{
    private readonly RagSearchService _ragService;
    private readonly TicketService _ticketService;

    public WidgetController(RagSearchService ragService, TicketService ticketService)
    {
        _ragService = ragService; _ticketService = ticketService;
    }

    [HttpPost("rag-search")]
    public async Task<IActionResult> RagSearch(RagSearchRequest request, CancellationToken ct)
        => Ok(await _ragService.SearchAsync(request.Query, ct));

    [HttpPost("rag-search/feedback")]
    public async Task<IActionResult> RagSearchFeedback(RagFeedbackRequest request, CancellationToken ct)
    {
        await _ragService.RecordDeviationAsync(request.Query, request.MatchedArticleId, ct);
        return NoContent();
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateTicketDto dto, CancellationToken ct)
    {
        var ticket = await _ticketService.CreateAsync(dto, ct);
        return Ok(new { ticketNumber = ticket.Id, ticket.Status });
    }
}

public record RagSearchRequest(string Query);
public record RagFeedbackRequest(string Query, Guid? MatchedArticleId);