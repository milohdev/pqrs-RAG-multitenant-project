using App.Application.Abstractions;
using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Tickets;

public record CreateTicketDto(string CustomerName, string CustomerEmail, string Subject, string Description, bool EscalatedFromRag);

public class TicketService
{
    private readonly IAppDbContext _db;
    private readonly TriageService _triageService;
    private readonly ITicketNotifier _notifier;

    public TicketService(IAppDbContext db, TriageService triageService, ITicketNotifier notifier)
    {
        _db = db; _triageService = triageService; _notifier = notifier;
    }

    public async Task<Ticket> CreateAsync(CreateTicketDto dto, CancellationToken ct = default)
    {
        var triage = await _triageService.ClassifyAsync(dto.Subject, dto.Description, ct);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            CustomerName = dto.CustomerName,
            CustomerEmail = dto.CustomerEmail,
            Subject = dto.Subject,
            Description = dto.Description,
            EscalatedFromRag = dto.EscalatedFromRag,
            Type = triage.Type,
            Priority = triage.Priority,
            Sentiment = triage.Sentiment,
            Summary = triage.Summary
            // TenantId se sella solo en SaveChanges (sección 4.3)
        };

        ((DbSet<Ticket>)_db.Tickets).Add(ticket);
        await _db.SaveChangesAsync(ct);

        if (ticket.Priority == TicketPriority.Alta || ticket.Sentiment == Sentiment.Negativo)
            await _notifier.NotifyCriticalTicketAsync(ticket, ct);

        return ticket;
    }

    public Task<List<Ticket>> ListAsync(TicketStatus? status, TicketPriority? priority, CancellationToken ct = default)
    {
        var query = ((DbSet<Ticket>)_db.Tickets).AsQueryable(); // ya filtrado por tenant vía Global Query Filter
        if (status is not null) query = query.Where(t => t.Status == status);
        if (priority is not null) query = query.Where(t => t.Priority == priority);
        return query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task<Ticket?> UpdateStatusAsync(Guid id, TicketStatus newStatus, CancellationToken ct = default)
    {
        var ticket = await ((DbSet<Ticket>)_db.Tickets).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return null;
        ticket.Status = newStatus;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ticket;
    }
}