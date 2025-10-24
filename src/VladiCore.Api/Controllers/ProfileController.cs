using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VladiCore.Data.Contexts;
using VladiCore.Data.Identity;
using VladiCore.Domain.DTOs;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UserManager<ApplicationUser> userManager, AppDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await BuildProfileResponseAsync(user).ConfigureAwait(false);
        return Ok(response);
    }

    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await ResolveUserAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Unauthorized();
        }

        if (user.IsBlocked)
        {
            return Forbid();
        }

        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.DisplayName : request.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var response = await BuildProfileResponseAsync(user).ConfigureAwait(false);
        return Ok(response);
    }

    private async Task<ApplicationUser?> ResolveUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var guid))
        {
            return null;
        }

        return await _userManager.Users.FirstOrDefaultAsync(u => u.Id == guid, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProfileResponse> BuildProfileResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        return new ProfileResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            IsBlocked = user.IsBlocked,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roles.ToArray()
        };
    }
}
