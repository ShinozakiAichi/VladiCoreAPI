using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using VladiCore.Api.Controllers;
using VladiCore.Data.Contexts;
using VladiCore.Data.Identity;
using VladiCore.Domain.DTOs;

namespace VladiCore.Tests.Controllers;

[TestFixture]
public class ProfileControllerTests
{
    [Test]
    public async Task GetProfile_ShouldReturnRoles()
    {
        await using var fixture = new ProfileControllerFixture();
        var user = await fixture.CreateUserAsync("admin@example.com", "Admin").ConfigureAwait(false);

        var controller = fixture.CreateControllerForUser(user);

        var result = await controller.GetProfile(CancellationToken.None).ConfigureAwait(false);

        result.Result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result.Result!).Value as ProfileResponse;
        payload.Should().NotBeNull();
        payload!.Roles.Should().Contain("Admin");
    }

    [Test]
    public async Task UpdateProfile_ShouldReturnRoles()
    {
        await using var fixture = new ProfileControllerFixture();
        var user = await fixture.CreateUserAsync("editor@example.com", "Admin").ConfigureAwait(false);

        var controller = fixture.CreateControllerForUser(user);

        var request = new UpdateProfileRequest
        {
            DisplayName = "Updated Name",
            PhoneNumber = "+123456789"
        };

        var result = await controller.UpdateProfile(request, CancellationToken.None).ConfigureAwait(false);

        result.Result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result.Result!).Value as ProfileResponse;
        payload.Should().NotBeNull();
        payload!.DisplayName.Should().Be("Updated Name");
        payload.Roles.Should().Contain("Admin");
    }

    private sealed class ProfileControllerFixture : IAsyncDisposable
    {
        private readonly UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid> _userStore;
        private readonly RoleStore<IdentityRole<Guid>, AppDbContext, Guid> _roleStore;
        private readonly ServiceProvider _serviceProvider;

        public AppDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole<Guid>> RoleManager { get; }

        public ProfileControllerFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            _userStore = new UserStore<ApplicationUser, IdentityRole<Guid>, AppDbContext, Guid>(Context);
            _roleStore = new RoleStore<IdentityRole<Guid>, AppDbContext, Guid>(Context);
            _serviceProvider = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            UserManager = new UserManager<ApplicationUser>(
                _userStore,
                new OptionsWrapper<IdentityOptions>(new IdentityOptions()),
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
        }

        public async Task<ApplicationUser> CreateUserAsync(string email, string role)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await UserManager.CreateAsync(user, "P@ssw0rd123!").ConfigureAwait(false);
            createResult.Succeeded.Should().BeTrue("user creation should succeed in tests");

            if (!await RoleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                var roleResult = await RoleManager.CreateAsync(new IdentityRole<Guid>(role)).ConfigureAwait(false);
                roleResult.Succeeded.Should().BeTrue("role creation should succeed in tests");
            }

            var addRoleResult = await UserManager.AddToRoleAsync(user, role).ConfigureAwait(false);
            addRoleResult.Succeeded.Should().BeTrue("adding role should succeed in tests");

            return user;
        }

        public ProfileController CreateControllerForUser(ApplicationUser user)
        {
            var controller = new ProfileController(UserManager, Context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                        }, "Test"))
                    }
                }
            };

            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            _userStore.Dispose();
            _roleStore.Dispose();
            _serviceProvider.Dispose();
            await Context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
