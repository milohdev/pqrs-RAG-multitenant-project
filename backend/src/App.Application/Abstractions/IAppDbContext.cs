using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<RagDeviation> RagDeviations { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}