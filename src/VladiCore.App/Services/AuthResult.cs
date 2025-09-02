namespace VladiCore.App.Services;

public record AuthResult(string AccessToken, string RefreshToken, string? Role);
