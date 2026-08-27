using System.Net.Http.Headers;
using System.Text;
using App.Application.Abstractions;
using App.Application.Auth;
using App.Application.KbArticles;
using App.Application.Tickets;
using App.Application.Widget;
using App.Domain.Entities;
using App.Infrastructure.Ai;
using App.Infrastructure.Auth;
using App.Infrastructure.Cors;
using App.Infrastructure.Persistence;
using App.Infrastructure.RealTime;
using App.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS dinámico (sección 6) ---
builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();
builder.Services.AddCors();

// --- Persistencia + pgvector ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), o => o.UseVector()));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// --- Tenancy (sección 5) ---
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());

// --- Auth JWT para agentes (sección 5.3) ---
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // SignalR: los WebSockets no mandan Authorization header, va por query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator>(_ => new JwtTokenGenerator(jwtSecret));

// --- IA / NVIDIA NIM (sección 7.1) ---
// Sin NVIDIA_API_KEY real se registran stubs deterministas (en cualquier entorno)
// para poder probar el flujo RAG y el aislamiento multi-tenant end-to-end con
// `docker compose up`. Con una key válida en el .env se usan los servicios NVIDIA.
if (!HasRealNvidiaKey(builder.Configuration))
{
    builder.Services.AddScoped<IEmbeddingService, StubEmbeddingService>();
    builder.Services.AddScoped<IChatCompletionService, StubChatCompletionService>();
}
else
{
    builder.Services.Configure<NvidiaOptions>(builder.Configuration.GetSection("Nvidia"));
    builder.Services.AddHttpClient<IEmbeddingService, NvidiaEmbeddingService>(ConfigureNvidiaClient);
    builder.Services.AddHttpClient<IChatCompletionService, NvidiaChatCompletionService>(ConfigureNvidiaClient);
}

// --- Tiempo real (sección 8) ---
builder.Services.AddSignalR();
builder.Services.AddScoped<ITicketNotifier, SignalRTicketNotifier>();

// --- Casos de uso (Application) ---
builder.Services.AddScoped<RagSearchService>();
builder.Services.AddScoped<TriageService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<KbArticleService>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();          // sirve wwwroot/pqrs-widget.js
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TicketsHub>("/hubs/tickets");

// Aplica migraciones pendientes al arranque: así `docker compose up` deja todo
// listo sin pasos manuales (la guía no especifica el mecanismo de migración en deploy).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed de datos de demostración (idempotente): tenants, agentes y un artículo
    // de KB, para que `docker compose up` deje el proyecto utilizable de inmediato.
    await SeedDemoDataAsync(scope.ServiceProvider);
}

app.Run();

static void ConfigureNvidiaClient(IServiceProvider sp, HttpClient client)
{
    var opts = sp.GetRequiredService<IOptions<NvidiaOptions>>().Value;
    // BaseAddress sin '/' final hace que HttpClient descarte el último segmento
    // ("/v1") al combinar con una ruta relativa -> se perdería el /v1.
    var baseUrl = opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
}

static bool HasRealNvidiaKey(IConfiguration config)
{
    var key = config["Nvidia:ApiKey"];
    return !string.IsNullOrWhiteSpace(key)
        && key != "tu-api-key-de-nvidia"
        && key != "TU_API_KEY_ACA";
}

// Seed de datos de demostración (idempotente): tenants, agentes y un artículo
// de KB, para que `docker compose up` deje el proyecto utilizable de inmediato.
// El TenantId se setea antes de cada operación para que el Global Query Filter
// haga su trabajo; no se usa IgnoreQueryFilters() (la única excepción es el login, 5.3).
static async Task SeedDemoDataAsync(IServiceProvider sp)
{
    var db = sp.GetRequiredService<AppDbContext>();
    var tenantContext = sp.GetRequiredService<TenantContext>();
    var hasher = sp.GetRequiredService<IPasswordHasher>();
    var kbService = sp.GetRequiredService<KbArticleService>();

    var acme = await db.Tenants.FirstOrDefaultAsync(t => t.WidgetApiKey == "acme-widget-key");
    Tenant? beta = null;
    if (acme is null)
    {
        acme = new Tenant { Id = Guid.NewGuid(), Name = "Acme S.A.", WidgetApiKey = "acme-widget-key", AllowedDomains = "localhost,acme.com" };
        beta = new Tenant { Id = Guid.NewGuid(), Name = "Beta Corp", WidgetApiKey = "beta-widget-key", AllowedDomains = "localhost,betacorp.com" };
        db.Tenants.AddRange(acme, beta);
        await db.SaveChangesAsync();
    }

    tenantContext.TenantId = acme.Id;
    if (!await db.Users.AnyAsync(u => u.Email == "agente@acme.com"))
    {
        db.Users.Add(new User { Id = Guid.NewGuid(), TenantId = acme.Id, FullName = "Agente Acme", Email = "agente@acme.com", PasswordHash = hasher.Hash("Password123!"), Role = UserRole.Agente });
        await db.SaveChangesAsync();
    }

    beta ??= await db.Tenants.FirstOrDefaultAsync(t => t.WidgetApiKey == "beta-widget-key");
    tenantContext.TenantId = beta.Id;
    if (!await db.Users.AnyAsync(u => u.Email == "agente@betacorp.com"))
    {
        db.Users.Add(new User { Id = Guid.NewGuid(), TenantId = beta.Id, FullName = "Agente Beta", Email = "agente@betacorp.com", PasswordHash = hasher.Hash("Password123!"), Role = UserRole.Agente });
        await db.SaveChangesAsync();
    }

    tenantContext.TenantId = acme.Id;
    if (!await db.KnowledgeBaseArticles.AnyAsync())
    {
        await kbService.CreateAsync(new UpsertKbArticleDto(
            "¿Cuál es el horario de atención?",
            "Atendemos de lunes a viernes de 8am a 6pm."), CancellationToken.None);
    }
}