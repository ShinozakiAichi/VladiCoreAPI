using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;

namespace VladiCore.Api.Controllers;

[Route("api/products/{productId:int}/reviews")]
public class ReviewsController : BaseApiController
{
    private const int FloodLimit = 1;
    private static readonly TimeSpan FloodWindow = TimeSpan.FromMinutes(1);
    private readonly ReviewOptions _options;

    public ReviewsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IOptions<ReviewOptions> options)
        : base(dbContext, cache, rateLimiter)
    {
        _options = options.Value;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(
        int productId,
        int skip = 0,
        int take = 20,
        string order = "createdAt",
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Max(1, Math.Min(100, take));
        order = (order ?? "createdAt").Trim().ToLowerInvariant();

        var cacheKey = $"reviews:{productId}:{skip}:{take}:{order}";
        var payload = await Cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(5), async () =>
        {
            var query = DbContext.ProductReviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId && r.IsApproved);

            query = order switch
            {
                "rating" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                _ => query.OrderByDescending(r => r.CreatedAt)
            };

            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await query
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var reviews = items.Select(ToDto).ToList();
            return new { total, items = reviews };
        }).ConfigureAwait(false);

        var etag = HashUtility.Compute(System.Text.Json.JsonSerializer.Serialize(payload));
        return CachedOk(payload, etag, TimeSpan.FromMinutes(5));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        int productId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        if (_options.RequireAuthentication && !(User?.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        var productExists = await DbContext.Products.AnyAsync(p => p.Id == productId, cancellationToken).ConfigureAwait(false);
        if (!productExists)
        {
            return NotFound();
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        var limiterKey = $"review:{productId}:{remoteIp}:{HashUtility.Compute(userAgent)}";
        if (!RateLimiter.IsAllowed(limiterKey, FloodLimit, FloodWindow))
        {
            return StatusCode((int)HttpStatusCode.TooManyRequests, "Too many reviews submitted. Please try again later.");
        }

        var photos = (request.Photos ?? new List<string>())
            .Where(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .Take(6)
            .ToArray();

        var review = new ProductReview
        {
            ProductId = productId,
            UserId = ResolveUserId(request.UserId),
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Body = request.Body.Trim(),
            Photos = photos,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        await DbContext.ProductReviews.AddAsync(review, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix($"reviews:{productId}:");

        var response = ToDto(review);
        return Accepted(response);
    }

    [HttpPost("{reviewId:long}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int productId, long reviewId, CancellationToken cancellationToken = default)
    {
        var review = await DbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);
        if (review == null)
        {
            return NotFound();
        }

        if (!review.IsApproved)
        {
            review.IsApproved = true;
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Cache.RemoveByPrefix($"reviews:{productId}:");
        }

        return NoContent();
    }

    [HttpDelete("{reviewId:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int productId, long reviewId, CancellationToken cancellationToken = default)
    {
        var review = await DbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);
        if (review == null)
        {
            return NotFound();
        }

        DbContext.ProductReviews.Remove(review);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Cache.RemoveByPrefix($"reviews:{productId}:");

        return NoContent();
    }

    private string? ResolveUserId(string? requestedUserId)
    {
        if (!string.IsNullOrWhiteSpace(requestedUserId))
        {
            return requestedUserId.Trim();
        }

        var principal = User;
        var identity = principal?.Identity;
        if (identity?.IsAuthenticated == true)
        {
            var userId = principal?.FindFirst("sub")?.Value
                ?? principal?.FindFirst("uid")?.Value
                ?? identity.Name;
            return string.IsNullOrWhiteSpace(userId) ? null : userId;
        }

        return null;
    }

    private static ReviewDto ToDto(ProductReview review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            Rating = review.Rating,
            Title = review.Title,
            Body = review.Body,
            Photos = review.Photos,
            IsApproved = review.IsApproved,
            CreatedAt = review.CreatedAt
        };
    }
}
