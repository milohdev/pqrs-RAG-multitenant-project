using App.Application.Abstractions;
using App.Domain.Common;
using App.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
        : base(options) => _tenantProvider = tenantProvider;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<RagDeviation> RagDeviations => Set<RagDeviation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        // Tenant NO implementa ITenantEntity: es la tabla dueña del aislamiento, no una tabla aislada.
        modelBuilder.Entity<Tenant>(e => e.HasIndex(t => t.WidgetApiKey).IsUnique());

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasQueryFilter(u => u.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<KnowledgeBaseArticle>(e =>
        {
            e.Property(a => a.Embedding).HasColumnType("vector(2000)"); // 2000 dims: límite de índice pgvector; modelo de 2048 dims truncado a 2000
            e.HasIndex(a => a.TenantId);
            e.HasQueryFilter(a => a.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasIndex(t => new { t.TenantId, t.Status });   // pedido explícito del enunciado
            e.HasIndex(t => new { t.TenantId, t.Priority });  // pedido explícito del enunciado
            e.HasQueryFilter(t => t.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<RagDeviation>(e =>
        {
            e.HasIndex(d => d.TenantId);
            e.HasQueryFilter(d => d.TenantId == _tenantProvider.TenantId);
        });

        base.OnModelCreating(modelBuilder);
    }

    // Sella el TenantId automáticamente en cada Insert.
    public override int SaveChanges() { StampTenantId(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        StampTenantId();
        return base.SaveChangesAsync(ct);
    }

    private void StampTenantId()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            if (entry.State == EntityState.Added)
                entry.Entity.TenantId = _tenantProvider.TenantId;
    }
}