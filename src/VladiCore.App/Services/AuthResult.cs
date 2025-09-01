using VladiCore.Domain.Enums;

namespace VladiCore.App.Services;

public record AuthResult(string AccessToken, string RefreshToken, UserRole Role);
