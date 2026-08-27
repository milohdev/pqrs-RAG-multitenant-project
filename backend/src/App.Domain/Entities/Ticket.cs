using App.Domain.Common;

namespace App.Domain.Entities;

public enum TicketType { Peticion, Queja, Reclamo, Sugerencia }
public enum TicketStatus { Pendiente, EnProceso, Resuelto }
public enum TicketPriority { Baja, Media, Alta }
public enum Sentiment { Positivo, Neutro, Negativo }

public class Ticket : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;

    public TicketType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pendiente;
    public TicketPriority Priority { get; set; } = TicketPriority.Media; // se sobrescribe con el triaje
    public Sentiment? Sentiment { get; set; }
    public string? Summary { get; set; }

    // true si el usuario ya probó el chat RAG y respondió "No" antes de radicar.
    public bool EscalatedFromRag { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}