using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure.Cors;

public class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    public DynamicCorsPolicyProvider(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrEmpty(origin)) return BuildPolicy(Array.Empty<string>());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var host = new Uri(origin).Host;

        // Se trae solo la columna de dominios (pocas filas, se compara en memoria
        // para no depender de que EF traduzca Split() a SQL).
        var domainLists = await db.Tenants.Select(t => t.AllowedDomains).ToListAsync();
        var allowed = domainLists.Any(list =>
            list.Split(',', StringSplitOptions.TrimEntries)
                .Contains(host, StringComparer.OrdinalIgnoreCase));

        return allowed ? BuildPolicy(new[] { origin }) : BuildPolicy(Array.Empty<string>());
    }

    private static CorsPolicy BuildPolicy(string[] origins) =>
        new CorsPolicyBuilder()
            .WithOrigins(origins)
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .Build();
}