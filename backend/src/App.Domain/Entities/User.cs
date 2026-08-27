using App.Domain.Common;

namespace App.Domain.Entities;

public enum UserRole { Agente, Admin }

public class User : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;       // único global, ver 5.3
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Agente;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}