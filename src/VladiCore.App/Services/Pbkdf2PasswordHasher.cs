using System.Security.Cryptography;

namespace VladiCore.App.Services;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySize);
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(key);
    }

    public bool Verify(string hash, string password)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var key = Convert.FromBase64String(parts[1]);
        var attempted = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(key, attempted);
    }
}
