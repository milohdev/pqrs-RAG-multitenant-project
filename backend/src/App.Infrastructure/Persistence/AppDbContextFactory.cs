using App.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

// Factory de design-time para `dotnet ef`: permite crear migraciones y aplicar
// `database update` sin levantar el host completo (Program.cs). Es infraestructura
// de desarrollo, no se registra en DI.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=pqrsdb;Username=appuser;Password=milo123";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(dataSource, o => o.UseVector())
            .Options;

        // Durante migraciones no hay tenant en curso; el filtro de TenantId se
        // evalúa recién en consultas en runtime, no en la creación del modelo.
        return new AppDbContext(options, new DesignTimeTenantProvider());
    }

    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
    }
}