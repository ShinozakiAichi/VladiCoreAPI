using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.PcBuilder.Services;

namespace VladiCore.Api.Controllers;

[Route("api/pc")]
public class PcController : BaseApiController
{
    private readonly IPcCompatibilityService _compatibilityService;
    private readonly IPcAutoBuilderService _autoBuilderService;

    public PcController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IPcCompatibilityService compatibilityService,
        IPcAutoBuilderService autoBuilderService)
        : base(dbContext, cache, rateLimiter)
    {
        _compatibilityService = compatibilityService;
        _autoBuilderService = autoBuilderService;
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateBuild([FromBody] PcValidateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var result = await _compatibilityService.ValidateAsync(request);
        return Ok(result);
    }

    [HttpPost("autobuild")]
    [AllowAnonymous]
    public async Task<IActionResult> AutoBuild([FromBody] AutoBuildRequest request)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var result = await _autoBuilderService.BuildAsync(request);
        return Ok(result);
    }
}
