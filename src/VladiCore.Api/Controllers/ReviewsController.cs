using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VladiCore.Api.Infrastructure;
using VladiCore.Api.Infrastructure.Options;
using VladiCore.Data.Contexts;
using VladiCore.Domain.DTOs;
using VladiCore.Domain.Entities;
using VladiCore.Domain.Services;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/reviews")]
public class ReviewsController : BaseApiController
{
    private const int MaxPhotos = CreateReviewRequest.MaxPhotoCount;
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "-createdat",
        "createdat",
        "-usefulup",
        "rating",
        "-rating"
    };

    private readonly ReviewOptions _options;
    private readonly IRatingService _ratingService;
    private readonly string? _cdnBaseUrl;

    public ReviewsController(
        AppDbContext dbContext,
        ICacheProvider cache,
        IRateLimiter rateLimiter,
        IOptions<ReviewOptions> options,
        IOptions<S3Options> storageOptions,
        IRatingService ratingService)
        : base(dbContext, cache, rateLimiter)
    {
        _options = options.Value;
        _ratingService = ratingService;
        _cdnBaseUrl = storageOptions.Value.CdnBaseUrl;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetReviews(
        int productId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string sort = "-createdAt",
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);
        sort = NormalizeSort(sort);

        var cacheKey = $"product:{productId}:reviews:{skip}:{take}:{sort}";
        var result = await Cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(2), async () =>
        {
            IQueryable<ProductReview> query = DbContext.ProductReviews
                .AsNoTracking()
                .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved)
                .Include(r => r.User);

            query = sort switch
            {
                "createdat" => query.OrderBy(r => r.CreatedAt),
                "-usefulup" => query
                    .OrderByDescending(r => r.UsefulUp)
                    .ThenByDescending(r => r.CreatedAt),
                "rating" => query
                    .OrderByDescending(r => r.Rating)
                    .ThenByDescending(r => r.CreatedAt),
                "-rating" => query
                    .OrderBy(r => r.Rating)
                    .ThenBy(r => r.CreatedAt),
                _ => query.OrderByDescending(r => r.CreatedAt)
            };

            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await query
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var dtos = items.Select(ToDto).ToList();
            return new PagedResult<ReviewDto>
            {
                Items = dtos,
                Total = total,
                Skip = skip,
                Take = take
            };
        }).ConfigureAwait(false);

        var etag = HashUtility.Compute(System.Text.Json.JsonSerializer.Serialize(result));
        return CachedOk(result, etag, TimeSpan.FromMinutes(1));
    }

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview(
        int productId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = CurrentUserId;
        if (userId == null)
        {
            if (_options.RequireAuthentication)
            {
                return Unauthorized(ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication required"));
            }

            return Forbid();
        }

        var product = await DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            .ConfigureAwait(false);
        if (product == null)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                StatusCodes.Status404NotFound,
                "Product not found"));
        }

        var hasExistingReview = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .AnyAsync(r => r.ProductId == productId && r.UserId == userId && !r.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
        if (hasExistingReview)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate review",
                Detail = "Update your existing review instead of creating a new one.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var limiterKey = $"review:create:{productId}:{userId}:{HashUtility.Compute(remoteIp)}";
        if (!RateLimiter.IsAllowed(limiterKey, 3, TimeSpan.FromMinutes(1)))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Too many reviews",
                Detail = "Please wait before submitting another review.",
                Status = StatusCodes.Status429TooManyRequests
            });
        }

        var normalizedPhotos = NormalizePhotos(productId, request.Photos ?? new List<string>());
        var now = DateTime.UtcNow;
        var review = new ProductReview
        {
            ProductId = productId,
            UserId = userId.Value,
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Text = request.Text.Trim(),
            Photos = normalizedPhotos,
            Status = ReviewStatus.Pending,
            UsefulDown = 0,
            UsefulUp = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbContext.ProductReviews.AddAsync(review, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix($"product:{productId}:reviews");
        Cache.RemoveByPrefix($"products:{productId}");
        Cache.RemoveByPrefix("products:list");

        var dto = ToDto(review);
        return CreatedAtAction(nameof(GetReviews), new { productId, skip = 0, take = 1 }, dto);
    }

    [HttpPatch("{reviewId:long}")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<ReviewDto>> PatchReview(
        int productId,
        long reviewId,
        [FromBody] PatchReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var review = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.IsDeleted)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && (userId == null || review.UserId != userId.Value))
        {
            return Forbid();
        }

        if (!isAdmin)
        {
            var editableStatuses = new[] { ReviewStatus.Pending, ReviewStatus.Approved };
            if (!editableStatuses.Contains(review.Status))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Review locked",
                    Detail = "Review cannot be edited in its current status.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            var cutoff = review.CreatedAt.AddHours(Math.Max(_options.UserEditWindowHours, 0));
            if (DateTime.UtcNow > cutoff)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Edit window expired",
                    Detail = "The review can no longer be edited.",
                    Status = StatusCodes.Status403Forbidden
                });
            }
        }

        var wasApproved = review.Status == ReviewStatus.Approved;
        var normalizedPhotos = request.Photos == null ? review.Photos : NormalizePhotos(productId, request.Photos);

        if (request.Rating != null)
        {
            review.Rating = request.Rating.Value;
        }

        if (request.Title != null)
        {
            review.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        }

        if (request.Text != null)
        {
            review.Text = request.Text.Trim();
        }

        review.Photos = normalizedPhotos;
        review.UpdatedAt = DateTime.UtcNow;

        if (!isAdmin && wasApproved && (request.Rating != null || request.Title != null || request.Text != null || request.Photos != null))
        {
            review.Status = ReviewStatus.Pending;
            review.ModerationNote = null;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (wasApproved && review.Status != ReviewStatus.Approved)
        {
            await _ratingService.RecomputeAsync(productId, cancellationToken).ConfigureAwait(false);
        }
        else if (review.Status == ReviewStatus.Approved)
        {
            await _ratingService.RecomputeAsync(productId, cancellationToken).ConfigureAwait(false);
        }

        Cache.RemoveByPrefix($"product:{productId}:reviews");
        Cache.RemoveByPrefix($"products:{productId}");
        Cache.RemoveByPrefix("products:list");

        return Ok(ToDto(review));
    }

    [HttpPost("{reviewId:long}/photos")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<ReviewDto>> ConfirmPhoto(
        int productId,
        long reviewId,
        [FromBody] ConfirmReviewPhotoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var review = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.IsDeleted)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && (userId == null || review.UserId != userId.Value))
        {
            return Forbid();
        }

        if (!request.Key.StartsWith($"reviews/{productId}/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid storage key",
                Detail = "Key must start with the review prefix.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (review.Photos.Length >= MaxPhotos)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Photo limit reached",
                Status = StatusCodes.Status409Conflict
            });
        }

        var wasApproved = review.Status == ReviewStatus.Approved;
        var photos = review.Photos.ToList();
        photos.Add(request.Key.Trim());
        review.Photos = photos.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxPhotos).ToArray();
        review.UpdatedAt = DateTime.UtcNow;

        if (!isAdmin && wasApproved)
        {
            review.Status = ReviewStatus.Pending;
            review.ModerationNote = null;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (wasApproved && review.Status != ReviewStatus.Approved)
        {
            await _ratingService.RecomputeAsync(productId, cancellationToken).ConfigureAwait(false);
        }

        Cache.RemoveByPrefix($"product:{productId}:reviews");
        Cache.RemoveByPrefix($"products:{productId}");
        Cache.RemoveByPrefix("products:list");

        return Ok(ToDto(review));
    }

    [HttpDelete("{reviewId:long}")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> DeleteReview(
        int productId,
        long reviewId,
        [FromQuery] string? moderationNote = null,
        CancellationToken cancellationToken = default)
    {
        var review = await DbContext.ProductReviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.IsDeleted)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && (userId == null || review.UserId != userId.Value))
        {
            return Forbid();
        }

        var wasApproved = review.Status == ReviewStatus.Approved;
        review.IsDeleted = true;
        review.UpdatedAt = DateTime.UtcNow;

        if (isAdmin)
        {
            review.Status = ReviewStatus.RemovedByAdmin;
            review.ModerationNote = string.IsNullOrWhiteSpace(moderationNote) ? "Removed by administrator" : moderationNote.Trim();
        }
        else
        {
            review.Status = ReviewStatus.DeletedByAuthor;
            review.ModerationNote = null;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (wasApproved)
        {
            await _ratingService.RecomputeAsync(productId, cancellationToken).ConfigureAwait(false);
        }

        Cache.RemoveByPrefix($"product:{productId}:reviews");
        Cache.RemoveByPrefix($"products:{productId}");
        Cache.RemoveByPrefix("products:list");

        return NoContent();
    }

    [HttpPost("{reviewId:long}/vote")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<object>> Vote(
        int productId,
        long reviewId,
        [FromBody] ReviewVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = CurrentUserId;
        if (userId == null)
        {
            return Unauthorized(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status401Unauthorized, "Authentication required"));
        }

        var review = await DbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);

        if (review == null || review.Status != ReviewStatus.Approved)
        {
            return NotFound(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status404NotFound, "Review not found"));
        }

        var desired = string.Equals(request.Value, "up", StringComparison.OrdinalIgnoreCase) ? (sbyte)1 : (sbyte)(-1);
        var vote = await DbContext.ProductReviewVotes.FindAsync(new object[] { reviewId, userId.Value }, cancellationToken);

        if (vote == null)
        {
            vote = new ProductReviewVote
            {
                ReviewId = reviewId,
                UserId = userId.Value,
                Value = desired
            };
            await DbContext.ProductReviewVotes.AddAsync(vote, cancellationToken).ConfigureAwait(false);
            if (desired > 0)
            {
                review.UsefulUp += 1;
            }
            else
            {
                review.UsefulDown += 1;
            }
        }
        else if (vote.Value != desired)
        {
            if (vote.Value > 0)
            {
                review.UsefulUp = Math.Max(0, review.UsefulUp - 1);
            }
            else
            {
                review.UsefulDown = Math.Max(0, review.UsefulDown - 1);
            }

            vote.Value = desired;

            if (desired > 0)
            {
                review.UsefulUp += 1;
            }
            else
            {
                review.UsefulDown += 1;
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Cache.RemoveByPrefix($"product:{productId}:reviews");

        return Ok(new { review.UsefulUp, review.UsefulDown });
    }

    private string[] NormalizePhotos(int productId, IReadOnlyCollection<string> photos)
    {
        return photos
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(p => IsAllowedPhotoReference(productId, p))
            .Take(MaxPhotos)
            .ToArray();
    }

    private bool IsAllowedPhotoReference(int productId, string path)
    {
        if (path.StartsWith($"reviews/{productId}/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_cdnBaseUrl)
            && Uri.TryCreate(_cdnBaseUrl, UriKind.Absolute, out var cdnUri)
            && Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, cdnUri.Host, StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith($"/reviews/{productId}/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeSort(string sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return "-createdat";
        }

        var normalized = sort.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return AllowedSorts.Contains(normalized) ? normalized : "-createdat";
    }

    internal static ReviewDto ToDto(ProductReview review)
    {
        return new ReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            UserDisplay = review.User?.DisplayName ?? review.User?.Email,
            Rating = review.Rating,
            Title = review.Title,
            Text = review.Text,
            Photos = review.Photos,
            Status = review.Status.ToString(),
            ModerationNote = review.ModerationNote,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            UsefulUp = review.UsefulUp,
            UsefulDown = review.UsefulDown
        };
    }
}
