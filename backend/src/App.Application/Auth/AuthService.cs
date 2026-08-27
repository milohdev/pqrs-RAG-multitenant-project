using App.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Auth;

public record LoginResult(string Token, Guid TenantId, string Role);

public class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _db = db; _hasher = hasher; _jwt = jwt;
    }

    public async Task<LoginResult?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await ((DbSet<App.Domain.Entities.User>)_db.Users)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !_hasher.Verify(password, user.PasswordHash)) return null;

        var token = _jwt.GenerateToken(user);
        return new LoginResult(token, user.TenantId, user.Role.ToString());
    }
}