using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VladiCore.Api.Infrastructure;
using VladiCore.Data.Contexts;
using VladiCore.Data.Identity;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Services;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize(Policy = "Admin")]
public class ReviewModerationController : BaseApiController
{
    private readonly IRatingService _ratingService;

    public ReviewModerationController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IRatingService ratingService)
        : base(dbContext, cache, rateLimiter)
    {
        _ratingService = ratingService;
    }

    [HttpGet("moderation")]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetQueue(
        [FromQuery] string status = "Pending",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);

        if (!Enum.TryParse<ReviewStatus>(status, true, out var parsedStatus))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid status",
                Detail = "Status must be a valid review status.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var baseQuery = DbContext.ProductReviews
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == parsedStatus);

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await baseQuery
            .OrderBy(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .GroupJoin(
                DbContext.Users.AsNoTracking(),
                review => review.UserId,
                user => user.Id,
                (review, users) => new { review, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, user) => new { x.review, user })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = items.Select(item => ReviewsController.ToDto(item.review, item.user)).ToList();
        return Ok(new PagedResult<ReviewDto>
        {
            Items = dtos,
            Total = total,
            Skip = skip,
            Take = take
        });
    }

    [HttpPost("{reviewId:long}/approve")]
    public async Task<ActionResult<ReviewDto>> Approve(
        long reviewId,
        CancellationToken cancellationToken = default)
    {
        var review = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.IsDeleted)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        review.Status = ReviewStatus.Approved;
        review.ModerationNote = null;
        review.UpdatedAt = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _ratingService.RecomputeAsync(review.ProductId, cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix($"product:{review.ProductId}:reviews");
        Cache.RemoveByPrefix($"products:{review.ProductId}");
        Cache.RemoveByPrefix("products:list");

        var user = await DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == review.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(ReviewsController.ToDto(review, user));
    }

    [HttpPost("{reviewId:long}/reject")]
    public async Task<ActionResult<ReviewDto>> Reject(
        long reviewId,
        [FromBody] RejectReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var review = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.IsDeleted)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        var wasApproved = review.Status == ReviewStatus.Approved;
        review.Status = ReviewStatus.Rejected;
        review.ModerationNote = string.IsNullOrWhiteSpace(request.Note)
            ? request.Reason
            : $"{request.Reason}: {request.Note.Trim()}";
        review.UpdatedAt = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (wasApproved)
        {
            await _ratingService.RecomputeAsync(review.ProductId, cancellationToken).ConfigureAwait(false);
        }

        Cache.RemoveByPrefix($"product:{review.ProductId}:reviews");
        Cache.RemoveByPrefix($"products:{review.ProductId}");
        Cache.RemoveByPrefix("products:list");

        var user = await DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == review.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(ReviewsController.ToDto(review, user));
    }
}
