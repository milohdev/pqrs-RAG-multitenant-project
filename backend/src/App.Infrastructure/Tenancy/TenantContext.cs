using App.Application.Abstractions;

namespace App.Infrastructure.Tenancy;

public class TenantContext : ITenantProvider
{
    public Guid TenantId { get; set; }
}
