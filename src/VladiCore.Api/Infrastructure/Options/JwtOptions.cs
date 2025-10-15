using System;
using System.ComponentModel.DataAnnotations;

namespace VladiCore.Api.Infrastructure.Options;

public class JwtOptions
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(60, 3600)]
    public int AccessTokenTtlSeconds { get; set; } = 900;

    [Range(600, 2592000)]
    public int RefreshTokenTtlSeconds { get; set; } = 1209600;
}
