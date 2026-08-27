namespace App.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;

    // CSV simple ("acme.com,app.acme.com"). No se separa en tabla aparte
    // para no sumar una entidad más de la que el enunciado no habla.
    public string AllowedDomains { get; set; } = default!;

    // Token público que el <script> del widget manda en el header X-Tenant-Id.
    // Deliberadamente NO es el Id interno (ver sección 5.1).
    public string WidgetApiKey { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}