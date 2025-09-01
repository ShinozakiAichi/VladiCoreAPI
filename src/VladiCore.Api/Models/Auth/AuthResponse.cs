namespace VladiCore.Api.Models.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, string? Role = null);
