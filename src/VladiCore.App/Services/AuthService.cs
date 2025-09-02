using Microsoft.EntityFrameworkCore;
using VladiCore.App.Exceptions;
using VladiCore.Domain.Entities;
using VladiCore.Data;

namespace VladiCore.App.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;

    public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string username)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new AppException(409, "user_exists", "Email already registered");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "user");
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Name = "user" };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = username,
            PasswordHash = _hasher.Hash(password),
            RoleId = role.Id,
            Role = role
        };
        _db.Users.Add(user);
        var refresh = _jwt.IssueRefreshToken(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refresh.token,
            ExpiresAt = refresh.expiresAt
        });
        await _db.SaveChangesAsync();
        var access = _jwt.IssueAccessToken(user);
        return new AuthResult(access.token, refresh.token, role.Name);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !_hasher.Verify(user.PasswordHash, password))
            throw new AppException(401, "invalid_credentials", "Invalid email or password");

        var refresh = _jwt.IssueRefreshToken(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refresh.token,
            ExpiresAt = refresh.expiresAt
        });
        await _db.SaveChangesAsync();
        var access = _jwt.IssueAccessToken(user);
        return new AuthResult(access.token, refresh.token, user.Role?.Name);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token == null || token.RevokedAt != null || token.ExpiresAt <= DateTime.UtcNow)
            throw new AppException(401, "invalid_refresh", "Refresh token invalid");

        token.RevokedAt = DateTime.UtcNow;
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == token.UserId) ??
            throw new AppException(404, "user_not_found", "User not found");

        var newRefresh = _jwt.IssueRefreshToken(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefresh.token,
            ExpiresAt = newRefresh.expiresAt
        });
        await _db.SaveChangesAsync();
        var access = _jwt.IssueAccessToken(user);
        return new AuthResult(access.token, newRefresh.token, user.Role?.Name);
    }

    public async Task LogoutAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
