using System;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace VladiCore.Api.Infrastructure.Security;

public static class JwtSecurityKeyFactory
{
    private const int MinKeySizeInBits = 128;

    public static SymmetricSecurityKey Create(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new ArgumentException("JWT signing key must be provided.", nameof(signingKey));
        }

        var keyBytes = TryDecodeBase64(signingKey, out var decodedBytes)
            ? decodedBytes
            : Encoding.UTF8.GetBytes(signingKey);

        if (keyBytes.Length * 8 < MinKeySizeInBits)
        {
            throw new InvalidOperationException(
                $"JWT signing key must be at least {MinKeySizeInBits / 8} bytes (128 bits) long.");
        }

        return new SymmetricSecurityKey(keyBytes);
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[value.Length];
        if (Convert.TryFromBase64String(value, buffer, out var bytesWritten))
        {
            bytes = buffer.AsSpan(0, bytesWritten).ToArray();
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }
}
