using App.Application.Abstractions;
using App.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace App.Infrastructure.RealTime;

public class SignalRTicketNotifier : ITicketNotifier
{
    private readonly IHubContext<TicketsHub> _hub;
    public SignalRTicketNotifier(IHubContext<TicketsHub> hub) => _hub = hub;

    public Task NotifyCriticalTicketAsync(Ticket ticket, CancellationToken ct = default) =>
        _hub.Clients.Group(ticket.TenantId.ToString()).SendAsync("CriticalTicket", new
        {
            ticket.Id, ticket.Subject, ticket.Priority, ticket.Sentiment
        }, ct);
}