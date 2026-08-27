using App.Domain.Common;
using Pgvector;

namespace App.Domain.Entities;

public class KnowledgeBaseArticle : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public Vector Embedding { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}