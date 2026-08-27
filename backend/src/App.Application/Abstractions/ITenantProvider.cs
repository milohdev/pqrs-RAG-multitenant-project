namespace App.Application.Abstractions;

public interface ITenantProvider
{
    Guid TenantId { get; }
}