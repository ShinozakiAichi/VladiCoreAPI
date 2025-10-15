using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using VladiCore.Api.Controllers;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Api.Services;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class AuthControllerTests
{
    [Test]
    public async Task Register_ShouldCreateUserAndReturnTokens()
    {
        await using var fixture = new AuthControllerFixture();
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Password = "P@ssw0rd123!",
            DisplayName = "Test User"
        };

        var result = await fixture.Controller.Register(request);

        var createdResult = result.Result as CreatedResult;
        createdResult.Should().NotBeNull();
        createdResult!.Location.Should().Be("/me");

        var response = createdResult.Value as AuthResponse;
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBe(Guid.Empty);

        var storedUser = await fixture.Context.Users.SingleAsync();
        storedUser.Email.Should().Be("user@example.com");
        storedUser.DisplayName.Should().Be("Test User");

        var refreshToken = await fixture.Context.RefreshTokens.SingleAsync();
        refreshToken.UserId.Should().Be(storedUser.Id);
        refreshToken.RevokedAt.Should().BeNull();

        var roles = await fixture.UserManager.GetRolesAsync(storedUser);
        roles.Should().Contain("User");
    }

    [Test]
    public async Task Refresh_ShouldReturnUnauthorized_WhenTokenExpired()
    {
        await using var fixture = new AuthControllerFixture();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "blocked@example.com",
            UserName = "blocked@example.com",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await fixture.UserManager.CreateAsync(user, "P@ssw0rd123!");
        createResult.Succeeded.Should().BeTrue();

        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        await fixture.Context.RefreshTokens.AddAsync(expiredToken);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Controller.Refresh(new RefreshRequest { RefreshToken = expiredToken.Id });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        var problem = (result.Result as UnauthorizedObjectResult)!.Value as ProblemDetails;
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Invalid refresh token");
    }

    private sealed class AuthControllerFixture : IAsyncDisposable
    {
        private readonly UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid> _userStore;
        private readonly RoleStore<IdentityRole<Guid>, AppDbContext, Guid> _roleStore;
        private readonly ServiceProvider _serviceProvider;

        public AppDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole<Guid>> RoleManager { get; }
        public AuthController Controller { get; }

        public AuthControllerFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            _userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid>(Context);
            _roleStore = new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(Context);

            var identityOptions = Options.Create(new IdentityOptions());
            identityOptions.Value.Password.RequireDigit = false;
            identityOptions.Value.Password.RequireLowercase = false;
            identityOptions.Value.Password.RequireUppercase = false;
            identityOptions.Value.Password.RequireNonAlphanumeric = false;
            identityOptions.Value.Password.RequiredLength = 6;
            identityOptions.Value.Password.RequiredUniqueChars = 0;

            _serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            UserManager = new UserManager<ApplicationUser>(
                _userStore,
                identityOptions,
                new PasswordHasher<ApplicationUser>(),
                new IUserValidator<ApplicationUser>[] { new UserValidator<ApplicationUser>() },
                new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                _serviceProvider,
                NullLogger<UserManager<ApplicationUser>>.Instance);

            RoleManager = new RoleManager<IdentityRole<Guid>>(
                _roleStore,
                new IRoleValidator<IdentityRole<Guid>>[] { new RoleValidator<IdentityRole<Guid>>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);

            var jwtOptions = Options.Create(new JwtOptions
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                SigningKey = "test-signing-key-should-be-long-123456",
                AccessTokenTtlSeconds = 600,
                RefreshTokenTtlSeconds = 3600
            });

            var tokenService = new JwtTokenService(jwtOptions);
            Controller = new AuthController(UserManager, RoleManager, Context, tokenService, jwtOptions);
        }

        public async ValueTask DisposeAsync()
        {
            _userStore.Dispose();
            _roleStore.Dispose();
            _serviceProvider.Dispose();
            await Context.DisposeAsync();
        }
    }
}
