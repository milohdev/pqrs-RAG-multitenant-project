using App.Domain.Entities;

namespace App.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}