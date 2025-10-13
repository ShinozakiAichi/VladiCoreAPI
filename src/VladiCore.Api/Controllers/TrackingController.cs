using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Data.Repositories;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Route("api/track")]
public class TrackingController : BaseApiController
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int Limit = 30;

    public TrackingController(AppDbContext dbContext, ICacheProvider cache, IRateLimiter rateLimiter)
        : base(dbContext, cache, rateLimiter)
    {
    }

    [HttpPost("view")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackView([FromBody] TrackViewDto dto)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!RateLimiter.IsAllowed(ip, Limit, Window))
        {
            return StatusCode((int)HttpStatusCode.TooManyRequests, "Rate limit exceeded");
        }

        var repository = new EfRepository<ProductView>(DbContext);
        await repository.AddAsync(new ProductView
        {
            ProductId = dto.ProductId,
            SessionId = dto.SessionId,
            UserId = dto.UserId,
            ViewedAt = DateTime.UtcNow
        });
        await repository.SaveChangesAsync();

        Cache.Remove($"reco:{dto.ProductId}:10:0");

        return Accepted();
    }
}
