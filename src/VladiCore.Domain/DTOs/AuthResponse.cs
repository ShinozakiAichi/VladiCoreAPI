using System;

namespace VladiCore.Domain.DTOs;

public class AuthResponse
{
    public Guid UserId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public Guid RefreshToken { get; set; }

    public DateTime RefreshExpiresAt { get; set; }
}
