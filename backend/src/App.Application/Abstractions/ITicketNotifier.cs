using App.Domain.Entities;

namespace App.Application.Abstractions;

public interface ITicketNotifier
{
    Task NotifyCriticalTicketAsync(Ticket ticket, CancellationToken ct = default);
}