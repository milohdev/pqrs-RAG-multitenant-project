using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tenancy;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, AppDbContext db)
    {
        // Agentes autenticados: el TenantId viene del claim del JWT.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(claim, out var tenantIdFromClaim))
                tenantContext.TenantId = tenantIdFromClaim;
        }
        // Widget público: el TenantId se resuelve a partir del WidgetApiKey.
        else if (context.Request.Path.StartsWithSegments("/api/v1/widget"))
        {
            var widgetKey = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(widgetKey))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Falta el header X-Tenant-Id." });
                return;
            }

            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.WidgetApiKey == widgetKey);

            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant no reconocido." });
                return;
            }

            tenantContext.TenantId = tenant.Id;
        }

        await _next(context);
    }
}