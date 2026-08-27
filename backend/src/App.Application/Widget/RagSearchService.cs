using App.Application.Abstractions;
using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace App.Application.Widget;

public record RagSearchResult(bool Matched, string? Answer, IReadOnlyCollection<Guid> ArticleIds);

public class RagSearchService
{
    // Calibrado con datos reales (llama-nemotron-embed-vl-1b-v2, espacio query):
    // misma temática ~0.48, no relacionada ~0.12. 0.45 deja un margen cómodo.
    private const double SimilarityThreshold = 0.45;

    private readonly IAppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly ITenantProvider _tenantProvider;

    public RagSearchService(IAppDbContext db, IEmbeddingService embeddingService,
        IChatCompletionService chatService, ITenantProvider tenantProvider)
    {
        _db = db; _embeddingService = embeddingService; _chatService = chatService; _tenantProvider = tenantProvider;
    }

    public async Task<RagSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var queryVector = new Vector(await _embeddingService.GetEmbeddingAsync(query, "query", ct));

        // El Global Query Filter de AppDbContext ya restringe por TenantId.
        // El .Where(...) explícito es defensa en profundidad (igual criterio que la guía anterior).
        var candidates = await ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles)
            .Where(a => a.TenantId == _tenantProvider.TenantId)
            .OrderBy(a => a.Embedding.CosineDistance(queryVector))
            .Take(3)
            .Select(a => new { a.Id, a.Answer, Distance = a.Embedding.CosineDistance(queryVector) })
            .ToListAsync(ct);

        var best = candidates.FirstOrDefault();
        // pgvector: CosineDistance = 1 - similitud_coseno.
        if (best is null || (1 - best.Distance) < SimilarityThreshold)
            return new RagSearchResult(false, null, Array.Empty<Guid>());

        var context = string.Join("\n---\n", candidates.Select(c => c.Answer));
        var messages = new List<ChatTurn>
        {
            new("system",
                "Respondé únicamente en base al siguiente contexto de la base de conocimiento de la empresa. " +
                "Si la respuesta no está en el contexto, decilo explícitamente en vez de inventar.\n\n" +
                $"Contexto:\n{context}"),
            new("user", query)
        };

        var answer = await _chatService.GetCompletionAsync(messages, ct);
        return new RagSearchResult(true, answer, candidates.Select(c => c.Id).ToArray());
    }

    public async Task RecordDeviationAsync(string query, Guid? matchedArticleId, CancellationToken ct = default)
    {
        // Ticket "desviado": el usuario confirmó que la respuesta del RAG le sirvió,
        // así que no se crea Ticket — solo se registra la métrica.
        ((DbSet<RagDeviation>)_db.RagDeviations).Add(new RagDeviation
        {
            Id = Guid.NewGuid(),
            Query = query,
            MatchedArticleId = matchedArticleId
        });
        await _db.SaveChangesAsync(ct);
    }
}