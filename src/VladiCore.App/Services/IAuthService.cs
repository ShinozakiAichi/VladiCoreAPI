using VladiCore.Domain.Enums;

namespace VladiCore.App.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string fullName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(Guid userId);
}
