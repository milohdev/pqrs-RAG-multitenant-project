using App.Domain.Common;

namespace App.Domain.Entities;

// Métrica de "ticket desviado": el usuario preguntó al widget, la respuesta
// del RAG le sirvió y por eso nunca se creó un Ticket real. Se guarda en su
// propia tabla para no ensuciar Tickets con algo que no es un PQRS.
public class RagDeviation : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Query { get; set; } = default!;
    public Guid? MatchedArticleId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}