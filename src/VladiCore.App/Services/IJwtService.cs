using VladiCore.Domain.Entities;

namespace VladiCore.App.Services;

public interface IJwtService
{
    (string token, DateTime expiresAt) IssueAccessToken(User user);
    (string token, DateTime expiresAt) IssueRefreshToken(User user);
}
