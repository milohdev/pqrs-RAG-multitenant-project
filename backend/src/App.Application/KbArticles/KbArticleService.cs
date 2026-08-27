using App.Application.Abstractions;
using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace App.Application.KbArticles;

public record UpsertKbArticleDto(string Question, string Answer);

public class KbArticleService
{
    private readonly IAppDbContext _db;
    private readonly IEmbeddingService _embeddingService;

    public KbArticleService(IAppDbContext db, IEmbeddingService embeddingService)
    {
        _db = db; _embeddingService = embeddingService;
    }

    public Task<List<KnowledgeBaseArticle>> ListAsync(CancellationToken ct = default) =>
        ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles).OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);

    public async Task<KnowledgeBaseArticle> CreateAsync(UpsertKbArticleDto dto, CancellationToken ct = default)
    {
        // Se indexa con input_type "query" (no "passage"): llama-nemotron-embed-vl
        // separa demasiado los espacios query/passage (el mismo texto da ~0.59 de
        // similitud), así que se indexa y busca en el mismo espacio para matchear.
        var embedding = await _embeddingService.GetEmbeddingAsync(dto.Answer, "query", ct);
        var article = new KnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            Question = dto.Question,
            Answer = dto.Answer,
            Embedding = new Vector(embedding)
        };
        ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles).Add(article);
        await _db.SaveChangesAsync(ct);
        return article;
    }

    public async Task<KnowledgeBaseArticle?> UpdateAsync(Guid id, UpsertKbArticleDto dto, CancellationToken ct = default)
    {
        var article = await ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return null;

        article.Question = dto.Question;
        article.Answer = dto.Answer;
        article.Embedding = new Vector(await _embeddingService.GetEmbeddingAsync(dto.Answer, "query", ct));
        article.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return article;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var article = await ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null) return false;
        ((DbSet<KnowledgeBaseArticle>)_db.KnowledgeBaseArticles).Remove(article);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}